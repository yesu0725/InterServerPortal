# Portal Visuals — per-mode glow

Give each portal mode a distinct colour so you can tell at a glance what a portal
does, and — like a vanilla portal — whether it's actually wired up. Implemented in
[`src/Portal/PortalVisuals.cs`](../src/Portal/PortalVisuals.cs) as Harmony
postfixes on `TeleportWorld`.

## Colours & active state

| Mode | Hue | Shows **active** (glowing) when… | Shows **inactive** (dark) when… |
|---|---|---|---|
| Vanilla tag-pair | native blue (untouched) | a 1:1 tag partner is found | no tag partner |
| Same-world network | violet `#c4b5fd` | the builder's mesh has ≥1 **other** network portal to hop to | it's a lone network portal |
| Inter-server | sky/cyan `#7dd3fc` | it has a usable travel option — a configured local-world destination, or (on a local world) a "Return to origin server" path | no destination / no return path |

"Off" looks the same across all modes: the portal sits at the vanilla
`m_colorUnconnected` (dark) emission, exactly like an unpaired vanilla portal.

## How it hooks vanilla

From the decompiled `TeleportWorld`:

- `Update()` (per-frame) lerps the frame model's `_EmissionColor` between
  `m_colorUnconnected` (dark) and `m_colorTargetfound` (glow) by `m_colorAlpha`,
  which vanilla drives from `m_hadTarget` — **true only for a tag connection**.
- `UpdatePortal()` (a 0.5s `InvokeRepeating`) shows the swirl vortex
  (`m_target_found`, an `EffectFade`) only when `TargetFound()` — again, a tag
  connection.

Because network/inter-server portals have **no** tag connection, vanilla keeps
them dark and vortex-less. So we mirror vanilla's own split:

- **`UpdatePortal` postfix** — recompute the mode + on/off state (cheap, 0.5s),
  cache it per portal, and drive the vortex on/off using the same "player near +
  can teleport" proximity gate vanilla uses. Also (once) retint the FX.
- **`Update` postfix** — for a managed portal, override `_EmissionColor` with the
  mode hue (`hue * EmissionBoost`) lerped by our own alpha, so the glow fades
  in/out identically to vanilla.

Vanilla portals are handed straight back to vanilla (we `States.Remove` them and
never touch their colour).

## FX retint (particles + lights)

The whole portal effect is retinted to the mode hue. A particle's colour can come
from several places, so all the tractable ones are handled, captured per-instance
(so nothing bleeds onto vanilla portals) and restored if the portal is flipped
back to vanilla:

- `ParticleSystem.main.startColor`
- the **Color-over-Lifetime** gradient (RGB whitened, alpha fade preserved)
- the particle **renderer material** colour props on an instanced copy:
  `_TintColor`, `_Color`, `_BaseColor`, `_EmissionColor` (emission keeps its
  intensity)
- every `Light` under the portal

### Known limitation — the `blue flames` burst stays orange

The outward radiating flame burst is a particle system named **`blue flames`**
using the shader **`Custom/Gradient Mapped Particle (Unlit)`**. That material
exposes **no colour property** — its colour is baked into `_MainTex`
(`wildfire06`) and the shader renders the texture's RGB directly, ignoring
particle/vertex colour. None of the paths above can touch it.

A texture-recolour approach (GPU-blit the texture, desaturate to brightness ×
mode hue, keep alpha, swap into the instanced material) was implemented and
**intentionally dropped** as too invasive. The flame is therefore **orange in all
modes** by design; the frame glow, inner vortex, sparks and lights carry the mode
colour.

### Known limitation — the frame **runes** keep their vanilla colour

The glowing rune/glyph characters carved into the portal frame **cannot be
recoloured** to the mode hue, for the same class of reason as the flame burst.

A runtime material dump (v0.9.1) confirmed the runes are not a separate tintable
element: on `portal_wood` the frame material `portal_small` (shader `Standard`,
`_EMISSION` keyword) draws them from its **emission texture** `_EmissionMap =
portal_small_e`, and the only colour input is `_EmissionColor` — which the frame
glow already drives. Multiplying a texture whose rune pixels already carry the
colour can't shift the hue (a red texel × any tint stays in the red channel), so
setting `_EmissionColor` to the mode hue can dim/brighten the runes but never
change their colour. `portal_stone` behaves the same way.

An extra pass that captured every `MeshRenderer` emissive material and folded it
into the retint list was tried (v0.9.1) and **reverted** — it found no separately
tintable rune material (`emissive rune mats=0`), confirming the colour lives in
the baked texture. Recolouring would require rewriting the emission texture
per-portal, the same invasive route rejected for the flame burst.

> Note: a "plain" portal that appears to have bright red runes is usually a
> **tag-paired vanilla portal** showing vanilla's own `m_colorTargetfound`
> connected glow — a mode the mod deliberately leaves untouched.

> Diagnosing shaders: a one-time dump of each portal particle system's shader and
> full material property list was used to find the above; it was removed once the
> gradient-mapped shader was identified. Re-add a temporary dump behind
> `DebugLogging` if a future game update changes the portal FX.

## Performance notes

- Network membership is a per-builder count rebuilt at most ~1/s from
  `ZDOMan.GetPortals()` (viewer-independent: "is this portal part of a ≥2-portal
  mesh by its creator"), so per-portal checks are an O(1) dictionary hit.
- The return-path availability (`ReturnRegistry.HasOrigin`, a file read) is cached
  ~3s.
- Per-portal state is pruned when a portal turns vanilla or is destroyed (e.g. a
  world switch).
