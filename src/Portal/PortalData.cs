using System.Collections.Generic;
using InterServerPortal.Core;
using InterServerPortal.Security;
using UnityEngine;

namespace InterServerPortal.Portal
{
    /// <summary>
    /// Read/write the custom per-portal ZDO fields that mark a vanilla portal as
    /// an inter-server portal. Living in the ZDO means the flag is networked to
    /// every client and persisted with the world. See docs/Data-Model-ZDO.md.
    ///
    /// Phase 2 only uses <c>ISP.enabled</c> (the inter-server flag). Later phases
    /// add the code/group/dests/policy fields documented alongside it.
    /// </summary>
    internal static class PortalData
    {
        // Hash the string keys once. Prefixed "ISP." to avoid collisions with
        // vanilla ZDO vars and other mods (Data-Model-ZDO.md → "Naming").
        private static readonly int EnabledHash = "ISP.enabled".GetStableHashCode();
        private static readonly int DestsHash = "ISP.dests".GetStableHashCode();
        private static readonly int CodeHashHash = "ISP.codehash".GetStableHashCode();
        private static readonly int CodeSaltHash = "ISP.codesalt".GetStableHashCode();
        private static readonly int LinkHash = "ISP.link".GetStableHashCode();

        /// <summary>True when this portal is flagged as an inter-server portal.</summary>
        internal static bool IsInterServer(ZNetView nview)
        {
            if (nview == null || !nview.IsValid()) return false;
            var zdo = nview.GetZDO();
            return zdo != null && zdo.GetInt(EnabledHash, 0) != 0;
        }

        internal static bool IsInterServer(TeleportWorld portal)
        {
            return portal != null && IsInterServer(portal.m_nview);
        }

        /// <summary>
        /// Flip the inter-server flag and return the new state. Claims ownership
        /// of the ZDO first so the change replicates to other clients and is
        /// saved with the world (only the owner may mutate a ZDO).
        /// </summary>
        internal static bool ToggleInterServer(ZNetView nview)
        {
            bool next = !IsInterServer(nview);
            SetInterServer(nview, next);
            return next;
        }

        internal static void SetInterServer(ZNetView nview, bool enabled)
        {
            if (nview == null || !nview.IsValid()) return;
            nview.ClaimOwnership();
            nview.GetZDO().Set(EnabledHash, enabled ? 1 : 0);
        }

        // ---- Destination list (Phase 4 hub routing) ----

        /// <summary>The portal's configured hub destinations (empty if none/invalid).</summary>
        internal static List<Destination> GetDestinations(ZNetView nview)
        {
            if (nview == null || !nview.IsValid()) return new List<Destination>();
            var zdo = nview.GetZDO();
            return Destination.Deserialize(zdo != null ? zdo.GetString(DestsHash, "") : "");
        }

        /// <summary>Persist the portal's destination list (claims ownership so it networks + saves).</summary>
        internal static void SetDestinations(ZNetView nview, List<Destination> dests)
        {
            if (nview == null || !nview.IsValid()) return;
            nview.ClaimOwnership();
            nview.GetZDO().Set(DestsHash, Destination.Serialize(dests));
        }

        // ---- Entry code / lock (Phase 5) — raw code never stored, only salt+hash ----

        /// <summary>True when this portal has an entry code set.</summary>
        internal static bool IsLocked(ZNetView nview)
        {
            if (nview == null || !nview.IsValid()) return false;
            var zdo = nview.GetZDO();
            return zdo != null && !string.IsNullOrEmpty(zdo.GetString(CodeHashHash, ""));
        }

        /// <summary>Set (non-empty) or clear (null/empty) the entry code. Generates a fresh salt on set.</summary>
        internal static void SetCode(ZNetView nview, string code)
        {
            if (nview == null || !nview.IsValid()) return;
            nview.ClaimOwnership();
            var zdo = nview.GetZDO();
            if (string.IsNullOrEmpty(code))
            {
                zdo.Set(CodeHashHash, "");
                zdo.Set(CodeSaltHash, "");
                return;
            }
            var salt = LockCodes.NewSalt();
            zdo.Set(CodeSaltHash, salt);
            zdo.Set(CodeHashHash, LockCodes.Hash(salt, code));
        }

        /// <summary>Verify an entered code against the stored salt+hash (true if the portal is unlocked).</summary>
        internal static bool CheckCode(ZNetView nview, string entered)
        {
            if (nview == null || !nview.IsValid()) return true;
            var zdo = nview.GetZDO();
            if (zdo == null) return true;
            var storedHash = zdo.GetString(CodeHashHash, "");
            if (string.IsNullOrEmpty(storedHash)) return true; // no lock
            return LockCodes.Verify(zdo.GetString(CodeSaltHash, ""), storedHash, entered);
        }

        // ---- Same-world link mode (Phase 8) — vanilla tag pair vs. personal network ----

        internal const int LinkTag = 0;      // vanilla 1:1 tag pairing (default)
        internal const int LinkNetwork = 1;  // joins the builder's same-world portal mesh

        /// <summary>
        /// This portal's same-world behavior: 0 = vanilla tag pairing, 1 = network
        /// (meshes with every other network-mode portal the same player built).
        /// Independent of the inter-server flag, which takes precedence when set.
        /// </summary>
        internal static int GetLinkMode(ZNetView nview)
        {
            if (nview == null || !nview.IsValid()) return LinkTag;
            var zdo = nview.GetZDO();
            return zdo != null ? zdo.GetInt(LinkHash, LinkTag) : LinkTag;
        }

        /// <summary>True when this portal is a same-world network portal (and not inter-server).</summary>
        internal static bool IsNetwork(ZNetView nview)
        {
            return !IsInterServer(nview) && GetLinkMode(nview) == LinkNetwork;
        }

        /// <summary>Persist the same-world link mode (claims ownership so it networks + saves).</summary>
        internal static void SetLinkMode(ZNetView nview, int mode)
        {
            if (nview == null || !nview.IsValid()) return;
            nview.ClaimOwnership();
            nview.GetZDO().Set(LinkHash, mode);
        }

        // ---- Live portal registry + arrival re-trigger guard ----

        /// <summary>Every live TeleportWorld this session, so we can measure how
        /// close the player is to one after arriving (registered from Awake).</summary>
        private static readonly HashSet<TeleportWorld> Portals = new HashSet<TeleportWorld>();

        /// <summary>How far (metres) the player must be from any inter-server
        /// portal before switching re-arms. Larger than the teleport trigger so
        /// re-arming means the player has genuinely stepped out of it.</summary>
        private const float ReArmClearDistance = 3f;

        internal static void Register(TeleportWorld portal)
        {
            if (portal != null) Portals.Add(portal);
        }

        private static bool _teardownSeen;   // player went null since the switch committed
        private static bool _prevAwaiting;

        /// <summary>
        /// Arm/disarm tick — call EVERY frame (handles a null player itself).
        /// While a switch is in flight (<see cref="WorldSwitcher.AwaitingArrival"/>)
        /// it refuses to re-arm until the old player has torn down (gone null) and
        /// a NEW player has spawned in the destination world — otherwise the
        /// teardown window (portals destroyed, old player lingering) looks "clear"
        /// and re-arms mid-flight, causing an arrival bounce. Once arrived it
        /// re-arms normally the moment the player is clear of every inter-server
        /// portal. See WorldSwitcher.PortalArmed / AwaitingArrival.
        /// </summary>
        internal static void UpdateArming()
        {
            var player = Player.m_localPlayer;

            // Detect the rising edge of a committed switch and reset teardown state.
            if (WorldSwitcher.AwaitingArrival && !_prevAwaiting) _teardownSeen = false;
            _prevAwaiting = WorldSwitcher.AwaitingArrival;

            if (WorldSwitcher.AwaitingArrival)
            {
                if (player == null) { _teardownSeen = true; return; } // torn down / loading
                if (!_teardownSeen) return;                            // still the old player
                WorldSwitcher.AwaitingArrival = false;                 // new player has arrived
                // fall through — stay disarmed until the player steps clear
            }

            if (WorldSwitcher.PortalArmed) return;
            if (player == null) return;
            if (!AnyInterServerPortalWithin(player.transform.position, ReArmClearDistance))
            {
                WorldSwitcher.PortalArmed = true;
                Plugin.Debug("Portal switch re-armed — player clear of inter-server portals.");
            }
        }

        private static bool AnyInterServerPortalWithin(Vector3 pos, float range)
        {
            Portals.RemoveWhere(p => p == null); // drop portals from unloaded worlds
            float sq = range * range;
            foreach (var portal in Portals)
            {
                if (!IsInterServer(portal.m_nview)) continue;
                if ((portal.transform.position - pos).sqrMagnitude <= sq) return true;
            }
            return false;
        }
    }
}
