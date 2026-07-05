# Feature #2 — Failure Handling & "Offline" Notification

Confirmed scope: detect **any** reason the local seed can't be entered and never
strand the player.

> **Status (Phase 3, done):** pre-flight validation + mid-flight fallback
> implemented. `src/Core/DestinationValidator.cs` checks the save on disk;
> `WorldSwitcher.RequestSwitchToLocal` blocks with a centered message while still
> on the server; `WorldSwitcher.FallbackToOrigin` reconnects to the origin if a
> load fails after teardown. **Confirmed in-game (Tests F–G):** a missing/renamed
> world is blocked and the player stays on the server; a valid world still
> switches. Not yet exercised in-game: the corrupt/missing-data/version reason
> strings and the mid-flight fallback reconnect (hard to stage by hand). The
> **greyed per-destination hub states** below arrive with the hub menu in Phase 4
> (no menu exists yet).

## What "offline" means here

The local seed is a single-player world *file* on the player's own machine — it
isn't a running host, so "offline" really means **"cannot be entered right now."**
Causes to detect:

- World save file missing (deleted/renamed/moved out of `worlds_local`).
- Corrupt or unreadable save.
- Version mismatch (save from an incompatible game/world-gen version).
- Any exception thrown during the switch/load.

## Detect before leaving the server

Validate the destination **while still safely connected** (step 2 of the core
sequence). If validation fails, show the notification and abort the switch — the
player stays on the server, no harm done.

Checks:
1. Enumerate `worlds_local` via `SaveSystem`; confirm the target `worldName`
   exists.
2. Confirm the save is readable / metadata loads.
3. Surface version/compatibility info if available.

## Notification UI

- In the hub menu, render unavailable destinations greyed with a reason tag
  ("missing", "corrupt", "version"). Don't remove them silently.
- On a blocked attempt, a clear centered message: e.g. *"Farm Seed is
  unavailable: world file not found."*

## Fallback if a switch fails mid-flight

Despite pre-validation, a load can still throw after teardown has begun. The
mod must recover, not crash:

1. Wrap the load in try/catch.
2. On failure, attempt to **reconnect to the origin server** from
   [ReturnRegistry](Core-Mechanic-World-Switching.md#return-registry).
3. If reconnect also fails, drop cleanly to the main menu with an explanatory
   message rather than a hung/black state.

The ReturnRegistry being persisted to disk is what makes step 2 possible even
after a crash/restart.

## Return trip failures

Same philosophy in reverse: if reconnecting to the server fails, keep the player
in the local world and notify, rather than tearing it down into nothing.
