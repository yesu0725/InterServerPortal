# Getting Started

This walks you through building your first **inter-server portal** and travelling
through it. If you just want to link portals inside one world, see
**[Same-World Networks](Same-World-Networks)** instead.

## What you need

- InterServerPortal installed (see **[Installation](Installation)**).
- At least one **local single-player world** to travel to. Create one from the main
  menu if you don't have one — note its exact display name.
- A portal (`portal_wood` or `portal_stone`) built where you want the doorway.

## The two ways to interact with a portal

| Action | Keys | What it does |
|---|---|---|
| **Use** | `E` | Vanilla behaviour — set the portal's pairing tag |
| **Alt-use** | **L.Shift + `E`** | Open the InterServerPortal config panel |
| **Walk through** | step into it | Travel (branches by the portal's mode) |

Alt-use requires build access — the same as editing the portal tag.

## Step 1 — Flag a portal inter-server

1. Stand on the dedicated server, in front of your portal.
2. Hold **L.Shift** and press **Use** (`E`) to open the config panel.
3. Turn on the **inter-server** flag.

## Step 2 — Add a destination

1. In the same panel, add a destination:
   - **Label** — a friendly name shown in the travel menu.
   - **World** — the exact name of your local single-player world.
2. Close the panel. The portal now glows **cyan** (its inter-server colour) and
   lights up because it has a usable destination.

## Step 3 — Travel

- **Walk through** the portal.
  - One destination → you go straight there.
  - Multiple destinations → a menu opens; pick one.
- One loading screen later, you're standing in your local world.

## Step 4 — Come back

In your local world, a portal offers a **Return to origin server** option when you
walk through it. Selecting it reconnects you to the server you came from.

> The origin server is remembered even across a game restart, so the return trip
> still works later. For password-protected servers, see the
> `RememberServerPassword` setting in **[Configuration](Configuration)**.

## Tips

- The portal is **shared**: every player who walks through the same inter-server
  portal goes to *their own* local world. Others just see you leave the server.
- Standing on a portal when you arrive won't bounce you back — travel re-arms only
  after you step clear of the portal.
- If a destination world is missing, corrupt, or the wrong version, you're told up
  front and not stranded. See **[FAQ & Troubleshooting](FAQ-and-Troubleshooting)**.

Next: **[Portal Modes & Colours](Portal-Modes-and-Colors)**.
