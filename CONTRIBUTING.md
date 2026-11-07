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
| `src/Box.cs` | A plain rectangle shared by the engine-independent layout rules of the card cloud and the hand preview. |
| `src/Patches/` | Thin Harmony postfixes on the co-op player row lifecycle (one patch class per feature), the local hand's hover and card pick-up, card plays, the game's own speech and thought bubbles, and the modding screen's mod details; `PatchTarget.cs` lets each patch class skip itself with one log line when the game renamed its target. |
| `src/Indicator/` | The icon and the speech bubble on each co-op player row, the card reminder cloud and the teammates' reactions (`HandReminders.cs`, `Bubbles.cs`), and the engine-independent logic the harness links: the card set, the icon level and its changes, which enemies need Vulnerable, the reminder and reaction rules, the waits for the game's own bubbles, the bubble lines, and cloud placement. |
| `src/HandPreview/` | Teammate hand preview on the row's card count (`RowHandPreview.cs`, `HandPreviews.cs`), and the engine-independent logic the harness links: when it opens, the strip layout, and the shown cards. |
| `src/Settings/` | Settings panel (`SettingsPanel.cs`, with `GameSliderLook.cs` and `NodeSearch.cs` matching it to the game's controls), current settings (`SettingsService.cs`), and engine-independent logic (`PlayerSettings.cs`, `SettingsStore.cs`, `SettingsLoadOutcome.cs`). |
| `tests/RegressionHarness/` | Console harness that links the engine-independent sources and drives them with plain values and a temporary settings folder. |
| `scripts/` | Player helpers that copy normal saves into the modded profile, described in `docs/modded-saves.md`, and `launch-coop.ps1` for a local co-op test. Not part of the mod. |
| `docs/` | Player guides linked from the README. |
| `workshop/` | Steam Workshop item text (`workshop.json`) and item id (`mod_id.txt`); the preview and gallery images are kept out of Git. |

The project references `sts2.dll` and `0Harmony.dll` from the game install as non-copy-local references; the Godot SDK version in `WaitIHaveVuln.csproj` pins `GodotSharp` and must match the game's Godot version.
The manifest declares no dependencies; BaseLib is not required.

## Build and test

```powershell
dotnet build .\WaitIHaveVuln.csproj
dotnet run --project .\tests\RegressionHarness\WaitIHaveVuln.RegressionHarness.csproj
```

The first build restores the Godot .NET SDK package.
The regular build does not deploy; `DeployMod` defaults to `false`.
The VS Code task `build (safe)` runs the same build.

The harness covers the supported card set and Mad Science, the icon levels and their changes, when a change may show during an action, which enemies need Vulnerable, the reaction turn, the arrival queue, and the waits for the game's bubbles.
It also covers settings parsing, clamping, and migration, the own-row rule, saving and loading the settings file, the card cloud and reaction rules, the bubble lines and voice, cloud placement, and the hand preview's rule, layout, shown cards, and setting.
It does not load the game, so it cannot verify resource loading, layout, the settings panel, or multiplayer behaviour.
The harness treats compiler warnings as errors, and GitHub Actions runs it on every push to `main` and every pull request; CI does not build the mod itself, because that build needs the game's assemblies.

## Deploy to your game

```powershell
dotnet build .\WaitIHaveVuln.csproj -p:DeployMod=true
```

This copies `WaitIHaveVuln.dll` and `WaitIHaveVuln.json` to `<game>\mods\WaitIHaveVuln` and refuses any other destination.
The VS Code task `build and deploy mod` runs the same build.
Close the game first; it locks the DLL.

Then start the game, accept the mods warning on the first modded launch, enable the mod under Settings > Mod Settings, and look for `[WaitIHaveVuln] Mod initialized.` in `%APPDATA%\SlayTheSpire2\logs\godot.log`.
That line confirms the patches were installed, not that the icon rendered.

### Local co-op

The mod draws only on co-op rows, so check it in the game with two copies on one PC, one hosting and one joining over `127.0.0.1`.
Close the game, then start the host from the repository root and wait for its co-op character select:

```powershell
pwsh -NoProfile -File .\scripts\launch-coop.ps1 -Role Host
pwsh -NoProfile -File .\scripts\launch-coop.ps1 -Role Client
```

The commands use PowerShell 7 (`pwsh`); with Windows PowerShell, run `powershell -ExecutionPolicy Bypass -NoProfile -File` with the same arguments.
The script finds the game through Steam's main library; pass `-GameDir <game folder>` to both commands when the game is in another Steam library.
The second command starts a window titled "Slay The Spire 2 (Client)" that joins the lobby; pick characters and play in both windows.
Both copies start outside Steam (`--force-steam off`), so the players are "Test Host" and "Test Client 1", and each has its own profile under `%APPDATA%\SlayTheSpire2\default\<id>`, apart from your Steam saves.
`-NoMods` starts the client without mods, as a teammate who does not have the mod.

- Each new test profile skips the mod on its first start, because the mods warning is hidden behind the co-op screen: back out to the main menu, accept the warning, quit, and start that copy again. The log then shows `Mod initialized.`.
- The host does not notice when the client quits and refuses it when it returns with the same id, so quit and restart both copies.
- The logs are `coop-host.log` and `coop-client.log` in `%APPDATA%\SlayTheSpire2\logs`, and each start overwrites its file.
- Both windows open at 1920 × 1080; resize them, and each profile keeps its size.

### Test in the game

- Keep only one copy of the mod installed. The game reads `<game>\mods` before Workshop items and loads only one mod per id, refusing the other copy with a duplicate-id error. Unsubscribe from the Workshop item before testing a deployed build, and delete `<game>\mods\WaitIHaveVuln` before testing the Workshop copy.
- Start the game through Steam, from the library or with `steam.exe -applaunch 2868840`. Starting `SlayTheSpire2.exe` directly fails Steam initialization, unless you start it with `--force-steam off` as local co-op does.
- Change the mod's settings from the main menu only; the game greys out Mod Settings during a run.
- The game keeps five `godot*.log` files and renames the current one at each launch, so copy the log out before a test that restarts the game several times.
- Filter the log on `[WaitIHaveVuln]`. `Asset not cached`, `Progress parse: ... Unknown card ID`, and the leaked RID and ObjectDB lines at exit come from the game. The mod logs `Mod initialized.`, `Settings slider: copied the game's slider look (...)`, an `Arrival bubble:` line for each "Wait! I have vuln!" bubble (shown, waiting for the game's bubble, or skipped with the reason, such as no enemy needing Vulnerable), a `Card reminder:` line for each cloud with its side and the tip boxes it avoided, and a `Reaction:` line with the tier for each angry line. `Skipped <class>: ... not found` means a game update renamed a patched member and that feature is off.

A successful compile and harness run are not runtime verification; co-op behaviour needs a real session, such as local co-op.

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
5. Upload the same two files to Steam Workshop with Mega Crit's [mod uploader](https://github.com/megacrit/sts2-mod-uploader), using the description in `workshop/workshop.json`, the same notes as the change note, and a preview image under 1 MB.

`dotnet clean .\WaitIHaveVuln.csproj` removes build outputs; it does not remove deployed files.
