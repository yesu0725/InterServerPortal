# Feature — Same-world Portal Networks

A second *normal* (same-world) way to use portals, alongside vanilla 1:1 tag
pairing: a **network** portal meshes with every other network-mode portal the same
player built, so you can hop from any one to any other.

> **Status (Phase 8, done — confirmed in-game Tests V–Z):** implemented.
> Per-portal opt-in (`ISP.link` = network). Membership is by the vanilla piece
> `creator` — "all my portals". Enumerates `ZDOMan.GetPortals()` (whole map),
> locally when we host the world and via a routed RPC to the server otherwise, so
> it works on dedicated servers too (the RPC path was verified to return a portal in
> an unloaded zone). Travel is a vanilla in-world teleport (no loading screen).

## Two same-world modes

Set per portal in the config panel (**Link mode**):

| Mode | Behavior |
|---|---|
| **Tag pair** (default) | Untouched vanilla 1:1 pairing by tag. |
| **Network** | Joins the builder's mesh. Walking through lists every *other* network-mode portal that player built and teleports to the chosen one. |

The inter-server flag is independent and takes **precedence**: a portal that is both
inter-server and network behaves as inter-server. A network portal's vanilla **tag
doubles as its display name** in the travel menu (untagged → "unnamed portal").

## How it works

1. Walk through a network portal → entry-code gate (if locked) → item restriction
   (fixed by prefab: wood blocks ore, stone allows — see
   [Feature-Item-Policy](Feature-Item-Policy.md)).
2. Fetch the player's network portals:
   - **World host** (single-player / listen server): enumerate
     `ZDOMan.instance.GetPortals()` directly — the full set is local.
   - **Remote client** (dedicated server): a routed RPC (`ISP_ReqNet` →
     `ISP_RespNet`) asks the server, which enumerates and replies with each
     portal's ZDOID, position, rotation, and tag. This avoids missing portals in
     zones the client hasn't loaded.
3. Show the selection menu (nearest first), then teleport with vanilla's own exit
   placement — `targetPos + targetForward * m_exitDistance + up`, then
   `Player.TeleportTo(pos, rot, distantTeleport: true)`. No world switch, one fade,
   the destination zone streams in if distant.

## Membership

"All my portals" = network-mode portals whose ZDO `creator` (vanilla
`ZDOVars.s_creator`, set at build time to `Player.GetPlayerID()`) matches the
traveller. No new ownership field — each player automatically has their own private
mesh, even on a shared server.

## Data model

- `ISP.link` — int: `0` = tag pair (vanilla, default), `1` = network. See
  [Data-Model-ZDO](Data-Model-ZDO.md).

## Arrival guard

You arrive standing on the destination portal, so its trigger fires once on the
spawn-overlap. A short cooldown (`NetworkController.ArrivalSuppressed`, ~2 s) after
any network teleport swallows that one trigger so the menu doesn't immediately
re-open; because `OnTriggerEnter` only fires once per physical entry, standing still
never loops. (Same problem as the inter-server arrival bounce, but simpler since
there's no world teardown — see [Portal-System](Portal-System.md).)

## Notes

- Reuses the existing lock-code gate and hover/config UI.
- A named/shared network (`ISP.group`) so multiple players or multiple meshes can
  coexist remains a possible future extension; today the mesh is per-builder.
