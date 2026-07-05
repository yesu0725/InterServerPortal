# Portal Modes & Colours

Every portal is in one of **three modes**. The mode decides what happens when you
walk through, and each mode has a **distinct glow colour** so you can tell at a
glance what a portal does — and whether it's actually wired up.

## The three modes

| Mode | Walk-through does… | Set it via |
|---|---|---|
| **Vanilla tag-pair** | Normal 1:1 teleport to the portal with the same tag | Default (use `E` to set a tag) |
| **Same-world Network** | Opens a menu of your other network portals, teleports in-world | Config panel → **Link mode: Network** |
| **Inter-server** | Switches you to your own local world | Config panel → **inter-server** flag |

Precedence: if a portal is flagged **both** inter-server and network, **inter-server
wins**.

## Colours

| Mode | Glow colour |
|---|---|
| Vanilla tag-pair | **Blue** (the normal Valheim portal colour) |
| Network | **Violet** |
| Inter-server | **Cyan** |

The colour applies to the portal's frame glow and its inner swirl vortex.

> **Note:** the outward radiating **flame burst stays orange in every mode** — its
> shader bakes that colour into a texture and can't be recoloured. The frame glow,
> inner vortex, sparks, and light carry the mode colour.

## Active vs. inactive (dark)

Just like a vanilla portal only glows when it has a partner, a modded portal only
**lights up when it's actually usable**. An unwired portal sits dark.

| Mode | Glows (active) when… | Sits dark (inactive) when… |
|---|---|---|
| Vanilla | it has a 1:1 tag partner | no partner |
| Network | your mesh has **another** network portal to hop to | it's the only network portal you own |
| Inter-server | it has a usable travel option — a configured destination, or (in a local world) a **Return to origin server** path | no destination / no return path |

So a lone network portal, or an inter-server portal with no destinations, will be
its colour but **dark** — a quick visual reminder that there's nothing to travel to
yet.

## Hover text

Looking at a portal shows its status in the hover tooltip:

- `InterServerPortal: ON` (cyan) — inter-server
- `InterServerPortal: NETWORK` (violet) — network
- `InterServerPortal: off` (grey) — vanilla
- `[locked]` — the portal has an entry code (see **[Entry Codes & Locks](Entry-Codes-and-Locks)**)

…plus a reminder of the use hints (walk through to travel, **L.Shift + Use** to
configure).

See also: **[Inter-Server Travel](Inter-Server-Travel)** ·
**[Same-World Networks](Same-World-Networks)**.
