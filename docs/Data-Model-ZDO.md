# Data Model — ZDO Fields

All per-portal state lives in the portal's ZDO so it is networked and persisted
with the world. Client-only session state (return registry) lives on disk in the
BepInEx data folder instead — see
[Core-Mechanic-World-Switching](Core-Mechanic-World-Switching.md).

## Naming

Prefix every custom key with `ISP.` to avoid collisions with vanilla and other
mods. ZDO stores strongly-typed values by hashed key; use `GetInt/SetInt`,
`GetString/SetString`, etc.

## Portal ZDO fields

| Key | Type | Meaning |
|---|---|---|
| `ISP.enabled` | int (0/1) | Portal is an inter-server portal (Feature #3 flag). If 0/absent → vanilla behavior. |
| `ISP.codehash` | string | Salted hash of the entry code, empty = no lock (Feature #1). Never store the raw code. |
| `ISP.codesalt` | string | Per-portal random salt for the code hash. |
| `ISP.group` | string | Hub group id. Portals sharing a group present each other as destinations (Feature #4). |
| `ISP.dests` | string | Serialized destination list (see below), if destinations are stored on the portal rather than in a shared registry. |
| `ISP.link` | int | Same-world link mode: 0 = tag pair (vanilla, default), 1 = network (meshes with the builder's other network portals). See [Feature-Portal-Network](Feature-Portal-Network.md). |
| `creator` | long | *(vanilla piece field, not ours)* the builder's `GetPlayerID()`, used to scope a portal network to "all my portals". |

> **Removed:** `ISP.policy` (the item-policy override) — `portal_wood` / `portal_stone`
> now keep their fixed vanilla item restrictions with no per-portal override.

## Destination list serialization

For Feature #4 (multiple seeds + arrival points). Keep it compact and
forward-compatible — a versioned, delimited string or small JSON blob:

```
v1|<label>;<worldName>;<spawnPointId>|<label>;<worldName>;<spawnPointId>|...
```

- `label` — what the player sees in the hub menu.
- `worldName` — the local world save name in `worlds_local`.
- `spawnPointId` — arrival marker within that world (a return/arrival portal id,
  or "default" for the world spawn).

If the list grows large, move it to a client-side registry keyed by `ISP.group`
instead of stuffing the ZDO.

## Client-side (not ZDO): Return Registry

JSON in the BepInEx data folder, persisted across crashes:

```json
{
  "originAddress": "1.2.3.4:2456",
  "originContext": { "dedicated": true, "passwordRequired": true },
  "timestamp": "2026-07-03T00:00:00Z"
}
```

Never persist raw server passwords unless the user explicitly opts in.

## Versioning

Bump a `v1` marker inside serialized blobs so future schema changes can migrate
old portals rather than breaking them.
