# Feature #5 — Item Policy (portal_stone vs portal_wood)

Confirmed scope: apply all inter-server features to `portal_stone` too, and use
it to carry items the `portal_wood` restricts.

> **Status (Phase 6 done; simplified in Phase 8):** implemented in
> `src/Policy/ItemPolicy.cs`. `portal_stone` needed no special code — all patches
> hook the shared `TeleportWorld` component. The policy is **fixed by prefab and
> not user-configurable** (Phase 8 removed the `ISP.policy` override + dropdown):
> `ItemPolicy.BlocksRestricted` returns `!TeleportWorld.m_allowAllItems`, so wood
> blocks and stone allows, exactly like vanilla. Blocking reuses
> `Humanoid.IsTeleportable()` and denies with `$msg_noteleport` + the offending
> item names. The gate runs before every switch (`HubController.ProceedTravel`) and
> before same-world network teleports (`NetworkController.Proceed`).

## The key insight

Teleport restrictions (ores/metals can't teleport) are enforced by vanilla's
`IsTeleportable()` check **during a `TeleportWorld` teleport** — they do NOT gate
a world *load*. Our mechanic is a world switch, and the character/inventory is
client-side, so **by default the entire inventory (ore included) already comes
through** regardless of prefab. See [Architecture-Constraints](Architecture-Constraints.md).

Therefore the wood-vs-stone distinction is a **policy we deliberately enforce**,
not an engine limitation we work around.

## Policy by prefab

| Prefab | Default policy | Behavior |
|---|---|---|
| `portal_wood` | Block restricted | Before switching, run the vanilla teleportability check; refuse (or require dropping) restricted items, mirroring vanilla portal feel. |
| `portal_stone` | Allow all | Skip the check; everything carries through. |

## Override

`ISP.policy` ZDO field ([Data-Model-ZDO](Data-Model-ZDO.md)) can force behavior
per portal:
- `0` auto (by prefab, table above)
- `1` block restricted
- `2` allow all

Lets an admin make a wood portal permissive or a stone portal strict if desired.

## Enforcement point

In the interaction flow (see [Portal-System](Portal-System.md)), right before
the world switch:
1. Resolve effective policy (prefab default unless `ISP.policy` overrides).
2. If "block restricted", iterate the player's inventory using the same
   restriction predicate vanilla uses (reuse `Inventory` / item `m_teleportable`
   flags — confirm against the publicized assembly).
3. On a violation: deny with a message naming the offending item, matching
   vanilla's "can not be teleported" wording style. Do not silently drop items.

## Notes

- Reuse vanilla's own teleportable predicate rather than hardcoding an item list,
  so modded/added items with teleport flags behave correctly.
- Policy is independent of routing and lock codes; it runs as a gate just before
  the switch.
