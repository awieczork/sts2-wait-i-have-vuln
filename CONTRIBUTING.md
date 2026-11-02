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
| `src/ModEntry.cs` | Initializer; installs the Harmony patches and owns the `ModId` constant. |
| `src/Patches/` | Thin Harmony postfixes on the co-op player row lifecycle. |
| `src/Indicator/` | Row UI (`MultiplayerVulnerableIndicator.cs`) and engine-independent logic (`VulnerableCardPredicate.cs`, `ICardHand.cs`, `HandIndicatorState.cs`). |
| `src/Testing/` | Solo test row patches, compiled only with `-p:SoloTestRow=true`. |
| `tests/RegressionHarness/` | Console harness that links the engine-independent sources and drives them with a fake hand. |
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

The harness covers the supported card set, hand transitions, rebinding, suspended tracking, owner cleanup, and independent local and remote rows.
It does not load the game, so it cannot verify resource loading, layout, or multiplayer behaviour.

## Deploy to your game

```powershell
dotnet build .\WaitIHaveVuln.csproj -p:DeployMod=true
```

This copies `WaitIHaveVuln.dll` and `WaitIHaveVuln.json` to `<game>\mods\WaitIHaveVuln` and refuses any other destination.
The VS Code task `build and deploy mod` runs the same build.
Close the game first; it locks the DLL.

Then start the game, accept the mods warning on the first modded launch, enable the mod under Settings > Mod Settings, and look for `[WaitIHaveVuln] Mod initialized.` in `%APPDATA%\SlayTheSpire2\logs\godot.log`.
That line confirms the patches were installed, not that the icon rendered.

### Solo test row

```powershell
dotnet build .\WaitIHaveVuln.csproj -p:DeployMod=true -p:SoloTestRow=true
```

This compiles `src/Testing/` in and defines `SOLO_TEST_ROW`: the game creates the local player's co-op row in single-player runs and treats a missing lobby as connected, so you can check the icon and the bubble alone.
The VS Code task `build and deploy mod (solo test row)` runs the same build, and the log then also says `Solo test row enabled (test build)`.
A test build changes single-player presentation and must never be distributed; rebuild without the property afterwards.
It does not exercise remote hands, hosting, or reconnects.

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
