# Feature #4 — Hub Routing (Multiple Seeds & Arrival Points)

Confirmed scope: one main portal can route to **multiple destination worlds
AND multiple arrival points** — a hub with a selection menu.

> **Status (Phase 4, done — confirmed in-game):** multi-**world** routing is
> implemented and tested (Tests J–N). Destinations are `(label, worldName)` pairs
> stored per-portal in the `ISP.dests` ZDO field (`src/Portal/Destination.cs`). A
> native Jötunn config panel (alt-use) edits the flag + destination list; a travel
> menu (plain Use / walk-through) picks a destination, with a single-destination
> fast path and greyed unavailable worlds (`src/Hub/*`). Local→local hops and a
> "Return to origin server" entry work. **Deferred:** named **arrival points**
> (`spawnPointId` — world spawn only for now) and **`ISP.group` cross-portal
> grouping** (destinations are per-portal, not yet shared across a group). The
> arrival-point + grouping sections below describe the eventual design.

## Why this doesn't hit the vanilla 1:1 limit

Vanilla portals pair exactly two portals by tag. Inter-server portals do **not**
use vanilla teleport, so that limit does not apply. We implement our own routing
table and destination-selection UI.

## Data model

A destination is `(label, worldName, spawnPointId)`:
- `label` — menu text ("Farm Seed", "Boss Testing", "Creative Flat").
- `worldName` — a local world save in `worlds_local`.
- `spawnPointId` — where to arrive inside that world (a named return/arrival
  portal, or `default` = world spawn).

Stored per the [Data-Model-ZDO](Data-Model-ZDO.md) `ISP.dests` field, or in a
shared client registry keyed by `ISP.group` when many portals share a set.

## Grouping ("connect multiple portals the player built")

Portals sharing an `ISP.group` id are aware of each other. A "main" portal can
therefore surface every destination registered under its group. Building another
portal and assigning it the same group adds it to the hub without editing the
main portal.

## Interaction UI

On interacting with a hub portal:
1. If exactly one destination → skip the menu, go straight to it.
2. If multiple → show a selection panel listing destinations by label, grouped
   by world then arrival point. Reuse Valheim/Jötunn UI primitives for a native
   look.
3. Selected destination feeds validation + the world switch
   ([Core-Mechanic-World-Switching](Core-Mechanic-World-Switching.md)).

## Arrival points

`spawnPointId` resolves inside the destination world after load:
- `default` → the world's normal spawn.
- named id → a return/arrival portal placed by the mod. If the named point is
  missing, fall back to default and notify.

## Edge cases

- A destination world no longer exists → show it greyed/offline
  ([Feature-Failure-Handling](Feature-Failure-Handling.md)), don't hard-fail the
  whole menu.
- Duplicate labels → disambiguate with world name.
- Empty destination list on a flagged portal → prompt the owner to configure one.
