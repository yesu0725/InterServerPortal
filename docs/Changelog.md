# Changelog

Development log for InterServerPortal. Newest first.

## Unreleased — per-mode portal glow + Discord travel notify

### Added — Discord notification
- `src/Net/DiscordNotifier.cs` + **`Discord/WebhookUrl`** config: when a webhook URL
  is set, posts `"<player> stepped through a portal to their local world <world>
  (from <server>)"` whenever a player crosses **server → local** (gated to a remote
  client in `WorldSwitcher.RequestSwitchToLocal`; local→local hops don't notify).
  Sent on a background thread with an explicit TLS 1.2 handshake + accept-cert
  callback — Valheim's Mono ships no usable CA root store and its default validation
  otherwise rejects Discord's cert, and the thread completes independently of the
  world-switch teardown. No-op when the URL is empty.

### Added — per-mode portal glow
- `src/Portal/PortalVisuals.cs` — recolors the portal's emission glow so each mode
  is distinct at a glance, and shows connected/unconnected exactly like a vanilla
  portal:
  - **vanilla tag-pair** → untouched native blue; active when a 1:1 tag partner is
    found (vanilla drives this).
  - **network** → violet `#c4b5fd`; active when the builder's mesh has ≥1 other
    network portal to hop to (a lone network portal sits dark).
  - **inter-server** → sky/cyan `#7dd3fc`; active when it has a usable travel option
    (a configured local-world destination, or — on a local world — a "Return to
    origin server" path).
  - Piggybacks vanilla's split: state recomputed on the 0.5s `UpdatePortal` tick,
    color lerped on the per-frame `Update` postfix via the same `_EmissionColor`
    channel/animation vanilla uses, so glow fades in/out identically. Network
    membership is counted at most ~1/s from `ZDOMan.GetPortals()`; the return-path
    file check is cached ~3s. State is pruned when a portal turns vanilla or is
    destroyed.
  - The near-portal **swirl vortex** (vanilla `m_target_found` / `EffectFade`) is
    driven on for our portals (vanilla only shows it on a tag connection) using the
    same "player near + can teleport" gate when active. The portal's particle
    effects and lights are retinted to the mode hue via three color sources —
    `startColor`, the **Color-over-Lifetime** gradient (whitened, keeping its alpha
    fade), and the particle **renderer material** color props (instanced per-portal;
    `_TintColor`/`_Color`/`_BaseColor`/`_EmissionColor`). Captured per-instance and
    restored on flip-back-to-vanilla, so it never bleeds onto vanilla portals.
  - **Known limitation:** the outward `blue flames` burst is **left orange in all
    modes** (by request). Its `Custom/Gradient Mapped Particle (Unlit)` shader
    exposes no color property and bakes the color into its `_MainTex` (`wildfire06`),
    so it can't be tinted through material/particle color. A texture-recolor attempt
    (GPU blit + hue remap) was tried and then dropped as too invasive; the frame
    glow, inner vortex, sparks and lights carry the mode color instead.

## 0.8.0 — Same-world portal networks + revisions (2026-07-04)

**Status:** ✅ confirmed in-game (Tests V–Z): network hop between two portals with a
nearest-first menu and tags as labels, vanilla tag-paired portals unaffected, the
dedicated-server RPC path returned a distant (unloaded-zone) portal, and a wood
network portal still blocks ore. Builds clean (0 warnings).

### Added — portal networks
- Same-world **portal network** mode: a per-portal opt-in (`ISP.link` = network) that
  meshes with every other network-mode portal the same player built. Walking through
  one lists the others (nearest first, labelled by tag) and teleports you there — a
  vanilla in-world teleport, no world switch / loading screen.
- `src/Net/PortalNetwork.cs` — enumerates `ZDOMan.GetPortals()` (whole-map portal
  ZDO set) filtered by vanilla `creator` + our link flag. Resolves locally when we
  host the world; on a dedicated server a routed RPC (`ISP_ReqNet` → `ISP_RespNet`)
  asks the server so distant/unloaded portals aren't missed. Re-registers the RPC
  after each reconnect/world switch.
- `src/Hub/NetworkController.cs` — lock gate → item check → request → menu →
  vanilla exit-placement teleport, with a short arrival cooldown so landing on the
  destination portal doesn't re-open the menu.
- `src/Hub/PortalNetworkMenu.cs` — the selection menu (distance shown, nearest first).
- Config panel gains a **Link mode** button (Tag pair / Network); hover shows
  `NETWORK`. Inter-server takes precedence over network on a portal set to both.

### Changed / removed (revisions)
- **Removed the item-policy setting** (`ISP.policy` + the config dropdown). Wood and
  stone keep their fixed vanilla item restrictions, not user-modifiable;
  `ItemPolicy.BlocksRestricted` is now simply `!m_allowAllItems` and also guards
  network teleports.
- **Removed the travel hotkeys** (`EnablePrototypeHotkeys`, F8/F9, `LocalWorldName`)
  and the empty-config prototype fallback. Travel is portals-only.

## 0.7.0 — Phase 7: polish & packaging (2026-07-04)

**Status:** 🔨 packaging complete; the config reorg is code-complete and awaiting a
quick in-game confirm (the default build should no longer fire the hotkeys).

### Added
- Thunderstore package under `Thunderstore files/InterServerPortal/`:
  `manifest.json` (dependencies: `denikson-BepInExPack_Valheim-5.4.2333`,
  `ValheimModding-Jotunn-2.25.0`), a 256×256 `icon.png` (glowing stone-arch
  portal), `README.md`, and `CHANGELOG.md`.
- `PackThunderstore` MSBuild target — `dotnet build -t:PackThunderstore -c Release`
  zips a ready-to-upload archive to `dist/InterServerPortal-<version>.zip`. A
  `DeployToThunderstore` post-build step also refreshes the DLL in the package
  folder so it never goes stale.

### Changed
- Config reorganized. The prototype hotkeys moved from the `Prototype` section to
  a `Hotkeys` section and are now **opt-in** behind `EnablePrototypeHotkeys`
  (default off) — portals are the intended way to travel. `LocalWorldName` keeps
  doubling as the fallback destination for a flagged portal with no configured
  destinations.

### Notes
- Version scheme continues the per-phase minor bump (Phase 7 → 0.7.0); bump to a
  1.0.0 at the actual public Thunderstore release if desired.

## 0.6.0 — Phase 6: item policy & portal_stone (2026-07-04)

**Status:** ✅ confirmed in-game (Tests O–R): wood blocks ore with a named
message, stone carries it through, the `ISP.policy` override flips each way, and
the block applies on the return leg too. `portal_stone` support is automatic (all
patches hook the shared `TeleportWorld` component); the new work is the
wood-vs-stone item policy.

### Added
- `src/Policy/ItemPolicy.cs` — the wood/stone teleport-restriction gate. Resolves
  the effective policy (the `ISP.policy` override, else the prefab default via the
  vanilla `TeleportWorld.m_allowAllItems` field: wood blocks, stone allows) and,
  when blocking, uses the vanilla `Humanoid.IsTeleportable()` predicate (which also
  honours the `TeleportAll` global key). A block denies with `$msg_noteleport` plus
  the offending item names, mirroring vanilla wording. Reuses the vanilla predicate
  rather than a hardcoded item list, so modded teleport flags behave correctly.
- `PortalData.Get/SetPolicy` on the `ISP.policy` ZDO int (0 auto / 1 block / 2 allow).
- Config panel: an **Item policy** dropdown (Auto / Block restricted / Allow all;
  the Auto label shows the prefab default), applied on Save.
- Hover text shows `[ore blocked]` / `[all items]` on a flagged portal.

### Changed
- `HubController.ProceedTravel` runs the item-policy gate just before the switch
  (after the lock gate): a blocked, restricted-item-carrying player is denied and
  stays put.

### Notes
- `portal_stone` needed no dedicated code — every Harmony patch targets the
  `TeleportWorld` component both prefabs share, so flagging, config, travel, and
  lock codes already applied to stone.
- The policy applies to whichever portal you step through, so it gates both the
  outbound (server→local) and return (local→server) legs by that portal's prefab.

## 0.5.0 — Phase 5: lock codes (2026-07-04)

**Status:** ✅ confirmed in-game — set/clear code, wrong code denied + throttled,
right code travels, and the three follow-up fixes below all verified.

### Added
- `src/Security/LockCodes.cs` — salt generation + SHA-256 hash of `salt + code`
  and a verify helper. The raw code is never stored (anti-casual-peek).
- `PortalData` — `ISP.codehash` / `ISP.codesalt` ZDO fields with
  `IsLocked` / `SetCode` (set generates a fresh salt; empty clears) / `CheckCode`.
- `src/Security/LockGate.cs` — per-portal (by ZDOID) session state: an escalating
  wrong-attempt cooldown (2s × fails, capped 30s) and a 5-minute "recently
  unlocked" memory so a correct code isn't re-prompted on every use.
- `src/Hub/CodePrompt.cs` — masked entry-code prompt shown before a locked portal
  travels; correct → proceed, wrong → deny message + throttle failure.
- Config panel: a Lock section (masked "new code" field + **Set code** /
  **Clear lock**, applied on Save) and a lock-state line.
- Hover text shows a `[locked]` indicator on locked inter-server portals.

### Changed
- `HubController.BeginTravel` now runs the entry-code gate first (respecting the
  throttle + unlock memory) before building the travel menu.
- `HubWindow.AddInput` gained an optional `ContentType` (for password masking).

### Fixed (found during in-game testing)
- **Arrival bounce returned** (server→local→server, once, then stopped on the
  server): `UpdateArming` re-armed *during teardown* — the old world's portals are
  destroyed before the old player, so the "clear of portals" test read true
  mid-flight and armed before arrival. Added `WorldSwitcher.AwaitingArrival`: the
  arm tick now runs every frame and refuses to re-arm until the old player has
  gone null and a new player has spawned in the destination world.
- **Plain Use (E) opened the travel menu**, hijacking vanilla tag-setting. Reverted:
  E now always falls through to the vanilla tag input; **travel happens only by
  walking through** a flagged portal; alt-use still opens config. Hover hint +
  docs updated.
- **Lock prompt layout**: title/label/input overlapped. Rewrote `CodePrompt`
  with the same computed-height top-down cursor layout as the config panel.

### Notes
- Codes are per-portal; per-`ISP.group` codes remain a future option.

## 0.4.0 — Phase 4: hub routing (2026-07-04)

**Status:** ✅ confirmed in-game (Tests J–N): config panel add/remove/save, travel
menu + single-destination fast path, greyed unavailable destinations, local→local
hop, return-to-server, and empty-config fallback all work. Arrival points +
`ISP.group` grouping deferred (see notes).

### Added
- `src/Portal/Destination.cs` — `(label, worldName, spawnPointId)` model with a
  versioned, percent-escaped serialization (`v1|label;world;spawn|…`) for the
  `ISP.dests` ZDO field.
- `PortalData.Get/SetDestinations` — read/write the per-portal destination list
  (claims ZDO ownership so it networks + persists).
- `src/Hub/HubWindow.cs` — native Jötunn modal base (woodpanel, title,
  `GUIManager.BlockInput`, Escape-to-close, single-window).
- `src/Hub/PortalConfigPanel.cs` — alt-use editor: inter-server flag toggle +
  add/remove destinations, world chosen from a dropdown of local saves.
- `src/Hub/DestinationMenu.cs` — travel selection panel; unavailable worlds are
  greyed with a reason instead of dropped.
- `src/Hub/HubController.cs` — `BeginTravel` builds the entry list (adds a
  "Return to origin server" entry inside a local world), validates each,
  single-destination fast path, else shows the menu.

### Changed
- New portal interaction model (`PortalPatches`): **alt-use** → config panel;
  **plain Use / walk-through** on a flagged portal → travel menu; unflagged
  portals keep vanilla tag input + teleport. Hover text updated.
- `WorldSwitcher.RequestSwitchToLocal` — relaxed the "already local host" guard to
  compare the target world, enabling **local→local hops** between seeds; origin
  server is only (re)captured when leaving a real server, so a hop preserves the
  saved origin. Removed the Phase 2 `RequestPortalSwitch` (superseded by the hub).
- Added the `UnityEngine.TextRenderingModule` reference (needed for `Font`).

### Fixed (found during in-game testing)
- Config panel layout: with zero destinations the panel was too short, so
  "Add destination" rendered underneath "Save & Close" and was unreachable.
  Rewrote `PortalConfigPanel` layout to compute the panel height from its content
  and place elements with a top-down cursor (named metrics), so it always fits and
  grows as destinations are added; also fixed the row label vs. Remove-button
  horizontal overlap.

### Deferred (follow-ups)
- Named arrival points (`spawnPointId`) — world spawn only for now.
- `ISP.group` cross-portal grouping — destinations are stored per-portal.

## 0.3.0 — Phase 3: destination validation & failure handling (2026-07-04)

**Status:** ✅ pre-flight validation confirmed in-game (Tests F–G) — a
missing/renamed world blocks with a centered message and keeps the player on the
server; a valid world still switches. The corrupt/missing-data/version reason
strings and the mid-flight `FallbackToOrigin` reconnect are code-complete but not
yet exercised in-game (Tests H–I, hard to stage by hand).

### Added
- `src/Core/DestinationValidator.cs` — validates a local-world destination from
  the on-disk save list (`SaveSystem.GetWorldList()` + `World.m_dataError`),
  mapping `None/BadVersion/Corrupt/MissingMeta/MissingDB/LoadError` to a
  player-facing reason (not found / corrupt / incompatible version / missing
  data / could not load).
- Pre-flight validation in `WorldSwitcher.RequestSwitchToLocal`: an unavailable
  destination shows a centered message and **aborts before any teardown**, so
  the player stays safely on the server.
- Mid-flight safety net in `StartLocalWorld`: re-validate + try/catch; on failure
  `FallbackToOrigin` reconnects to the remembered origin server (or drops cleanly
  to the main menu if there is no saved origin) instead of stranding the player.

### Changed
- Replaced the Phase 1 `WorldSwitcher.FindWorld` helper (which only logged and
  left the player on the menu when the world was missing) with the validator +
  fallback path.

## 0.2.0 — Phase 2: portal integration (2026-07-04)

**Status:** ✅ confirmed in-game (Tests A–E). Flagged portals drive the full
server→local→server switch by walk-through with no bounce; unflagged portals
stay vanilla.

### Added
- `src/Portal/PortalData.cs` — read/write the `ISP.enabled` portal ZDO flag
  (networked + world-persisted). Claims ZDO ownership before mutating so the
  flag replicates and saves.
- `src/Portal/PortalPatches.cs` — Harmony patches on `TeleportWorld`:
  - `Teleport` prefix: a flagged portal swallows the vanilla paired teleport and
    runs the inter-server switch for the local player instead.
  - `Interact` prefix: **alt-use** (L.Shift + Use) toggles the inter-server flag
    (the Phase 2 "config UI"), gated on `PrivateArea.CheckAccess`; plain use
    still opens the vanilla tag input.
  - `GetHoverText` postfix: shows the flag state + the alt-use toggle hint.
- `WorldSwitcher.RequestPortalSwitch()` — direction-aware entry so one flagged
  portal serves both legs (on a server → go local; in a local world → return).

### Fixed (found during in-game testing)
- **Infinite world-to-world bounce** on the return leg: after a switch the
  player spawns inside the portal they just used, `OnTriggerEnter` fires on that
  spawn-overlap and immediately switches back — looping forever. Added an
  arm/disarm guard (`WorldSwitcher.PortalArmed`): a switch disarms; re-arms only
  once the player is > ~3 m from every inter-server portal (live portal registry
  from `TeleportWorld.Awake`, checked in `PortalData.UpdateArming`). Starts
  disarmed so logging in on a portal doesn't insta-switch. See
  docs/Portal-System.md → "Arrival re-trigger".

### Notes
- Prototype hotkeys (F8/F9) remain wired for now; portals are the primary path.
- Destination is still the single hardcoded `Prototype/LocalWorldName` — hub
  routing replaces it in Phase 4.

## 0.1.0 — Phase 1: core world-switch prototype (2026-07-04)

**Status:** server → local → server round-trip confirmed working in-game.

### Added
- `src/Plugin.cs` — BepInEx entry (`com.interserverportal`), Harmony bootstrap,
  config, and prototype hotkeys (`SwitchToLocalKey` / `ReturnToServerKey`).
- `src/Core/WorldSwitcher.cs` — the disconnect → load-other-world sequence, with
  a static `Pending` intent that survives the Game→start→main scene reload.
- `src/Core/ReturnRegistry.cs` — persists the origin server `host:port` (+ opt-in
  password) to `BepInEx/config/InterServerPortal.return.txt`.
- `src/Patches/FejdStartupPatch.cs` — Postfix on `FejdStartup.Start` that resumes
  a pending switch after the reload.
- Dedicated-server support: second `[BepInProcess("valheim_server.exe")]` for the
  multi-regional server plan; switch logic gated on `Player.m_localPlayer`.
- Build auto-deploys to Steam, r2modman ("Hearthbound Valheim - Test"), and the
  dedicated server.

### Fixed (found during in-game testing)
- Local world lookup used `FejdStartup.FindWorld`, which NREs early in the start
  scene → now reads `SaveSystem.GetWorldList()` from disk.
- Return trip called `FejdStartup.JoinServer()`, whose async matchmaking IP
  resolution never completes for a raw/local dedicated IP → now sets
  `ZNet.SetServerHost(host, port, Steamworks)` directly.
- `ZNet.m_serverHost` can embed the port (`"ip:port"`), producing a malformed
  `"ip:port:port"` on reconnect → added `NormalizeHostPort()`.
- `InProgress` reentrancy guard was only cleared on failure, bricking hotkeys
  after the first successful switch → now cleared after `TransitionToMainScene()`.

### Changed
- No-op guards: switching to local while already a local host, or returning
  while already a remote client, is now skipped with a log line instead of a
  redundant reload.

### Known limits (carried to later phases)
- Direct `IP:port` (Steamworks) joins only — no Steam-friend/PlayFab relay.
- Local→local hops between different seeds are blocked (relax in Phase 4).
- No destination validation / offline handling yet (Phase 3).

## 0.1.0 — Phase 0: scaffold (2026-07-03)

### Added
- `.csproj` (net48, publicizer, Jötunn + Unity + BepInEx refs) and empty-but-
  loadable `Plugin.cs`.
- Documentation knowledge base under `docs/`.
