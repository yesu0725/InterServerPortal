# Configuration

Most of InterServerPortal is configured **per portal, in-game** via the config panel
(**L.Shift + Use**). A small number of global settings live in the BepInEx config
file.

## Config file

`BepInEx/config/com.interserverportal.cfg` — written on first launch. Edit it with a
text editor, or through a config-manager mod's in-game UI.

| Section / Key | Default | What it does |
|---|---|---|
| `General` / `DebugLogging` | `false` | Verbose logging to the BepInEx console/log. Turn on when reporting an issue. |
| `General` / `RememberServerPassword` | `false` | Persist the origin server's password so the **Return to origin server** trip can auto-reconnect to password-protected servers. **Stored in plain text** — leave off unless you need it. |
| `Discord` / `WebhookUrl` | *(empty)* | Discord webhook URL. When set, posts a message on a **server → local** crossing. Empty = disabled. See **[Discord Notifications](Discord-Notifications)**. |

## Per-portal settings (in-game)

Open with **L.Shift + Use** on a `portal_wood` / `portal_stone` (requires build
access):

- **Inter-server flag** — makes the portal switch you to a local world.
- **Link mode** — **Tag pair** (vanilla) or **Network** (mesh with your other
  network portals).
- **Destinations** — the `(label, world)` list for inter-server travel.
- **Lock** — set or clear an entry code.

These are stored with the portal (networked and saved with the world), not in the
config file.

## Notes

- Travel is **portals-only** — there are no travel hotkeys to bind.
- After changing the config file, **fully quit and relaunch** — BepInEx reads config
  at startup and an in-game logout keeps the process alive.

See also: **[Portal Modes & Colours](Portal-Modes-and-Colors)** ·
**[Discord Notifications](Discord-Notifications)**.
