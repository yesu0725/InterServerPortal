# Development Setup

Mirrors the toolchain used by the sibling mods in `E:\Valheim Modding`
(Dvergr Expanded, ValheimServerGuide).

## Stack

| Component | Value |
|---|---|
| Loader | BepInEx 5 (denikson BepInExPack_Valheim 5.4.2333) |
| Patching | HarmonyX (bundled with BepInEx 5) |
| Framework helper | Jötunn 2.25.0 (custom pieces/UI/config helpers) |
| Target framework | net48 |
| Publicizer | BepInEx.AssemblyPublicizer.MSBuild — `Publicize=true` on `assembly_valheim` |
| Language | C# |

## References

Publicized `assembly_valheim.dll` is available under sibling projects, e.g.
`E:\Valheim Modding\Dvergr Expanded\src\obj\Release\publicized\assembly_valheim.dll`.
The `.csproj` should reference the game's managed assemblies (via publicizer)
plus BepInEx and Jötunn. Copy the reference/publicizer setup from
`Dvergr Expanded/src/LostScrollsII.csproj` or ServerGuide's csproj as a template.

## Project skeleton

```
InterServerPortal/
├── CLAUDE.md
├── docs/                 (this knowledge base)
├── src/
│   ├── InterServerPortal.csproj
│   ├── Plugin.cs         [BepInPlugin("com.interserverportal", ...)] + [BepInDependency(Jotunn)]
│   ├── Core/  Portal/  Hub/  Security/  Policy/  Net/
│   └── ...
├── Thunderstore files/   (manifest.json, icon.png, README.md, CHANGELOG.md, DLL)
└── dist/                 (built `InterServerPortal-<version>.zip`, git-ignored)
```

## Plugin bootstrap essentials

- `[BepInPlugin(GUID, Name, Version)]` with `GUID = com.interserverportal`.
- `[BepInDependency(Jotunn.Main.ModGuid)]`.
- Harmony `PatchAll` in `Awake`.
- BepInEx `ConfigFile` for user settings (hotkeys, default policies, UI toggles).

## Building & deploy

Standard `dotnet build -c Release`. The publicizer restores the game assemblies
on first build. The `.csproj` copies the built DLL, `AfterTargets="Build"`, to
three locations (each guarded by an `Exists` check):

- Steam `BepInEx\plugins\InterServerPortal`
- r2modman profile — **default `R2MODMAN_PROFILE_DIR` = "Hearthbound Valheim -
  Test"** (this is the profile actually used for testing; override with
  `-p:R2MODMAN_PROFILE_DIR="..."`).
- Dedicated server `BepInEx\plugins\InterServerPortal`.

> **Deploy gotcha (cost us a debugging cycle):** there are several r2modman
> profiles. Deploying to the wrong one leaves a stale DLL in the profile you
> actually launch. If behavior doesn't match the code, first check the on-disk
> DLL timestamp/size in the launched profile matches the build output.

## Packaging for Thunderstore (Phase 7)

Static package files live in `Thunderstore files/InterServerPortal/`:
`manifest.json`, `icon.png` (256×256 RGBA), `README.md`, `CHANGELOG.md`. Every
`Release` build copies the fresh DLL into that folder (`DeployToThunderstore`).

To produce the upload archive:

```
dotnet build src/InterServerPortal.csproj -c Release -t:PackThunderstore
```

This zips the package folder to `dist/InterServerPortal-<version>.zip` with all
files at the **archive root** (Thunderstore requires `manifest.json`, `icon.png`,
`README.md` at the top level — not inside a subfolder). `manifest.json`
dependencies are pinned to `denikson-BepInExPack_Valheim-5.4.2333` and
`ValheimModding-Jotunn-2.25.0` (the version floor the mod was built/tested
against; newer Jötunn — e.g. 2.29.x in the test profile — works fine).

Bump `<Version>` in the `.csproj`, `PluginVersion` in `Plugin.cs`, and
`version_number` in `manifest.json` together before packaging.

## Reload gotcha — BepInEx loads plugins only at process start

Editing/redeploying the DLL does **not** hot-reload. Valheim's in-game "Log Out"
(and our world switch) returns to the menu but keeps the process — and the old
assembly — alive. **Fully quit to desktop and relaunch** to pick up a new build.
Symptom of forgetting: a stack trace referencing code paths you already changed.

## Decompiling the game (for API discovery)

`ilspycmd` (dotnet global tool, installed) is the fastest way to read real
method bodies when reflection isn't enough:

```
cd ".../Valheim/valheim_Data/Managed"
ilspycmd -t FejdStartup assembly_valheim.dll > fejd.cs
```

This is how the `FejdStartup.JoinServer` async-matchmaking behavior and
`TransitionToMainScene` load condition were confirmed (see
[Core-Mechanic-World-Switching](Core-Mechanic-World-Switching.md) → Findings).

## Testing environment

Two worlds are needed: a dedicated server reachable by **IP** (a local
`127.0.0.1:2456` is fine) and at least one local single-player world as the
destination. Set `Prototype/LocalWorldName` to that world's display name. See
[Roadmap-Phases](Roadmap-Phases.md) for the phased test checklist.

## Verify API names

Valheim renames fields/methods between updates. Before relying on any
`ZNet`/`Game`/`FejdStartup`/`TeleportWorld`/`SaveSystem` member, confirm it
against the current publicized assembly (reflection or `ilspycmd`) rather than
trusting these docs verbatim. A verified snapshot lives in project memory
`interserverportal-worldswitch-api`.
