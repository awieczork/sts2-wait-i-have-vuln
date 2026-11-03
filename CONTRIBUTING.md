# Contributing

This guide covers setting up a Windows machine, building, testing, deploying to a local game install, and releasing.
Run the commands from the repository root.

## Set up

1. Clone the repository and install the .NET 9 SDK; the .NET runtime alone cannot build the project.
2. Install Slay the Spire 2 through Steam. The build finds the game on its own through the Steam registry entries or the default Steam library (see `Sts2PathDiscovery.props`).
3. If the build reports that `sts2.dll` was not found, copy `Directory.Build.props.example` to `Directory.Build.props` (ignored by Git) and set `Sts2InstallDir` to your game folder, or pass `-p:Sts2InstallDir="<game folder>"` on the command line.

No Godot editor is needed; the mod ships a DLL only.
The workspace recommends the C# extension for VS Code, but any editor works.

## Layout

| Path | What's in it |
| --- | --- |
| `WaitIHaveVuln.csproj`, `WaitIHaveVuln.sln` | Project and solution; the solution also includes the harness. |
| `WaitIHaveVuln.json` | Mod manifest. `id` is `WaitIHaveVuln`; the display name is `Wait! I Have Vuln!`. |
| `Sts2PathDiscovery.props`, `Directory.Build.props.example` | Game path discovery and the optional local override template. |
| `src/ModEntry.cs` | Initializer; loads the settings, installs the Harmony patches, and owns the `ModId` constant. |
| `src/HookGuard.cs` | Wraps every Harmony entry point, so a mod error is logged as a warning and never reaches the game. |
| `src/Patches/` | Thin Harmony postfixes on the co-op player row lifecycle, the local hand's hover and card pick-up, card plays, and the modding screen's mod details; `PatchTarget.cs` lets each patch class skip itself with one log line when the game renamed its target. |
| `src/Indicator/` | Row icon and speech bubble (`MultiplayerVulnerableIndicator.cs`), the card reminder cloud and the teammates' reactions (`HandReminders.cs`, `Bubbles.cs`), and the engine-independent logic the harness links: the card set, hand state, reminder rules, bubble lines, and cloud placement. |
| `src/Settings/` | Settings panel (`SettingsPanel.cs`, with `GameSliderLook.cs` and `NodeSearch.cs` matching it to the game's controls), current settings (`SettingsService.cs`), solo mode (`SoloMode.cs`), and engine-independent logic (`PlayerSettings.cs`, `SettingsStore.cs`, `SettingsLoadOutcome.cs`). |
| `src/Testing/` | Solo mode patches, which create your own co-op row in single-player runs while solo mode is on. |
| `tests/RegressionHarness/` | Console harness that links the engine-independent sources and drives them with a fake hand and a temporary settings folder. |
| `scripts/` | Player helpers that copy normal saves into the modded profile; described in `docs/modded-saves.md`. Not part of the mod. |
| `docs/` | Player guides linked from the README. |

The project references `sts2.dll`, `GodotSharp.dll`, and `0Harmony.dll` from the game install as non-copy-local references.
The manifest declares no dependencies; BaseLib is not required.

## Build and test

```powershell
dotnet build .\WaitIHaveVuln.csproj
dotnet run --project .\tests\RegressionHarness\WaitIHaveVuln.RegressionHarness.csproj
```

The first build restores the Godot .NET SDK package.
The regular build does not deploy; `DeployMod` defaults to `false`.
The VS Code task `build (safe)` runs the same build.

The harness covers the supported card set, hand transitions, rebinding, suspended tracking, owner cleanup, independent local and remote rows, settings parsing, clamping, and migration, the own-row visibility rule, saving and loading the settings file, when the card cloud and the teammates' reactions show, the bubble lines, and where the cloud goes at the hovered card.
It does not load the game, so it cannot verify resource loading, layout, the settings panel, or multiplayer behaviour.

## Deploy to your game

```powershell
dotnet build .\WaitIHaveVuln.csproj -p:DeployMod=true
```

This copies `WaitIHaveVuln.dll` and `WaitIHaveVuln.json` to `<game>\mods\WaitIHaveVuln` and refuses any other destination.
The VS Code task `build and deploy mod` runs the same build.
Close the game first; it locks the DLL.

Then start the game, accept the mods warning on the first modded launch, enable the mod under Settings > Mod Settings, and look for `[WaitIHaveVuln] Mod initialized.` in `%APPDATA%\SlayTheSpire2\logs\godot.log`.
That line confirms the patches were installed, not that the icon rendered.

### Solo mode

To check the icon, the bubbles, and the card cloud alone, open Settings > Mod Settings from the main menu, select the mod, turn on **Solo mode [dev]**, and then start or continue a single-player run.
The game then creates your own co-op row and treats the missing lobby as connected; the log says `Solo mode [dev] on`.
Solo mode is never saved, so it is off after every game restart, and a change applies from the next run you start or continue.
Your own row then stands for a teammate: hovering an Attack shows the card cloud, and your own Attacks bring its reactions.
With **Show icon on my own row** off, the solo row shows nothing, because it is your own row.
It does not exercise remote hands, hosting, or reconnects.

### Test in the game

- Keep only one copy of the mod installed. The game reads `<game>\mods` before Workshop items and loads only one mod per id, refusing the other copy with a duplicate-id error. Unsubscribe from the Workshop item before testing a deployed build, and delete `<game>\mods\WaitIHaveVuln` before testing the Workshop copy.
- Start the game through Steam, from the library or with `steam.exe -applaunch 2868840`. Starting `SlayTheSpire2.exe` directly fails Steam initialization.
- Change the mod's settings from the main menu only; the game greys out Mod Settings during a run.
- To repeat a combat check without making moves: Save and Quit, change the settings on the main menu, and Continue. Continuing a run saved in combat restarts that combat at turn 1 with the same hand, so the bubble shows again on every Continue.
- A single-player run in progress has to be continued or abandoned before a new one can start; look for Continue on the main menu before planning a fresh run.
- The game keeps five `godot*.log` files and renames the current one at each launch, so copy the log out before a test that restarts the game several times.
- Filter the log on `[WaitIHaveVuln]`. `Asset not cached`, `Progress parse: ... Unknown card ID`, and the leaked RID and ObjectDB lines at exit come from the game. The mod logs `Mod initialized.`, `Settings slider: copied the game's slider look (...)`, `Solo mode [dev] on` or `off`, a `Card reminder:` line for each cloud with its side and the tip boxes it avoided, and a `Reaction:` line with the tier for each angry line. `Skipped <class>: ... not found` means a game update renamed a patched member and that feature is off.

A successful compile and harness run are not runtime verification; co-op behaviour needs a real session.

## Submit a change

- Open an issue first for anything that changes what players see. The mod stays quality-of-life only, and `affects_gameplay: false` in the manifest must stay true to its behaviour.
- Branch from `main`, write commits in Conventional Commits form (`feat:`, `fix:`, `docs:`, `chore:`), and open a pull request against `main`.
- Build and run the harness before pushing.
- Add one bullet under `## Unreleased` in `CHANGELOG.md` for each player-visible result; tooling changes get no bullet.
- Never commit `Directory.Build.props`, game files, decompiled code, save files, or credentials. The repository is public.

## Release (maintainer)

1. Move the `Unreleased` notes to `## [x.y.z] - YYYY-MM-DD`, set the same `version` in `WaitIHaveVuln.json`, and commit as `chore(release): prepare vx.y.z`.
2. Build the release DLL with `dotnet build .\WaitIHaveVuln.csproj --configuration ExportRelease --no-incremental`; the output is `.godot\mono\temp\bin\ExportRelease\WaitIHaveVuln.dll`.
3. Zip `WaitIHaveVuln.dll` and `WaitIHaveVuln.json` as `wait-i-have-vuln-vx.y.z.zip` and test that exact build in the game.
4. Tag `vx.y.z`, push, and create the GitHub release with the ZIP and the changelog section as notes.
5. Upload the same two files to Steam Workshop with Mega Crit's [mod uploader](https://github.com/megacrit/sts2-mod-uploader), using the same notes as the change note and a preview image under 1 MB.

`dotnet clean .\WaitIHaveVuln.csproj` removes build outputs; it does not remove deployed files.
