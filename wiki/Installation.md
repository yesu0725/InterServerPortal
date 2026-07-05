# Installation

## Requirements

- **BepInEx** for Valheim (denikson BepInExPack_Valheim 5.4.2333+)
- **Jötunn** (ValheimModding-Jotunn 2.25.0+)

Both are pulled in automatically when you install through a mod manager.

## Install with a mod manager (recommended)

1. Open **r2modman** or the **Thunderstore Mod Manager**.
2. Search for **InterServerPortal** and install it. BepInEx and Jötunn are added as
   dependencies automatically.
3. Launch the game through the mod manager.

## Manual install

1. Install BepInEx and Jötunn.
2. Extract the InterServerPortal package so that `InterServerPortal.dll` lands in
   `BepInEx/plugins/InterServerPortal/`.
3. Launch the game.

## Where to install it

| Setup | Install on |
|---|---|
| Single-player only (portal networks) | Your game client |
| Joining a dedicated server | **Every player's client** |
| Running a dedicated server | The **server** too (in addition to clients) |

**On a dedicated server** the world-switch logic no-ops (there is no local player
on a headless server), so the mod is safe there. Installing it server-side keeps
each portal's flag/config/lock networked and persisted with the world, and enables
the network lookup for portals in unloaded zones. If you only run listen/host
games, client-side is enough.

## After installing

- A per-portal config panel becomes available via **L.Shift + Use** on any
  `portal_wood` or `portal_stone`.
- The config file `BepInEx/config/com.interserverportal.cfg` is written on first
  launch — see **[Configuration](Configuration)**.

## Updating

Because BepInEx loads plugins only at process start, **fully quit Valheim to the
desktop and relaunch** after updating — an in-game "Log Out" (and this mod's world
switch) keeps the old assembly loaded.
