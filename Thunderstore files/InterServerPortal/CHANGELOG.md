# Changelog

## 0.9.1 — Icon refresh

- Updated the mod icon.
- No gameplay changes. (Note: the glowing rune characters on the portal frame
  keep their vanilla colour — that colour is baked into the portal's texture and
  can't be re-tinted, the same as the orange flame burst. The frame glow, swirl
  vortex, sparks and lights still carry the per-mode colour.)

## 0.9.0 — Per-mode portal glow & Discord

- **New: per-mode portal glow.** Each portal now glows a colour for its mode —
  vanilla tag = blue, network = **violet**, inter-server = **cyan** — and, like a
  vanilla portal, only lights up when it's actually connected/usable (a lone
  network portal or a destination-less inter-server portal sits dark). The inner
  swirl vortex is coloured to match. (The outward flame burst stays orange — its
  shader can't be recoloured.)
- **New: optional Discord notification.** Set `Discord/WebhookUrl` in the config
  and a message is posted whenever you cross from a server into your local world.

## 0.8.0 — Same-world portal networks

- **New: portal networks.** Set a portal's link mode to **Network** and it meshes
  with every other network-mode portal you built in that world. Walk through one to
  pick any of the others from a menu and teleport there — an ordinary in-world
  teleport, no loading screen. Works alongside vanilla 1:1 tag pairing (your other
  portals are unaffected). Works in single-player and on dedicated servers.
- **Removed the item-policy setting.** `portal_wood` and `portal_stone` now keep
  their fixed vanilla item restrictions (wood blocks ore, stone carries it) with no
  override — simpler and predictable.
- **Removed the travel hotkeys.** Travel is portals-only now.

## 0.7.0 — Polish & packaging

- First Thunderstore package: manifest, icon, README, and this changelog.
- Reorganized config: the debug/fallback hotkeys moved to a **Hotkeys** section
  and are now **off by default** behind `EnablePrototypeHotkeys`. Portals are the
  intended way to travel; the hotkeys remain as a testing/keyboard fallback and
  still drive the empty-config fallback destination.

## 0.6.0 — Item policy & portal_stone

- `portal_stone` support (all behavior applies to both prefabs).
- Wood/stone item policy: `portal_wood` blocks teleport-restricted items (ores),
  `portal_stone` carries them. A per-portal `ISP.policy` override (Auto / Block /
  Allow) is set from a new dropdown in the config panel. Blocking reuses vanilla's
  own teleportability check and names the offending items.

## 0.5.0 — Lock codes

- Optional per-portal entry code (salted SHA-256; raw code never stored), set/clear
  in the config panel, masked entry prompt before travel, and an escalating
  wrong-attempt throttle with a short "recently unlocked" memory.

## 0.4.0 — Hub routing

- Per-portal destination list `(label, world)`, a native config/editor panel
  (alt-use) and a travel selection menu with a single-destination fast path.
- Unavailable destinations are greyed with a reason. Local→local hops between your
  own seeds, and a "Return to origin server" entry inside a local world.

## 0.3.0 — Destination validation & failure handling

- Validates the target local world before leaving (missing / corrupt / wrong
  version) and shows a centered reason instead of stranding you. A mid-flight
  failure reconnects to the remembered origin server.

## 0.2.0 — Portal integration

- Flag a portal inter-server via a custom ZDO field; flagged portals drive the
  world switch by walk-through, unflagged portals stay vanilla. Arrival-bounce
  guard so you don't loop between worlds on spawn.

## 0.1.0 — Core world-switch

- The core disconnect → load-local-world → return sequence, with a persisted
  return registry (origin server IP:port) that survives a crash/restart.
