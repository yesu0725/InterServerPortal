# Same-World Networks

A **portal network** meshes all of *your* portals inside one world. Walk through any
network portal and pick any of your other network portals from a menu — you teleport
straight there. It's an ordinary in-world teleport: **no world switch, no loading
screen.**

This is separate from inter-server travel and works in single-player and on
dedicated servers.

## How it differs from vanilla portals

- **Vanilla:** portals pair **1:1** by matching tag. Two portals, one link.
- **Network:** every network-mode portal you built joins one **mesh**. Walk through
  one → choose from *all* the others. No tags to juggle.

Your vanilla tag-paired portals are unaffected — you can mix both in the same world.

## Setting it up

1. **Alt-use** (**L.Shift + Use**) a portal to open the config panel.
2. Set **Link mode → Network**.
3. Repeat on at least one more portal **you built**.

A network portal glows **violet**. It only lights up (active) once there is **at
least one other** network portal in your mesh — a lone network portal stays dark.

## Travelling the network

- **Walk through** a network portal.
- A menu lists your **other** network portals, **nearest first**, with the distance
  shown. Each portal's **tag** (set with plain **Use** / `E`) is used as its name in
  the menu, so tag your portals to label them.
- Pick one → you're teleported there in-world.

Landing on the destination portal won't immediately re-open the menu — there's a
short arrival cooldown.

## Who owns a network?

A network is **per-player**: it's made of every network-mode portal built by the
same character (by the piece's builder). Your network is yours; another player's
network portals are theirs.

## Locks and items

Network travel respects the same gates as inter-server travel:

- A **locked** network portal prompts for its code first
  (see **[Entry Codes & Locks](Entry-Codes-and-Locks)**).
- The **[Item Policy](Item-Policy)** applies — a wood network portal refuses
  teleport-restricted items (ore/metal); a stone one carries them.

## Tips

- Name every network portal with a clear tag (**Use** / `E`) so the menu is easy to
  read.
- On a dedicated server, portals in **unloaded** parts of the map are still found
  (the server is queried), so distant portals aren't missed.

See also: **[Portal Modes & Colours](Portal-Modes-and-Colors)**.
