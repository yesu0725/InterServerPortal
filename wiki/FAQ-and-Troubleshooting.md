# FAQ & Troubleshooting

## Frequently asked questions

**Does everyone who uses a shared portal end up in the same world?**
No — each player goes to **their own** local world. The portal is shared; the
destination is personal.

**Can I avoid the loading screen?**
No. Valheim binds you to one world per session; switching always costs one load.
The mod removes the *menu navigation*, not the load.

**Do I need this on the dedicated server?**
Install it on every **client**. Installing it on the **server** too keeps portal
config networked/persisted and lets network lookups find portals in unloaded zones.
The switch itself no-ops server-side.

**Will it break my vanilla portals?**
No. Portals you don't configure keep exact vanilla 1:1 tag pairing.

**Can I carry ore/metal through?**
Through a **stone** portal, yes. A **wood** portal blocks it, exactly like vanilla.
See **[Item Policy](Item-Policy)**.

**Does it add custom items or models?**
No — vanilla assets only (`portal_wood`, `portal_stone`).

## Troubleshooting

**My changes/features don't seem to apply.**
BepInEx loads plugins only at process start. **Fully quit to the desktop and
relaunch** — an in-game Log Out (and this mod's world switch) keeps the old
assembly loaded. With a mod manager, also confirm you launched the profile the mod
is actually installed in.

**The config panel doesn't open on L.Shift + Use.**
You need **build access** to the area (same as editing the portal tag). Make sure
you're allowed to build there, and that you're holding L.Shift while pressing Use.

**The portal is its colour but stays dark.**
That means it's not usable yet:
- Inter-server portal → add a destination (or, in a local world, it lights when a
  **Return to origin server** path exists).
- Network portal → you need **another** network portal you built in that world.
See **[Portal Modes & Colours](Portal-Modes-and-Colors)**.

**The outward flames are orange, not my mode colour.**
That's expected — the flame burst's shader can't be recoloured. The frame glow and
inner swirl carry the mode colour.

**"`<world>` is unavailable" when I try to travel.**
The destination local world is missing, corrupt, or a different version. Check the
world exists in your profile and the name matches exactly. You're not stranded — the
mod keeps you where you are (or reconnects you to the server if it failed mid-switch).

**The return trip doesn't reconnect / asks for a password.**
Return works for direct **IP:port** (Steamworks) servers. For password-protected
servers, enable `RememberServerPassword` in the config (stored in plain text) so the
reconnect can log in.

**I keep bouncing between worlds.**
Step **clear** of the portal after arriving — travel re-arms only once you're away
from the portal, which prevents spawn-overlap loops. If it still loops, report it
with `DebugLogging` enabled.

**Discord messages aren't posting.**
See **[Discord Notifications](Discord-Notifications)** → Troubleshooting.

## Reporting an issue

1. Set `[General] DebugLogging = true` in `BepInEx/config/com.interserverportal.cfg`.
2. Reproduce the problem.
3. Open an issue with the BepInEx log at
   [github.com/yesu0725/InterServerPortal/issues](https://github.com/yesu0725/InterServerPortal/issues).
