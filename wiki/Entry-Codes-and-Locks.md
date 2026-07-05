# Entry Codes & Locks

You can protect any inter-server or network portal with an **entry code**. A locked
portal prompts for its code before it will travel.

## Setting a code

1. **Alt-use** (**L.Shift + Use**) the portal to open the config panel.
2. In the **Lock** section, enter a code and confirm.
3. Close the panel. The portal's hover text now shows **`[locked]`**.

To remove the lock, open the panel and clear the code.

## Using a locked portal

- **Walk through** a locked portal → a **masked** code prompt appears.
- Enter the correct code → you travel.
- The code is **remembered** for a short while after a correct entry, so you're not
  re-prompted on every use.

## Wrong attempts

- Wrong codes are throttled: after failed attempts the portal makes you **wait**
  before trying again, and the remaining cooldown is shown.
- The delay escalates with repeated wrong attempts, then resets.

## How your code is stored

- The **raw code is never stored.** Only a random **salt** and a **SHA-256 hash** of
  `salt + code` are saved with the portal.
- Verification re-hashes what you type and compares — so the plain code isn't kept
  anywhere, on disk or over the network.

## Notes

- The lock is **per-portal** (tied to that specific portal), so different portals can
  have different codes.
- Locks apply to both **inter-server** and **network** travel.
- The lock gates *travelling through* the portal. Editing the portal's config still
  requires **build access** to the area, as usual.

See also: **[Inter-Server Travel](Inter-Server-Travel)** ·
**[Same-World Networks](Same-World-Networks)**.
