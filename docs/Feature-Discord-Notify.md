# Discord Travel Notification

Post a Discord message whenever a player steps through a portal from a **server**
into their own **local world**. Implemented in
[`src/Net/DiscordNotifier.cs`](../src/Net/DiscordNotifier.cs); fired from
`WorldSwitcher.RequestSwitchToLocal`.

## Config

`BepInEx/config/com.interserverportal.cfg` (in the launched profile):

```ini
[Discord]
WebhookUrl = https://discord.com/api/webhooks/…
```

- Set it on the **client** — the notification fires from the travelling player's
  client, not the server.
- Empty (default) → disabled, no posts, no errors.

## Message

```
🌀 <player> stepped through a portal to their local world <world> (from <server>).
```

`<player>` = `Player.GetPlayerName()`, `<world>` = the destination local seed,
`<server>` = the world name of the server being left.

## When it fires

Only on a genuine **server → local** switch: the call is gated to
`IsRemoteClient()` (we're a client connected to a real server). A **local→local**
hop does **not** notify. It's sent just before the session is torn down for the
switch, while the local player + connection still exist, so the player name and
origin server can be read.

## Delivery

The POST runs on a **background thread** (`ThreadPool`), not a Unity coroutine, so
it:

- completes independently of the world-switch scene reload that the travel kicks
  off immediately afterwards, and
- can force TLS explicitly. Valheim's Mono runtime ships without a usable CA root
  store, so the default certificate validation rejects Discord's cert. We set
  `ServicePointManager.SecurityProtocol |= Tls12` and add an accept-all
  `ServerCertificateValidationCallback` (a webhook URL is a low-risk,
  user-supplied endpoint).

Failures are logged as a warning (with the HTTP status/body when available);
success is logged under `DebugLogging`.
