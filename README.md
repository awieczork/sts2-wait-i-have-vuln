# CoopVulnIndicator

This repository contains a minimal Windows C# mod workspace for Slay the Spire 2.
The initializer only writes a log message; it does not add gameplay behavior, UI, or Harmony patches.
Project-wide Copilot guidance is in [.github/copilot-instructions.md](.github/copilot-instructions.md).

## Changes and releases

Use the [commit workflow](.github/skills/commit/SKILL.md) for reviewed local commits and the [publish workflow](.github/skills/publish/SKILL.md) for tagged releases.
Player-facing changes are recorded in [CHANGELOG.md](CHANGELOG.md).

## Prerequisites

- Windows x64.
- The .NET 9 SDK. The .NET runtime alone is not sufficient to compile this project.
- Slay the Spire 2 installed locally, with its managed assemblies under `data_sts2_windows_x86_64`.
- The game version currently configured in `mod_manifest.json` is `0.107.1`.

The current local configuration points to `C:\Steam\steamapps\common\Slay the Spire 2`.
For another machine, copy `Directory.Build.props.example` to `Directory.Build.props` and edit `Sts2InstallDir` to the game's install directory.
`Directory.Build.props` is intentionally ignored by Git; keep the example available for other workspaces.

The manifest's author is the explicit placeholder `REPLACE_WITH_AUTHOR`; replace it before distributing the mod.

## Editor tooling

The [workspace extension recommendations](.vscode/extensions.json) recommend Microsoft's C# extension.
No editor extension is required to run `dotnet build`.
Godot scene and GDScript tooling is not needed for this log-only C# mod.

VS Code installs extensions into a user profile, not an individual workspace.
Workspace recommendations do not install extensions or change profile synchronization.
The Install (Do not Sync) action avoids extension synchronization but still installs into a profile, so it is not workspace-only isolation.

## Build and deploy

For initial setup or dependency changes, restore packages as a separate step after approving downloads:

```powershell
dotnet restore .\CoopVulnIndicator.csproj
```

The default build uses restored packages and never deploys to the game directory:

```powershell
dotnet build .\CoopVulnIndicator.csproj --no-restore
```

To explicitly build and deploy, use the `build and deploy mod` VS Code task or run:

```powershell
dotnet build .\CoopVulnIndicator.csproj --no-restore -p:DeployMod=true
```

Deployment validates the game install and requires the destination to be exactly `mods\CoopVulnIndicator` beneath that install.
It copies only `CoopVulnIndicator.dll` and the source `mod_manifest.json`, renamed to `CoopVulnIndicator.json`.
All game assembly references are non-copy-local, so game DLLs and Godot bindings are not deployed as mod dependencies.

To remove workspace build outputs, run the `clean` VS Code task or:

```powershell
dotnet clean .\CoopVulnIndicator.csproj
```

Clean does not delete deployed mod files.

## Load check

On the first modded launch, acknowledge the game's mods warning; the game skips loading mods until that warning has been seen.
After a successful explicit deployment, enable the mod in the game's Settings > Mod Settings and restart the game if prompted.
Check the game output/log for `[CoopVulnIndicator] Mod initialized.`
The current Windows log is `%APPDATA%\SlayTheSpire2\logs\godot.log`.
That message is emitted by the attributed initializer through the game's `Log.Info` API.

The local .NET 9 SDK is verified as `9.0.319`.
The no-restore build passed with deployment disabled, and invalid deployment destinations were rejected without creating a folder.
The existing package cache was used; no new restore was run by the agent.
The approved deployment passed: the game mod folder contains exactly the mod library and renamed manifest, both matching the workspace files by hash.
In-game loading passed after the first-run mods warning was acknowledged and the game restarted.
The fresh game log showed the assembly loading, the initializer call, `[CoopVulnIndicator] Mod initialized.`, and completed mod initialization.
