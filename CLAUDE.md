# InterServerPortal — Project Reference

A BepInEx/Harmony mod that lets a player standing on a dedicated server step
through a `portal_wood` (or `portal_stone`) and switch directly to **their own
local single-player world** — replacing the manual logout → main menu → pick
world → load → relog cycle with a single portal interaction.

> **The one hard rule:** Valheim binds a client to exactly one world per
> session. There is no way to be in two worlds at once or to switch without a
> loading screen. This mod does not fight that — it removes the *menu
> navigation*, not the load. Every transition costs exactly one loading screen.
> See [docs/Architecture-Constraints.md](docs/Architecture-Constraints.md).

## Quick Facts

| Field | Value |
|---|---|
| GUID | `com.interserverportal` |
| Version | `0.9.1` (Phase 9 — per-mode portal glow + Discord travel notify; 0.9.1 = icon refresh + documented that the frame runes can't be recoloured) on top of Phase 8 (same-world portal networks; removed the item-policy override and the travel hotkeys) |
| Framework | net48 |
| BepInEx dep | `5.x` (HarmonyX included) — denikson BepInExPack_Valheim 5.4.2333 |
| Jötunn dep | `[BepInDependency(Jotunn.Main.ModGuid)]` — 2.25.0 |
| Publicizer | BepInEx.AssemblyPublicizer.MSBuild (Publicize=true on assembly_valheim) |
| Assets | Vanilla only (`portal_wood`, `portal_stone`) |
| Runs on | `valheim.exe` (client) **and** `valheim_server.exe` (dedicated — multi-regional plan). Switch logic gated on `Player.m_localPlayer`, so it no-ops on a headless server. |

> **Status:** Phases 0–8 done and confirmed in-game (Phase 8 = same-world portal
> networks + the two revisions below, Tests V–Z incl. the dedicated-server RPC path).
> Phase 9 (v0.9.0 — per-mode portal glow + Discord travel notify) is built and
> deployed; the glow/vortex colours and the server→local Discord post are confirmed
> in-game (the outward flame burst is left orange by design). **v0.9.1** (icon
> refresh; no gameplay code change) additionally confirmed **in-game** that the
> glowing frame **runes** can't be recoloured — their colour is baked into the
> portal's emission texture and only `_EmissionColor`-multiplied, so they're left
> vanilla like the flame burst (a mesh-material retint pass was prototyped and
> reverted).
> **alt-use** opens a native Jötunn config panel (inter-server flag, a
> **Link-mode** button Tag-pair/Network, add/remove inter-server destinations, and a
> Lock section for a per-portal entry code). **Walking through** a portal branches by
> precedence: inter-server → local-world hub (single-dest fast path, multiple → menu,
> unavailable greyed); else network → a menu of the player's other network portals
> (nearest first) then a vanilla in-world teleport (no world switch); else vanilla tag
> pairing. Plain **Use (E)** still sets the vanilla tag (which doubles as a network
> portal's menu name). A locked portal prompts for its code first (salt+SHA-256,
> masked entry, throttle). The wood/stone item restriction is now **fixed by prefab**
> (wood blocks ore, stone carries it — the `ISP.policy` override was removed) and
> guards both inter-server and network travel. Network membership = "all my portals"
> by the vanilla piece `creator`; the list comes from `ZDOMan.GetPortals()` locally
> when hosting, else a routed RPC to the dedicated server. Local→local hops work; a
> local world's portal offers "Return to origin server". **Travel is portals-only**
> (the F8/F9 hotkeys were removed). **Per-mode portal glow** (Phase 9 / v0.9.0): each
> mode has a distinct emission + swirl-vortex colour (vanilla blue / network violet /
> inter-server cyan) that shows active-vs-dark just like a vanilla portal; the
> outward `blue flames` burst stays orange by design (its gradient-mapped shader
> bakes colour into a texture and exposes no colour input). **Discord travel notify**
> (Phase 9 / v0.9.0): with `Discord/WebhookUrl` set on the client, a server→local
> crossing posts to Discord. Deferred: named arrival points (world-spawn only), `ISP.group`
> cross-portal / shared networks. Next: the manual Thunderstore upload
> (`dist/InterServerPortal-<version>.zip`) whenever you're ready to publish. See
> [docs/Roadmap-Phases.md](docs/Roadmap-Phases.md).

## Concept in one diagram

```
  [Dedicated Server world]                 [Player's local single-player world]
        │                                            ▲
        │  step through flagged portal               │  return portal (remembers
        │  ─────────────────────────────────────────▶│  origin IP:port)
        │  disconnect → load worlds_local → rebuild   │
        ▼                                            │
   (one loading screen)  ◀──────────────────────────┘  (one loading screen)
```

Each entrant switches **their own** client only; other players just see them
leave the server. The portal is shared — everyone who uses it goes to *their
own* local world.

## Feature Summary

| # | Feature | Doc |
|---|---|---|
| Core | Portal → switch to own local world + return path | [Core-Mechanic-World-Switching](docs/Core-Mechanic-World-Switching.md) |
| 1 | Optional entry codes / lock | [Feature-Lock-Codes](docs/Feature-Lock-Codes.md) |
| 2 | "Local seed offline / load failed" notification + safe fallback | [Feature-Failure-Handling](docs/Feature-Failure-Handling.md) |
| 3 | Vanilla portal behavior preserved on the same pieces | [Portal-System](docs/Portal-System.md) |
| 4 | Hub menu routing across multiple seeds AND arrival points | [Feature-Hub-Routing](docs/Feature-Hub-Routing.md) |
| 5 | Same features on `portal_stone`; ore-block on wood, skip on stone (fixed) | [Feature-Item-Policy](docs/Feature-Item-Policy.md) |
| 6 | Same-world portal networks — mesh all a player's portals | [Feature-Portal-Network](docs/Feature-Portal-Network.md) |
| 7 | Per-mode portal glow — distinct colour + active/dark state per mode | [Feature-Portal-Visuals](docs/Feature-Portal-Visuals.md) |
| 8 | Discord notification on a server→local crossing | [Feature-Discord-Notify](docs/Feature-Discord-Notify.md) |

## Source Layout

`[x]` exists · `[ ]` planned. Current tree reflects Phase 8.

```
src/
├── [x] Plugin.cs                 BepInEx entry, config (DebugLogging, RememberServerPassword, Discord/WebhookUrl), Harmony bootstrap, per-frame re-arm tick + portal-network RPC registration
├── Core/
│   ├── [x] WorldSwitcher.cs      leave → load local / rejoin server; scene-reload-safe Pending; local→local hops; mid-flight FallbackToOrigin
│   ├── [x] ReturnRegistry.cs     persists origin server host:port (key=value file) for the return trip
│   └── [x] DestinationValidator.cs  pre-flight worlds_local validation (missing/corrupt/version) via SaveSystem + World.m_dataError
├── [x] Patches/
│   └── [x] FejdStartupPatch.cs   Postfix on FejdStartup.Start; resumes a pending switch after reload
├── [x] Portal/
│   ├── [x] PortalPatches.cs      TeleportWorld patches; walk-through branch inter-server / network / vanilla; alt-use=config; plain-use=vanilla tag; hover
│   ├── [x] PortalData.cs         custom ZDO read/write (ISP.enabled, ISP.dests, ISP.codehash/salt, ISP.link) + live portal registry + arm guard
│   ├── [x] PortalVisuals.cs      per-mode emission glow (vanilla blue / network violet / inter-server cyan) + connected-vs-dark active state
│   └── [x] Destination.cs        (label, worldName, spawnPointId) model + versioned serialization for ISP.dests
├── [x] Hub/
│   ├── [x] HubController.cs      inter-server travel orchestration: build entries, direction, fast path, greying
│   ├── [x] NetworkController.cs  same-world network travel: lock/item gate → request → menu → vanilla teleport + arrival cooldown
│   ├── [x] HubWindow.cs          native Jötunn modal (woodpanel + input-block + Escape) base for the panels
│   ├── [x] DestinationMenu.cs    inter-server travel selection panel
│   ├── [x] PortalNetworkMenu.cs  same-world network selection panel (nearest first, distance shown)
│   ├── [x] PortalConfigPanel.cs  alt-use editor: inter-server flag + link mode (Tag/Network) + destinations + lock
│   └── [x] CodePrompt.cs         masked entry-code prompt shown before a locked portal travels
├── [x] Security/
│   ├── [x] LockCodes.cs          salt + SHA-256(salt+code) hash/verify (raw code never stored)
│   └── [x] LockGate.cs           per-portal (ZDOID) wrong-attempt throttle + recently-unlocked memory
├── Policy/
│   └── [x] ItemPolicy.cs         fixed prefab teleport-restriction gate (wood blocks / stone allows; reuses vanilla IsTeleportable)
└── Net/
    ├── [x] PortalNetwork.cs      portal-network enumeration (ZDOMan.GetPortals + creator + ISP.link) + routed RPC (host-local or server query)
    └── [x] DiscordNotifier.cs    optional Discord webhook post when a player switches into their local world (Discord/WebhookUrl config)
```

> Usage note: **alt-use** (L.Shift + Use) on a `portal_wood` **or `portal_stone`**
> opens the config panel — flag it inter-server, set the same-world **link mode**
> (Tag pair / Network), add `(label, world)` inter-server destinations, and
> optionally set an entry code (Lock section). Plain **Use** (E) still sets the
> vanilla portal tag (which also names a network portal in the travel menu).
> **Walking through** branches by precedence: inter-server flagged → local-world
> travel (one dest → straight there, multiple → menu); else network → a menu of the
> player's other network portals then an in-world teleport; else vanilla tag pairing.
> A locked portal prompts for its code first; the fixed wood/stone item restriction
> then gates the teleport (wood refuses ore). Travel is portals-only — the old F8/F9
> hotkeys were removed. Named arrival points and `ISP.group` shared/named networks
> are deferred follow-ups.

## Documentation Index

- [Architecture-Constraints](docs/Architecture-Constraints.md) — ground truth on what Valheim allows
- [Core-Mechanic-World-Switching](docs/Core-Mechanic-World-Switching.md) — the core sequence + return path
- [Portal-System](docs/Portal-System.md) — prefabs, `TeleportWorld`, vanilla-vs-inter-server flagging
- [Data-Model-ZDO](docs/Data-Model-ZDO.md) — custom ZDO fields
- [Feature-Hub-Routing](docs/Feature-Hub-Routing.md) — #4
- [Feature-Lock-Codes](docs/Feature-Lock-Codes.md) — #1
- [Feature-Failure-Handling](docs/Feature-Failure-Handling.md) — #2
- [Feature-Item-Policy](docs/Feature-Item-Policy.md) — #5
- [Feature-Portal-Network](docs/Feature-Portal-Network.md) — #6
- [Feature-Portal-Visuals](docs/Feature-Portal-Visuals.md) — #7 (per-mode glow)
- [Feature-Discord-Notify](docs/Feature-Discord-Notify.md) — #8 (server→local webhook)
- [Development-Setup](docs/Development-Setup.md) — build toolchain, deploy/reload gotchas, decompiling
- [Roadmap-Phases](docs/Roadmap-Phases.md) — build order + testing checklist (Phases 0–2 done)
- [Changelog](docs/Changelog.md) — development log
