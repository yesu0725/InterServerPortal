# Architecture Constraints — Ground Truth

This is the doc that governs every design decision. If something below is
violated, the feature is impossible, not just hard.

## 1. One world per client session

A Valheim client is bound to exactly one world at a time. The world state is
held in engine singletons that are torn down and rebuilt on any world change:

- `ZNet` — the netcode layer (connection to a server *or* the local socket
  when hosting). One instance; hosting and being a client are mutually exclusive.
- `ZDOMan` — the authority store for all networked objects (ZDOs).
- `ZoneSystem` / `ZNetScene` — terrain/zone generation and the spawned object graph.
- The world save (`.db`/`.fwl`).

**Consequences:**
- You cannot be a client of Server A and simultaneously host/load World B.
- You cannot hold two worlds in memory at once.
- Switching worlds *requires* tearing all of the above down and rebuilding →
  this is a loading screen, and it cannot be avoided.

## 2. What "seamless" would require (and why it's impossible)

A zero-loading-screen portal into another world would need two live `ZNet`/
`ZDOMan` graphs concurrently. The engine is not built for it and there is no
mod-level seam to add it. **Do not promise seamless.** The mod's value is
removing menu navigation, not removing the load.

## 3. What *does* carry across for free: the character

The player character — including inventory, skills, and equipment — is stored
**client-side** in the character save file, separate from any world. When the
client loads a different world, the same character (and its full inventory)
comes along automatically.

**Consequences:**
- We do NOT need item-transfer plumbing across the portal. Gear just follows.
- The vanilla "ores can't teleport" restriction is enforced *only* during a
  `TeleportWorld` teleport check — it does **not** gate a world load. So on a
  raw world switch, ore comes through by default. Feature #5 re-applies that
  restriction deliberately on the wood portal. See
  [Feature-Item-Policy](Feature-Item-Policy.md).
- Caveat: servers using enforced/server-side character mods break this
  assumption. Out of scope for v0.1; document as a known incompatibility.

## 4. Per-player, client-side switch

Because the switch happens on the entrant's own client, "shared portal for
everyone" is trivially consistent: when Player X uses the portal, only X's
client disconnects and loads X's local world. Everyone else simply observes X
leave the server (normal logout from their point of view).

## 5. The return path is not optional

Once in the local single-player world, the player needs a way back. The mod
must therefore also run inside the local world and:
- Remember the origin server's `IP:port` (and password context) before leaving.
- Provide a return portal (or auto-placed marker) in the local world that
  reconnects to that origin.
See [Core-Mechanic-World-Switching](Core-Mechanic-World-Switching.md).

## Summary table

| Want | Possible? | Notes |
|---|---|---|
| Switch to own local world via portal | ✅ | One loading screen |
| No loading screen / two worlds at once | ❌ | Engine singletons forbid it |
| Keep inventory across the switch | ✅ (automatic) | Character is client-side |
| Shared portal, per-player destination | ✅ | Switch is client-local |
| Return to server from local world | ✅ | Must persist origin IP:port |
