# Feature #1 — Lock Codes / Entry Gate

Optional per-portal code that must be entered before the portal will switch you.

> **Status (Phase 5, done — confirmed in-game):** implemented per the design
> below. `src/Security/LockCodes.cs` does salt + SHA-256(salt+code);
> `PortalData` stores it in `ISP.codehash` / `ISP.codesalt` (raw code never
> stored). The config panel sets/clears the code (masked field, applied on Save);
> `src/Hub/CodePrompt.cs` prompts before travel; `src/Security/LockGate.cs`
> throttles wrong attempts (escalating cooldown) and remembers a correct code for
> 5 min. Codes are **per-portal** (per-`ISP.group` is still a future option).

## Purpose

Let a portal owner/server admin gate who may use an inter-server portal. Purely
an access check in front of the world switch — it does not change the switch
itself.

## Storage

Never store the raw code. Per [Data-Model-ZDO](Data-Model-ZDO.md):
- `ISP.codesalt` — per-portal random salt, generated when a code is first set.
- `ISP.codehash` — hash of `salt + code`. Empty/absent = no lock.

Use a standard hash (e.g. SHA-256) over `salt + code`. This is anti-casual-peek,
not high-security — the ZDO is readable by anyone who can read the world save, so
the salt+hash only stops trivially reading the code off the portal.

## Setting a code

In the portal config UI (owner side):
- Field to set/change the code → generates salt, stores hash.
- Clear button → removes hash + salt (unlocks).

## Entering

On interacting with a locked portal:
1. Prompt for the code.
2. Hash the entry with the stored salt; compare to `ISP.codehash`.
3. Match → proceed to hub menu / switch. Mismatch → deny with a message; apply a
   short cooldown / attempt throttle to discourage brute forcing.

## Notes

- Codes are per-portal (or per-group if you tie them to `ISP.group` — decide in
  implementation; per-portal is simpler for v0.1).
- Keep the prompt UI consistent with the hub menu styling.
- Locking is independent of item policy and routing; it simply runs first in the
  interaction flow (see [Portal-System](Portal-System.md)).
