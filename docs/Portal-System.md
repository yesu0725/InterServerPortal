# Portal System

How we hook the vanilla portals and keep them dual-purpose.

## Prefabs

- `portal_wood` — the standard portal. Blocks teleport-restricted items
  (ores/metals) in vanilla.
- `portal_stone` — the Ashlands stone portal. Allows restricted items through
  in vanilla. Same custom behavior applies; the item policy differs
  (see [Feature-Item-Policy](Feature-Item-Policy.md)).

Both use the `TeleportWorld` component.

## Dual-purpose requirement (Feature #3)

The same physical piece must still work as a normal portal. We therefore do
**not** replace `TeleportWorld` behavior — we branch, in precedence order:

```
on teleport trigger (walk-through):
    if PortalData.IsInterServer(znetview):
        inter-server flow  (code check → hub menu → world switch)
    elif PortalData.IsNetwork(znetview):
        same-world network flow  (code check → item check → portal menu → in-world teleport)
    else:
        fall through to vanilla TeleportWorld behavior (tag pairing, teleport)
```

`ISP.enabled` (inter-server) wins over `ISP.link` (same-world network); a portal
that is both behaves as inter-server. See
[Feature-Portal-Network](Feature-Portal-Network.md) for the network branch.

## Flagging a portal as inter-server

Two candidate mechanisms (decide in implementation):

1. **Reserved tag keyword** — if the portal's vanilla tag equals/prefixes a
   sentinel (e.g. tag starts with `#isp:`), treat it as inter-server. Simple,
   uses existing UI, but consumes the tag field.
2. **Custom ZDO flag** — a dedicated boolean ZDO field toggled via an added
   button in the portal interaction UI. Cleaner; leaves the vanilla tag free for
   the hub's own grouping. **Preferred.**

Either way the flag lives in the ZDO so it is networked and persistent; see
[Data-Model-ZDO](Data-Model-ZDO.md).

## Interaction flow (inter-server portal)

1. Player interacts with a flagged portal.
2. If a lock code is set → prompt ([Feature-Lock-Codes](Feature-Lock-Codes.md)).
3. Show the hub destination menu if more than one destination is configured,
   else use the single destination ([Feature-Hub-Routing](Feature-Hub-Routing.md)).
4. Apply the item policy for this prefab
   ([Feature-Item-Policy](Feature-Item-Policy.md)).
5. Validate destination, then run the world switch
   ([Core-Mechanic-World-Switching](Core-Mechanic-World-Switching.md)).

## Patch surface

- `TeleportWorld.Interact` (and/or the hover/use path) — inject the branch.
- `TeleportWorld.Teleport` / the collision-based teleport trigger — ensure a
  flagged portal does not run vanilla teleport.
- The portal config UI (`TeleportWorld` text-input panel) — add our controls
  (flag toggle, code, destination management).

Confirm exact method names against the publicized assembly; they drift between
game versions.

## Arrival re-trigger (the infinite-bounce bug)

The teleport is driven by `TeleportWorldTrigger.OnTriggerEnter → TeleportWorld.Teleport(player)`.
After a switch, the player loads back in **standing inside the portal they just
used** (their logout position). Unity fires `OnTriggerEnter` once on that
spawn-overlap → the flagged portal switches again → the player lands on the
destination-world portal → switches back → an infinite bounce between the two
worlds. (Confirmed in Phase 2 testing: `Switching to local` / `Returning to
server` alternating every ~9s in the logs.)

Fix — an **arm/disarm guard** (`WorldSwitcher.PortalArmed`):

- A switch commits → **disarm** (`Leave()`) and set `AwaitingArrival`.
- The arm tick (`PortalData.UpdateArming`, run **every frame**) will not re-arm
  while `AwaitingArrival` is set until the **old player has torn down (gone null)
  and a new player has spawned** in the destination world. This is essential:
  during teardown the old world's portals are destroyed *before* the old player
  is, so the "> ~3 m from every inter-server portal" test briefly reads true and
  would re-arm mid-flight → an arrival bounce (fixed in v0.5.0).
- After arrival, each frame re-arms once the player is **> ~3 m from every
  inter-server portal** (fed by a live portal registry populated from
  `TeleportWorld.Awake`).
- A flagged portal's `Teleport` prefix ignores the trigger while disarmed.

Because `OnTriggerEnter` only fires once per physical entry, the single
spawn-overlap trigger is swallowed and standing still does not loop; using the
portal for real means stepping out and walking back in. Starts disarmed so
logging in on a portal doesn't insta-switch either.

## Keeping vanilla pairing intact

Vanilla portals pair 1:1 by tag. Because inter-server portals do not use vanilla
teleport, their tag is free for our own grouping. Non-flagged portals keep the
untouched vanilla tag-pairing behavior — no regressions.
