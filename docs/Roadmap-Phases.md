# Roadmap & Phases

Build the risky core first. Do not build features on top of a switch that isn't
proven stable.

> **Status (2026-07-04):** Phase 0 ✅ done · Phase 1 ✅ done · Phase 2 ✅ done ·
> Phase 3 ✅ done · Phase 4 ✅ done · Phase 5 ✅ done (per-portal entry code:
> salt+SHA-256 in `ISP.codehash`/`ISP.codesalt`, set/clear in the config panel,
> masked entry prompt, escalating throttle — all confirmed in-game, plus
> arrival-bounce + E-key-tag fixes) · Phase 6 ✅ done (item policy + portal_stone:
> wood blocks ore / stone carries it, `ISP.policy` override, confirmed in-game
> Tests O–R) · Phase 7 ✅ packaging done (Thunderstore manifest + icon + README +
> changelog, `PackThunderstore` zip target; hotkeys were made opt-in then removed
> in Phase 8) · Phase 8 ✅ done (same-world portal networks; removed the item-policy
> override and the travel hotkeys — confirmed in-game Tests V–Z, incl. the
> dedicated-server RPC path).

## Phase 0 — Scaffold ✅ DONE
- [x] `.csproj` with BepInEx + Jötunn + publicizer, building the plugin.
- [x] Auto-deploy targets: Steam BepInEx, r2modman profile, dedicated server.
- [x] Plugin loads and logs on startup.
- [x] Runs on **both** `valheim.exe` and `valheim_server.exe` (two
  `[BepInProcess]` attrs) for the multi-regional dedicated-server plan.

## Phase 1 — Core switch (highest risk) ✅ DONE — GATE CLEARED
- [x] Hotkey prototype: leave server → load configured local world → arrive.
- [x] Reverse hotkey: local world → reconnect to the remembered server `IP:port`.
- [x] Persisted [ReturnRegistry](Core-Mechanic-World-Switching.md#return-registry)
  (survives crash/restart).
- [x] No-op guards (already-local / already-on-server) + reentrancy guard.
- [x] Confirmed repeatable in-game, inventory intact both ways.
- See [Core-Mechanic-World-Switching](Core-Mechanic-World-Switching.md) →
  "Findings / gotchas" for the five bugs found and fixed during testing.
- Known limits carried forward: direct `IP:port` (Steamworks) joins only;
  local→local hops blocked until Phase 4; no destination validation yet
  (Phase 3).

## Phase 2 — Portal integration (Feature #3) ✅ DONE
- [x] Custom ZDO flag (`ISP.enabled`) + alt-use toggle to mark a portal
  inter-server ([Portal-System](Portal-System.md), `src/Portal/PortalData.cs`).
- [x] Branch in `TeleportWorld.Teleport` prefix: flagged → switch flow;
  unflagged → untouched vanilla behavior (`src/Portal/PortalPatches.cs`).
- [x] Direction-aware `RequestPortalSwitch` so one portal serves both legs.
- [x] Arrival-bounce fix: arm/disarm guard stops the infinite world-to-world
  loop when the player spawns on the portal they used (Portal-System.md →
  "Arrival re-trigger").
- [x] Regression check in-game: normal tag-paired portals still work; flagged
  portals switch both legs without looping (confirmed Tests A–E).

## Phase 3 — Destination validation & failure handling (Feature #2) ✅ DONE
- [x] Enumerate/validate `worlds_local` before leaving — `DestinationValidator`
  reads `SaveSystem.GetWorldList()` + `World.m_dataError`
  (`src/Core/DestinationValidator.cs`).
- [x] Blocked-attempt message: an unavailable destination shows a centered
  reason ("world file not found" / "corrupt" / "incompatible version" / …) and
  aborts the switch while still safely on the server.
- [x] Mid-flight fallback: re-validate + try/catch in `StartLocalWorld`; on
  failure `FallbackToOrigin` reconnects to the remembered server (or drops
  cleanly to the menu if there is none).
- [x] In-game test (Tests F–G): missing/renamed world → blocked with message,
  stays on server; a valid world still switches normally.
- [ ] Not yet exercised in-game: the corrupt/missing-data/version reason strings
  (Test H) and the mid-flight `FallbackToOrigin` path (Test I) — code-complete,
  hard to stage by hand.
- Greyed per-destination "unavailable" states in the hub menu come with the hub
  UI in Phase 4 (no menu exists yet).

## Phase 4 — Hub routing (Feature #4) ✅ DONE
- [x] `(label, worldName)` destinations stored per-portal in the `ISP.dests` ZDO
  field (versioned, escaped serialization) — `src/Portal/Destination.cs` +
  `PortalData.Get/SetDestinations`.
- [x] Native Jötunn config/editor panel (alt-use): flag toggle + add/remove
  destinations, world picked from a dropdown of local saves
  (`src/Hub/PortalConfigPanel.cs`, `HubWindow.cs`).
- [x] Travel selection menu (`src/Hub/DestinationMenu.cs`) with single-destination
  fast path; unavailable destinations greyed with a reason
  (`HubController.BeginTravel`).
- [x] Local→local hops: the "already local host" guard now compares the target
  world, so switching between different seeds works (origin server preserved).
- [x] Return-to-server appears as a menu entry from within a local world.
- Deferred: named **arrival points** (`spawnPointId`) — world spawn only for now;
  and `ISP.group` cross-portal grouping (dests are per-portal). Both are follow-ups.
- [x] In-game test (Tests J–N): config panel add/remove/save, travel menu + fast
  path, greyed unavailable, local→local hop, return-to-server, empty-config
  fallback — all confirmed. Config-panel layout overlap fixed (top-down cursor).

## Phase 5 — Lock codes (Feature #1) ✅ DONE
- [x] Salt+SHA-256 hash of `salt + code` in `ISP.codehash` / `ISP.codesalt`
  (raw code never stored) — `src/Security/LockCodes.cs`, `PortalData` code fields.
- [x] Set/clear code in the config panel (masked field, applied on Save).
- [x] Masked entry prompt before travel (`src/Hub/CodePrompt.cs`); correct code
  proceeds, wrong denies with a message.
- [x] Wrong-attempt throttle + "recently unlocked" memory (per-portal by ZDOID) —
  `src/Security/LockGate.cs`; escalating cooldown, 5-min unlock window.
- [x] Hover text shows a `[locked]` indicator.
- [x] In-game test: set a code, wrong code denied + throttled, right code travels,
  clear code unlocks — confirmed. Also fixed here: an arrival-bounce regression
  (re-arm during teardown) and plain-Use (E) hijacking the vanilla tag input.
- Per-portal codes only (per-`ISP.group` codes remain a future option).

## Phase 6 — Item policy & portal_stone (Feature #5) ✅ DONE
- [x] Extend all behavior to `portal_stone` — automatic: every patch targets the
  `TeleportWorld` component (which both prefabs share), so flagging, config,
  travel, and lock codes already work on stone with no extra code.
- [x] Enforce restriction on wood, skip on stone (`src/Policy/ItemPolicy.cs`).
  The auto default reads the vanilla `TeleportWorld.m_allowAllItems` field
  (stone = true), so it mirrors the prefab exactly.
- [x] `ISP.policy` override (0 auto / 1 block / 2 allow) — `PortalData.Get/SetPolicy`,
  set from an "Item policy" dropdown in the config panel.
- [x] Reuse the vanilla teleportability predicate — `Humanoid.IsTeleportable()`
  (honours the `TeleportAll` global key); a block denies with `$msg_noteleport`
  plus the offending item names, mirroring vanilla wording.
- [x] Hover text shows `[ore blocked]` / `[all items]` for a flagged portal.
- [x] In-game test (Tests O–R): wood blocks ore, stone carries it, `ISP.policy`
  override flips each, message names the item — confirmed.

## Phase 8 — Same-world portal networks + revisions ✅ DONE
Post-1.0 feature work requested after Phase 7.
- [x] **Portal networks:** per-portal opt-in (`ISP.link` = network) that meshes with
  every network-mode portal the same player built; walk-through opens a menu of the
  others and does a vanilla in-world teleport. `src/Net/PortalNetwork.cs`,
  `src/Hub/NetworkController.cs`, `src/Hub/PortalNetworkMenu.cs`.
  - [x] Membership by vanilla piece `creator` ("all my portals").
  - [x] Works on dedicated servers via a routed RPC (`ISP_ReqNet`/`ISP_RespNet`);
    resolves locally when we host the world.
  - [x] Arrival cooldown so landing on the target portal doesn't re-open the menu.
- [x] **Removed** the item-policy override (`ISP.policy` + dropdown) — wood/stone
  restrictions are now fixed and unmodifiable.
- [x] **Removed** the travel hotkeys — travel is portals-only.
- [x] In-game test (Tests V–Z): network hop between two of my portals, nearest-first
  menu, tag as label, unaffected vanilla tag portals, dedicated-server RPC path
  (distant portal returned), wood-blocks-ore still applies to a network wood portal
  — all confirmed.

## Phase 7 — Polish & packaging 🔨 IN PROGRESS
- [x] Config options: hotkeys moved to a **Hotkeys** section and made opt-in
  behind `EnablePrototypeHotkeys` (off by default — portals are the intended
  path); `LocalWorldName` doubles as the empty-config fallback destination.
- [x] Thunderstore package under `Thunderstore files/InterServerPortal/`:
  `manifest.json` (deps: BepInExPack_Valheim 5.4.2333, Jötunn 2.25.0), a 256×256
  `icon.png`, `README.md`, `CHANGELOG.md`. A `PackThunderstore` MSBuild target
  zips a ready-to-upload archive to `dist/` (every build also refreshes the DLL
  in the package folder).
- [x] Known-incompatibility notes in the README (IP:port joins only; server-side
  character/inventory mods; other `TeleportWorld` patchers).
- [ ] Quick in-game confirm: default build no longer fires F8/F9 unless
  `EnablePrototypeHotkeys` is enabled — **pending**.
- [ ] Actual Thunderstore upload (manual, when you're ready to publish).

## Testing checklist (run each phase)

- [x] Switch server → local, inventory intact *(Phase 1)*
- [x] Return local → server, inventory intact *(Phase 1)*
- [x] Redundant same-destination presses are no-ops, not extra reloads *(Phase 1)*
- [x] Flagged portal switches server→local→server via walk-through, no bounce *(Phase 2, Tests C–E)*
- [ ] Crash mid-switch → ReturnRegistry lets player recover
- [x] Vanilla portal (unflagged) still tag-pairs and teleports *(Phase 2, Test A)*
- [x] Missing/corrupt world shows offline, does not strand player *(Phase 3, Tests F–G: missing-world block confirmed; corrupt/version reasons + mid-flight fallback code-done, untested)*
- [x] Locked portal denies wrong code, accepts right code *(Phase 5, confirmed in-game)*
- [ ] Missing/corrupt world shows offline, does not strand player
- [x] Hub menu lists multiple seeds; picks correctly *(Phase 4, Tests J–N; arrival points deferred)*
- [x] Wood blocks ore (or prompts), stone carries ore through *(Phase 6, Tests O–Q; override removed in Phase 8, behavior now fixed)*
- [x] Network hop between my own portals; nearest-first menu; vanilla tag portals unaffected *(Phase 8, Tests V–Z; dedicated-server RPC path confirmed)*
- [ ] Other players see only a normal logout when someone uses the portal
