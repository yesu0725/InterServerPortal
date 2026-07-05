# Discord Notifications

InterServerPortal can post a message to Discord whenever a player crosses **from a
server into their own local world**. It's off by default and enabled by setting a
webhook URL.

## Enable it

1. In Discord: **Server Settings → Integrations → Webhooks → New Webhook**, pick a
   channel, and **Copy Webhook URL**.
2. Open `BepInEx/config/com.interserverportal.cfg` and set:

   ```ini
   [Discord]
   WebhookUrl = https://discord.com/api/webhooks/…
   ```

3. Restart the game to pick up the change.

Set this on the **client** — the notification is sent from the travelling player's
game, not from the server.

## The message

```
🌀 <player> stepped through a portal to their local world <world> (from <server>).
```

- `<player>` — the traveller's character name
- `<world>` — the destination local world
- `<server>` — the server world they left

## When it fires

- **Only** on a genuine **server → local** crossing (you were connected to a real
  server).
- A **local → local** hop does **not** post.
- Leave `WebhookUrl` empty to disable entirely (no posts, no errors).

## Troubleshooting

- **No message appears:**
  - Confirm the URL is set in the config file of the **profile you actually launch**.
  - Turn on `[General] DebugLogging = true` and check the BepInEx log — a success
    logs `Discord travel notification sent`, a failure logs `Discord notify failed:`
    with the reason.
- **`Discord notify failed` with an HTTP error:** the webhook URL is wrong, deleted,
  or rate-limited. Re-copy the URL from Discord.
- The request is sent on a background connection with an explicit TLS handshake, so
  it works on Valheim's runtime and completes even though the game immediately loads
  your local world.

See also: **[Configuration](Configuration)**.
