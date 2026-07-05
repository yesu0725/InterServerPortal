# Inter-Server Travel

The core feature: step through a portal on a server and switch to **your own local
world**, then step back to return. Each transition is a single portal interaction —
plus one unavoidable loading screen.

## Set up an inter-server portal

1. **Alt-use** (**L.Shift + Use**) a `portal_wood` or `portal_stone` to open the
   config panel (requires build access).
2. Turn on the **inter-server** flag.
3. Add one or more **destinations**, each with:
   - **Label** — shown in the travel menu.
   - **World** — the exact name of a local single-player world.
4. Optionally set an **entry code** (see **[Entry Codes & Locks](Entry-Codes-and-Locks)**).

The portal glows **cyan** and lights up once it has a usable destination.

## Travelling

**Walk through** the portal:

- **One destination** → you go straight there (fast path).
- **Multiple destinations** → a selection menu opens. Unavailable worlds are greyed
  out with the reason.
- **Locked portal** → you're prompted for the code first.

Each player who uses the shared portal goes to **their own** local world. Other
players just see you disconnect from the server as normal.

## Returning to the server

Inside a local world, walking through a portal offers **Return to origin server**.
The origin (host + port) is remembered — persisted to disk — so the return trip
works even after a game restart or crash.

For password-protected servers, enable `RememberServerPassword` so the reconnect
can log in automatically (see **[Configuration](Configuration)**).

## Local → local hops

You can also travel from one local world to **another** local world by adding a
second local world as a destination. The origin server is kept, so you can still
**Return to origin server** from the new world.

## Safety & failure handling

- **Pre-flight validation.** Before you leave, the target world is checked. If it's
  **missing, corrupt, or the wrong version**, you get a centered message explaining
  why and you **stay put** — no teardown, no strand.
- **Mid-flight fallback.** If a load fails after you've already left the server, the
  mod reconnects you to the remembered origin server instead of dumping you on the
  main menu.
- **No arrival bounce.** You spawn standing inside the portal you used; travel only
  re-arms once you step clear, so you won't loop between worlds.

## Things to know

- Travel is **portals-only** — there are no travel hotkeys.
- Your character and inventory travel with you, subject to the
  **[Item Policy](Item-Policy)** (a wood portal refuses ore/metals).
- Return works for direct **IP:port** (Steamworks) servers. Steam-friend / relay
  joins aren't supported for the return trip.

See also: **[Getting Started](Getting-Started)** ·
**[FAQ & Troubleshooting](FAQ-and-Troubleshooting)**.
