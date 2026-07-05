using System.Collections.Generic;
using HarmonyLib;
using InterServerPortal.Core;
using UnityEngine;

namespace InterServerPortal.Portal
{
    /// <summary>
    /// Give each portal mode a distinct glow so you can tell at a glance what a
    /// portal does — and, like a vanilla portal, whether it's actually wired up:
    ///
    ///   • vanilla tag-pair → untouched (native blue). Active = a 1:1 tag partner
    ///     was found (vanilla already drives this; we don't touch it).
    ///   • same-world network → violet (#c4b5fd). Active = the builder's mesh has
    ///     at least one OTHER network portal to hop to.
    ///   • inter-server → sky/cyan (#7dd3fc). Active = it has a usable travel
    ///     option (a configured local-world destination, or — on a local world —
    ///     a "Return to origin server" path).
    ///
    /// An unwired portal of either custom mode sits dark exactly like a vanilla
    /// portal with no partner, so "off" reads the same everywhere.
    ///
    /// Mirrors vanilla's own split: the 0.5s <c>UpdatePortal</c> tick recomputes
    /// the on/off state and the per-frame <c>Update</c> lerps <c>_EmissionColor</c>
    /// toward it — the same emission channel and animation vanilla uses, so the
    /// glow fades in/out identically.
    /// </summary>
    [HarmonyPatch(typeof(TeleportWorld))]
    internal static class PortalVisuals
    {
        // How much to boost the base hue for the HDR emission channel so the glow
        // reads as brightly as vanilla's target-found color.
        private const float EmissionBoost = 2.0f;

        // Active-state emission colors, matched to the hover-text hues.
        // Base 0..1 hues, matched to the hover-text colors. The model emission uses
        // the boosted HDR version; the swirl particles/light use the plain hue.
        private static readonly Color InterServerHue = new Color(0.490f, 0.827f, 0.988f); // #7dd3fc
        private static readonly Color NetworkHue = new Color(0.769f, 0.710f, 0.992f);     // #c4b5fd

        private static readonly int EnabledHash = "ISP.enabled".GetStableHashCode();
        private static readonly int LinkHash = "ISP.link".GetStableHashCode();

        private sealed class VState
        {
            public bool Managed;       // we own this portal's color (network / inter-server)
            public bool Active;        // should it glow?
            public Color Hue;          // this mode's base (0..1) color
            public float Alpha;        // animated 0..1, mirrors vanilla m_colorAlpha
            public bool FxTinted;      // have we already recolored this portal's FX?

            // Every particle system + light under the portal, and their captured
            // vanilla colors, so the whole effect (inner vortex AND the outward
            // flame burst) is retinted — and can be restored if flipped to vanilla.
            public bool FxCaptured;
            public ParticleSystem[] Particles;
            public ParticleSystem.MinMaxGradient[] OrigStartColors;
            public bool[] OrigColorOverLife;                       // was the module enabled?
            public ParticleSystem.MinMaxGradient[] OrigColorOverLifeColors;
            public List<MatCapture> Mats;   // particle renderer materials (instanced) + orig colors
            public Light[] Lights;
            public Color[] OrigLightColors;
        }

        /// <summary>An instanced particle material + the color properties we retint on it.</summary>
        private sealed class MatCapture
        {
            public Material Mat;
            public int[] Props;
            public bool[] IsEmission;
            public Color[] Orig;
        }

        // Candidate color properties on particle materials. Different portal FX use
        // different shaders, so we tint whichever of these the material actually has.
        // (The gradient-mapped `blue flames` system bakes its color into _MainTex and
        // exposes no color property, so it is intentionally left orange in all modes.)
        private static readonly string[] TintPropNames =
            { "_TintColor", "_Color", "_BaseColor", "_EmissionColor" };

        // Per-portal color state. Pruned when a portal turns vanilla or is destroyed.
        private static readonly Dictionary<TeleportWorld, VState> States =
            new Dictionary<TeleportWorld, VState>();

        /// <summary>
        /// 0.5s tick (piggybacks vanilla's own InvokeRepeating): recompute the
        /// mode + on/off state so <see cref="Update_Postfix"/> has something cheap
        /// to lerp toward every frame.
        /// </summary>
        [HarmonyPatch("UpdatePortal")]
        [HarmonyPostfix]
        private static void UpdatePortal_Postfix(TeleportWorld __instance)
        {
            PruneDead();

            var nview = __instance.m_nview;
            if (nview == null || !nview.IsValid()) return;

            bool interServer = PortalData.IsInterServer(nview);
            bool network = !interServer && PortalData.IsNetwork(nview);

            if (!interServer && !network)
            {
                // Vanilla-managed portal — restore its FX if we'd tinted it, then
                // hand the color back to vanilla Update.
                if (States.TryGetValue(__instance, out var old))
                {
                    RestoreFx(old);
                    States.Remove(__instance);
                }
                return;
            }

            if (!States.TryGetValue(__instance, out var st))
            {
                st = new VState();
                States[__instance] = st;
            }
            st.Managed = true;

            var hue = interServer ? InterServerHue : NetworkHue;
            if (st.Hue != hue) st.FxTinted = false; // mode changed → re-tint the FX
            st.Hue = hue;
            st.Active = interServer ? IsInterServerActive(nview) : IsNetworkActive(nview);

            DriveSwirl(__instance, st);
        }

        /// <summary>
        /// Show/hide the near-portal swirl vortex (vanilla's <c>m_target_found</c>)
        /// for a mode we manage — vanilla keeps it off since our portals have no
        /// tag connection — and retint the whole portal effect (inner vortex + the
        /// outward flame burst) to the mode hue so it all matches the frame glow.
        /// </summary>
        private static void DriveSwirl(TeleportWorld portal, VState st)
        {
            if (portal.m_target_found != null && portal.m_proximityRoot != null)
            {
                // Same "player near + can teleport" gate vanilla uses for the vortex.
                var closest = Player.GetClosestPlayer(portal.m_proximityRoot.position, portal.m_activationRange);
                bool playerReady = closest != null && (closest.IsTeleportable() || portal.m_allowAllItems);
                portal.m_target_found.SetActive(playerReady && st.Active);
            }

            if (!st.FxTinted)
            {
                CaptureFx(portal, st);
                TintFx(st, st.Hue);
                st.FxTinted = true;
            }
        }

        /// <summary>
        /// Gather + remember every particle system and light on the portal (once),
        /// before we overwrite their colors. startColor / Light.color are per-instance
        /// (not shared assets) so tinting never bleeds onto vanilla portals.
        /// </summary>
        private static void CaptureFx(TeleportWorld portal, VState st)
        {
            if (st.FxCaptured) return;

            st.Particles = portal.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            int p = st.Particles.Length;
            st.OrigStartColors = new ParticleSystem.MinMaxGradient[p];
            st.OrigColorOverLife = new bool[p];
            st.OrigColorOverLifeColors = new ParticleSystem.MinMaxGradient[p];
            for (int i = 0; i < p; i++)
            {
                var ps = st.Particles[i];
                if (ps == null) continue;
                st.OrigStartColors[i] = ps.main.startColor;
                var col = ps.colorOverLifetime;
                st.OrigColorOverLife[i] = col.enabled;
                st.OrigColorOverLifeColors[i] = col.color;
            }

            // Particle renderer materials — the fire color often lives here (an
            // orange-tinted/emissive material), not in startColor/gradient. Use the
            // instanced .material so each portal gets its own copy (no bleed).
            st.Mats = new List<MatCapture>();
            foreach (var ps in st.Particles)
            {
                if (ps == null) continue;
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (r == null) continue;
                var mat = r.material; // instantiates a per-renderer copy
                if (mat == null) continue;

                var props = new List<int>();
                var isEmis = new List<bool>();
                var orig = new List<Color>();
                foreach (var name in TintPropNames)
                {
                    int id = Shader.PropertyToID(name);
                    if (!mat.HasProperty(id)) continue;
                    bool emission = name == "_EmissionColor";
                    var c = mat.GetColor(id);
                    // Skip an emission channel that's effectively off (would tint to black).
                    if (emission && c.maxColorComponent < 0.01f) continue;
                    props.Add(id);
                    isEmis.Add(emission);
                    orig.Add(c);
                }
                if (props.Count > 0)
                {
                    st.Mats.Add(new MatCapture
                    {
                        Mat = mat,
                        Props = props.ToArray(),
                        IsEmission = isEmis.ToArray(),
                        Orig = orig.ToArray(),
                    });
                }
            }

            st.Lights = portal.GetComponentsInChildren<Light>(includeInactive: true);
            st.OrigLightColors = new Color[st.Lights.Length];
            for (int i = 0; i < st.Lights.Length; i++)
            {
                if (st.Lights[i] != null) st.OrigLightColors[i] = st.Lights[i].color;
            }

            st.FxCaptured = true;
        }

        /// <summary>
        /// Recolor every particle system + light to the mode hue. A particle's color
        /// can come from its <c>startColor</c>, its Color-over-Lifetime gradient, or
        /// its material's tint/emission property, so all three are handled. (The
        /// gradient-mapped `blue flames` system bakes its orange into a texture with
        /// no color input, so it stays orange — recoloring it was dropped.)
        /// </summary>
        private static void TintFx(VState st, Color hue)
        {
            if (st.Particles != null)
            {
                foreach (var ps in st.Particles)
                {
                    if (ps == null) continue;
                    var main = ps.main;
                    main.startColor = new ParticleSystem.MinMaxGradient(hue);

                    var col = ps.colorOverLifetime;
                    if (col.enabled)
                    {
                        col.color = WhitenKeepAlpha(col.color);
                    }
                }
            }
            if (st.Mats != null)
            {
                foreach (var mc in st.Mats)
                {
                    if (mc.Mat == null) continue;
                    for (int i = 0; i < mc.Props.Length; i++)
                    {
                        Color c;
                        if (mc.IsEmission[i])
                        {
                            // Keep the original glow intensity, swap the hue.
                            c = hue * Mathf.Max(mc.Orig[i].maxColorComponent, 1f);
                        }
                        else
                        {
                            c = new Color(hue.r, hue.g, hue.b, mc.Orig[i].a);
                        }
                        mc.Mat.SetColor(mc.Props[i], c);
                    }
                }
            }
            if (st.Lights != null)
            {
                foreach (var l in st.Lights)
                {
                    if (l != null) l.color = hue;
                }
            }
        }

        /// <summary>Put the vanilla FX colors back (portal flipped back to vanilla).</summary>
        private static void RestoreFx(VState st)
        {
            if (!st.FxCaptured) return;
            if (st.Particles != null && st.OrigStartColors != null)
            {
                int n = Mathf.Min(st.Particles.Length, st.OrigStartColors.Length);
                for (int i = 0; i < n; i++)
                {
                    var ps = st.Particles[i];
                    if (ps == null) continue;
                    var main = ps.main;
                    main.startColor = st.OrigStartColors[i];
                    if (st.OrigColorOverLife != null && st.OrigColorOverLife[i])
                    {
                        var col = ps.colorOverLifetime;
                        col.color = st.OrigColorOverLifeColors[i];
                    }
                }
            }
            if (st.Mats != null)
            {
                foreach (var mc in st.Mats)
                {
                    if (mc.Mat == null) continue;
                    for (int i = 0; i < mc.Props.Length; i++)
                    {
                        mc.Mat.SetColor(mc.Props[i], mc.Orig[i]);
                    }
                }
            }
            if (st.Lights != null && st.OrigLightColors != null)
            {
                int n = Mathf.Min(st.Lights.Length, st.OrigLightColors.Length);
                for (int i = 0; i < n; i++)
                {
                    if (st.Lights[i] != null) st.Lights[i].color = st.OrigLightColors[i];
                }
            }
        }

        /// <summary>
        /// Return a copy of a Color-over-Lifetime gradient with its RGB forced to
        /// white but its alpha curve preserved, so it stops injecting the original
        /// (orange) color while keeping the flames' fade-out shape.
        /// </summary>
        private static ParticleSystem.MinMaxGradient WhitenKeepAlpha(ParticleSystem.MinMaxGradient src)
        {
            switch (src.mode)
            {
                case ParticleSystemGradientMode.Color:
                {
                    var c = src.color; c.r = c.g = c.b = 1f;
                    return new ParticleSystem.MinMaxGradient(c);
                }
                case ParticleSystemGradientMode.TwoColors:
                {
                    var a = src.colorMin; a.r = a.g = a.b = 1f;
                    var b = src.colorMax; b.r = b.g = b.b = 1f;
                    return new ParticleSystem.MinMaxGradient(a, b);
                }
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Whiten(src.gradientMin), Whiten(src.gradientMax));
                default: // Gradient / RandomColor — both expose .gradient
                    return new ParticleSystem.MinMaxGradient(Whiten(src.gradient));
            }
        }

        /// <summary>Clone a gradient with white color keys but the original alpha keys.</summary>
        private static Gradient Whiten(Gradient src)
        {
            var g = new Gradient();
            var colorKeys = new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f),
            };
            g.SetKeys(colorKeys, src.alphaKeys);
            g.mode = src.mode;
            return g;
        }

        /// <summary>
        /// Per-frame: for a mode we manage, override the emission color vanilla
        /// just set (vanilla lerps ours toward "off" since m_hadTarget is false).
        /// </summary>
        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        private static void Update_Postfix(TeleportWorld __instance)
        {
            if (!States.TryGetValue(__instance, out var st) || !st.Managed) return;
            var model = __instance.m_model;
            if (model == null) return;

            st.Alpha = Mathf.MoveTowards(st.Alpha, st.Active ? 1f : 0f, Time.deltaTime);
            var color = Color.Lerp(__instance.m_colorUnconnected, st.Hue * EmissionBoost, st.Alpha);
            model.material.SetColor("_EmissionColor", color);
        }

        // ---- "connected / active" tests, one per custom mode ----

        /// <summary>
        /// An inter-server portal glows when it can actually take you somewhere:
        /// a configured local-world destination, or — standing in a local world —
        /// the return trip back to the origin server.
        /// </summary>
        private static bool IsInterServerActive(ZNetView nview)
        {
            if (PortalData.GetDestinations(nview).Count > 0) return true;
            return InLocalWorld() && ReturnAvailableCached();
        }

        /// <summary>
        /// A network portal glows when it's part of a real mesh — i.e. the same
        /// builder owns at least one OTHER network portal to hop to. A lone
        /// network portal sits dark like an unpaired vanilla one.
        /// </summary>
        private static bool IsNetworkActive(ZNetView nview)
        {
            var zdo = nview.GetZDO();
            if (zdo == null) return false;
            long creator = zdo.GetLong(ZDOVars.s_creator, 0L);
            if (creator == 0L) return false;

            RefreshNetworkCounts();
            // Count includes this portal, so ">= 2" means at least one sibling.
            return _networkCounts.TryGetValue(creator, out int n) && n >= 2;
        }

        // Drop entries whose portal was destroyed (e.g. a world switch tears down
        // every portal but leaves its InvokeRepeating-cancelled state behind).
        private static readonly List<TeleportWorld> _dead = new List<TeleportWorld>();
        private static float _pruneAt = -999f;
        private static void PruneDead()
        {
            if (Time.time - _pruneAt < 5f) return;
            _pruneAt = Time.time;
            _dead.Clear();
            foreach (var key in States.Keys)
            {
                if (key == null) _dead.Add(key); // Unity "== null" ⇒ destroyed
            }
            foreach (var key in _dead) States.Remove(key);
        }

        // ---- shared, cached lookups (kept off the per-frame path) ----

        private static bool InLocalWorld() =>
            ZNet.instance != null && ZNet.instance.IsServer() && !ZNet.instance.IsDedicated();

        // ReturnRegistry.HasOrigin reads a file; cache it briefly so the glow tick
        // doesn't hammer the disk.
        private static bool _returnCache;
        private static float _returnCacheAt = -999f;
        private static bool ReturnAvailableCached()
        {
            if (Time.time - _returnCacheAt > 3f)
            {
                _returnCache = ReturnRegistry.HasOrigin;
                _returnCacheAt = Time.time;
            }
            return _returnCache;
        }

        // Network membership = count of network-mode portals per builder. Rebuilt
        // at most ~1/s from the authoritative ZDO portal set (same source vanilla
        // pairs on) so per-portal checks are an O(1) dictionary hit.
        private static readonly Dictionary<long, int> _networkCounts = new Dictionary<long, int>();
        private static float _netCountAt = -999f;
        private static void RefreshNetworkCounts()
        {
            if (Time.time - _netCountAt < 1f) return;
            _netCountAt = Time.time;
            _networkCounts.Clear();
            if (ZDOMan.instance == null) return;

            foreach (var zdo in ZDOMan.instance.GetPortals())
            {
                if (zdo == null || !zdo.IsValid()) continue;
                if (zdo.GetInt(EnabledHash, 0) != 0) continue;                       // inter-server, not network
                if (zdo.GetInt(LinkHash, PortalData.LinkTag) != PortalData.LinkNetwork) continue;
                long creator = zdo.GetLong(ZDOVars.s_creator, 0L);
                if (creator == 0L) continue;
                _networkCounts.TryGetValue(creator, out int c);
                _networkCounts[creator] = c + 1;
            }
        }
    }
}
