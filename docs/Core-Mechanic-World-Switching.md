# Core Mechanic — World Switching

The heart of the mod and its biggest engineering risk. Everything else is UI or
data on top of this sequence.

## Goal

From inside a live server session, on portal interaction, transition the local
client to a chosen local single-player world — and later back — each with a
single loading screen and no manual menu navigation.

## The transition sequence (server → local)

Conceptually this reproduces what the menu does when you leave a server and
start a single-player world, but driven programmatically:

1. **Capture origin.** Record the current server `IP:port` (and the join
   context/password if available) into [ReturnRegistry](#return-registry) so the
   local world knows where "back" is. Persist to disk, not just memory — a
   crash mid-switch must not strand the player.
2. **Validate destination.** Confirm the target world save exists and is loadable
   (see [Feature-Failure-Handling](Feature-Failure-Handling.md)) *before*
   disconnecting. Fail fast while still safely on the server.
3. **Tear down the server session.** Leave the world cleanly — the same path the
   game uses for "Log Out" / return to main menu (`Game.instance` shutdown,
   `ZNet.Shutdown`). Do not skip the normal teardown; a partial teardown is the
   main crash source.
4. **Start the local world.** Configure the world/host settings for the chosen
   local seed and start a single-player session (the `FejdStartup` /
   `SaveSystem` world-start path). This produces the loading screen.
5. **Arrive.** On world-loaded, place/verify a **return portal** and drop the
   player at the chosen arrival point (Feature #4 routing).

## The transition sequence (local → server)

1. Read origin from ReturnRegistry.
2. Tear down the local session (same clean path).
3. Connect to the origin server (`ZNet.SetServer` / join by address).
4. On failure, notify and leave the player in the local world rather than a
   broken state.

## Implementation status

**Phase 1 is implemented and confirmed working in-game** (full server → local →
server round-trip). Code:

- `src/Core/WorldSwitcher.cs` — the sequence + the `Pending` static that
  survives the scene reload.
- `src/Core/ReturnRegistry.cs` — persisted origin store.
- `src/Patches/FejdStartupPatch.cs` — resumes the switch in the start scene.
- `src/Plugin.cs` — prototype hotkeys (config `SwitchToLocalKey` /
  `ReturnToServerKey`; the user runs F7/F8 since F9 is taken in-game).

## Verified Valheim API (assembly_valheim, 2026-07-04)

Reflected/decompiled from the publicized assembly. **Re-verify after any Valheim
update — these names drift.**

**Leave the current session (both directions):**
- `Game.instance.Logout(bool save, bool changeToStartScene)` → `(true, true)`.
  Tears down cleanly and loads the start scene (FejdStartup). Do not skip this;
  a partial teardown is the main crash source.

**Scene-reload survival.** A switch spans *Game scene → start scene → main
scene*. A BepInEx `BaseUnityPlugin` lives on a `DontDestroyOnLoad` object, so
the static `WorldSwitcher.Pending` and `Plugin.Instance.StartCoroutine` survive.
The switch is resumed from a **Postfix on `FejdStartup.Start`**.

**Start a LOCAL world (server → local):**
- Find the world from disk: `SaveSystem.GetWorldList()` → `List<World>`, match on
  `World.m_name`. **Do NOT use `FejdStartup.FindWorld` here** — it reads the
  menu's cached `m_worlds`, which is unpopulated that early and throws an NRE
  (see Findings).
- `fejd.m_world = world; fejd.m_startingWorld = true;`
- `ZNet.SetServer(true, false, false, world.m_name, "", world)` — host a
  private, non-open single-player session.
- `fejd.TransitionToMainScene()` — begins a 1.5s fade, then loads the main scene
  because `m_startingWorld` is true.

**Return to a dedicated server (local → server):**
- **Bypass `FejdStartup.JoinServer()`** (see Findings — its async matchmaking
  resolution never completes for a raw/local IP).
- `ZNet.SetServer(false, false, false, "", "", null); ZNet.ResetServerHost();`
- `ZNet.SetServerHost(host, port, OnlineBackendType.Steamworks)` — this is
  exactly what vanilla's async callback ultimately calls. Sets `HasServerHost()`
  true so the transition loads immediately.
- Optional password: `FejdStartup.ServerPassword = password;` (static property).
- `fejd.m_startingWorld = false; fejd.TransitionToMainScene();`
- `TransitionToMainScene` → `LoadMainSceneIfBackendSelected` loads when
  `m_startingWorld || ZNet.HasServerHost()`.

**Role checks (guards):**
- Local single-player host: `ZNet.instance.IsServer() && !IsDedicated()`.
- Connected to a remote server: `!ZNet.instance.IsServer()`.

Full signature list: see project memory `interserverportal-worldswitch-api`.

## Return Registry

Persisted as a small `key=value` text file (no JSON dependency) at
`BepInEx/config/InterServerPortal.return.txt`:

```
host=127.0.0.1
port=2456
password=            # empty unless RememberServerPassword config is ON (opt-in)
backend=Steamworks
timestamp=2026-07-04T09:40:12Z
```

Captured from `ZNet.m_serverHost` / `m_serverHostPort` / `m_serverPassword` /
`m_onlineBackend` on the outbound trip; read on the return trip. Persisted to
disk so a crash mid-switch can't strand the player.

## Findings / gotchas (from Phase 1 testing)

1. **`FejdStartup.FindWorld` throws early.** In the start scene the menu's
   `m_worlds` list isn't populated yet, so `FindWorld` NREs instead of returning
   null. Fix: read `SaveSystem.GetWorldList()` from disk; guard any
   `FejdStartup.FindWorld` call in try/catch.
2. **`FejdStartup.JoinServer()` silently fails for a known IP.** For a dedicated
   server it runs an *async* `MultiBackendMatchmaking.GetServerIPAsync`, which
   never resolves for a raw/local IP (no matchmaking entry). `HasServerHost()`
   stays false, so `TransitionToMainScene` retries and gives up with no
   connection and no obvious error. Fix: set `ZNet.SetServerHost(...)` directly.
3. **`ZNet.m_serverHost` may already contain the port** (`"ip:port"`). Since we
   also store the port separately, a naive reconnect builds `"ip:port:port"` and
   fails. Fix: `NormalizeHostPort()` strips a trailing numeric `:port` (leaves
   bare IPv6 alone).
4. **Reentrancy guard must reset on success.** `InProgress` was only cleared on
   failure, so after a successful switch it stayed true forever and bricked the
   hotkeys in the destination world (keypress eaten, no log). Fix: clear
   `InProgress` right after `TransitionToMainScene()` — safe because
   `Player.m_localPlayer` is null during the load, so nothing can double-fire.
5. **No-op guards.** Switching to local while already a local host, or returning
   while already a remote client, just reloads where you are. Now skipped with a
   log line. NOTE: the "already local host" guard also blocks local→local hops
   between *different* seeds — relax it in Phase 4 to compare the actual target.

## Risk notes / follow-ups

- **Teardown timing.** Resume waits for `FejdStartup.instance` + a couple frames
  before driving the transition; login/PlayFab flows may need more on slower
  setups.
- **Join types.** v0.1 targets direct `IP:port` (Steamworks backend) — the
  multi-regional dedicated-server plan. Steam-friend / PlayFab-relay joins are
  out of scope; `m_serverHost` won't hold a usable address for them.
- **Password handshake.** Pre-setting `FejdStartup.ServerPassword` avoids the
  prompt; if absent, the vanilla connecting UI still prompts.
