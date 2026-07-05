# Item Policy

InterServerPortal keeps Valheim's **vanilla item rules** for portals, and applies
them to inter-server and network travel too.

## The rule

| Portal | Teleport-restricted items (ores, metals, etc.) |
|---|---|
| **`portal_wood`** | **Blocked** — you can't carry them through |
| **`portal_stone`** | **Allowed** — carries everything (Ashlands stone portal) |

This is **fixed by prefab** — there is no per-portal override to configure. What a
wood or stone portal carries is exactly what it carries in vanilla; the mod just
enforces the same rule when you travel between worlds or across your network.

## What happens if you're blocked

If you walk through a **wood** portal while carrying restricted items, travel is
refused and you get a centered message naming the offending items. Drop or store
them (or use a **stone** portal) and try again.

## When it's checked

The item check runs **just before** the switch/teleport — after any lock-code
prompt. So the order is:

1. Lock code (if the portal is locked)
2. Item policy (wood refuses ore)
3. Travel

## Tips

- Use **`portal_stone`** for inter-server or network portals you intend to move
  ore/metal through.
- The check reuses Valheim's own "is this teleportable?" logic, so it matches what
  you'd expect from normal portals.

See also: **[Inter-Server Travel](Inter-Server-Travel)** ·
**[Same-World Networks](Same-World-Networks)**.
