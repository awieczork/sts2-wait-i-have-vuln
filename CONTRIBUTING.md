# Contributing

This guide prepares a Windows x64 development machine for this private repository and records its local build, test, and optional deployment procedures.
Run PowerShell commands from the repository root unless noted otherwise.

## Prepare a machine

1. Obtain authorized access to the private repository, clone it with your approved Git method, and open the repository root in your editor.
2. Install the .NET 9 SDK; the .NET runtime alone cannot build the project.
3. Install Slay the Spire 2 locally and locate its managed assemblies under `data_sts2_windows_x86_64`.
4. Inspect `Directory.Build.props.example` and create the ignored local `Directory.Build.props` only when it does not already exist:

   ```powershell
   if (-not (Test-Path -LiteralPath .\Directory.Build.props)) {
       Copy-Item -LiteralPath .\Directory.Build.props.example -Destination .\Directory.Build.props
   }
   ```

5. Edit `Sts2InstallDir` in the local props file to the game install directory on this machine.
6. Keep the template's derived paths aligned with the local install: `Sts2DataDir` points to `$(Sts2InstallDir)\data_sts2_windows_x86_64`, and `Sts2ModsDir` points to `$(Sts2InstallDir)\mods\$(MSBuildProjectName)`.

Do not overwrite an existing `Directory.Build.props` while preparing another machine; inspect and update only the local values that need changing.
The tracked example is a template, not a universal game install path.
Keep machine-specific paths in the ignored local file rather than tracked configuration.

The current project targets `net9.0` and uses `Godot.NET.Sdk` version `4.5.1`.
The local SDK version previously verified for this workspace was `9.0.319`; the repository does not pin an SDK patch version.
No Godot editor or GDScript tooling is required for this C# build.
The workspace recommends Microsoft's C# extension, but no editor extension is required to run the build.

## Project layout and identity

- `WaitIHaveVuln.csproj` and `WaitIHaveVuln.sln` are the root project and solution.
- `WaitIHaveVuln.json` is the single mod manifest; its `id` is `WaitIHaveVuln` and its display `name` is `wait-i-have-vuln`.
- `src/ModEntry.cs` owns the initializer and the single internal `ModId` identity constant.
- `src/Patches/MultiplayerPlayerStatePatches.cs` contains the thin multiplayer row lifecycle patches.
- `src/Indicator/MultiplayerVulnerableIndicator.cs` owns row-level UI and lifecycle integration.
- `src/Indicator/VulnerableIndicatorState.cs` contains the engine-independent eligibility predicate and hand state.
- `tests/RegressionHarness/WaitIHaveVuln.RegressionHarness.csproj` links the production state source into the fake-hand regression harness.
- `scripts/copy-vanilla-saves.ps1` and `scripts/copy-vanilla-saves.sh` are player-facing helpers that back up the account save folder and copy a normal profile's progress into the matching modded profile; the README describes their use. They are not part of the mod payload. They were checked against a fake save tree under PowerShell 7, Windows PowerShell 5.1, and Git Bash, not against real game saves.

The project references the installed game assemblies `sts2.dll`, `GodotSharp.dll`, and `0Harmony.dll` from `Sts2DataDir`.
Those references are non-copy-local and are not deployment dependencies.
The manifest declares no dependencies; this project does not require BaseLib.
The manifest currently declares version `0.1.0` and minimum game version `0.107.1`; neither value establishes compatibility with newer game versions.

## Restore, build, and test

On a fresh machine or after a dependency change, restore the main project separately:

```powershell
dotnet restore .\WaitIHaveVuln.csproj
```

This restore may download the Godot .NET SDK or other required packages.
An agent must obtain approval before running a restore that may download packages.

After restore, the regular build does not restore packages and does not deploy to the game directory; `DeployMod` defaults to `false`:

```powershell
dotnet build .\WaitIHaveVuln.csproj --no-restore
```

The VS Code task `build (safe)` runs this build.

On a fresh machine, restore the harness project separately as well:

```powershell
dotnet restore .\tests\RegressionHarness\WaitIHaveVuln.RegressionHarness.csproj
```

Then run its checks without another restore:

```powershell
dotnet run --project .\tests\RegressionHarness\WaitIHaveVuln.RegressionHarness.csproj --no-restore
```

The harness links the production eligibility predicate and hand-state implementation and uses a fake hand without executing game code.
It covers the supported-model boundary, hand transitions, rebinding, interrupted tracking, owner cleanup, and independent local/remote row state.
It does not verify game loading, resource loading, rendered row fit, multiplayer host/join behavior, or reconnect freshness.

## Local configuration and generated files

Git transfers tracked source, project and manifest files, tests, documentation, the props example, and the workspace's VS Code settings, tasks, and extension recommendations.
It does not transfer the ignored machine-local `Directory.Build.props`, generated `.godot/mono/temp`, `bin`, or `obj` outputs, or ignored `.github/context/` task history.
The workspace extension recommendation does not install an extension; VS Code extensions and personal Copilot plugins belong to a user profile, not this repository.

Do not put secrets, access tokens, Steam account information, game binaries or assets, decompiled game content, or save files in tracked documentation or configuration.
Private repository access does not make the repository a secret store.

## Optional deployment and load check

Deployment is explicit and is not part of the regular build.
Only run the command below when deployment to this machine's game install is intended and approved:

```powershell
dotnet build .\WaitIHaveVuln.csproj --no-restore -p:DeployMod=true
```

The VS Code task `build and deploy mod` runs the same opt-in operation.
The project verifies that `Sts2ModsDir` is exactly `mods\WaitIHaveVuln` beneath `Sts2InstallDir`.
It copies only `WaitIHaveVuln.dll` and `WaitIHaveVuln.json` to that folder; game assemblies and Godot bindings are not copied.

### Solo test row

For local testing without a co-op session, build with the opt-in `SoloTestRow` property, which defaults to `false`:

```powershell
dotnet build .\WaitIHaveVuln.csproj --no-restore -p:DeployMod=true -p:SoloTestRow=true
```

The VS Code task `build and deploy mod (solo test row)` runs the same operation.
This defines `SOLO_TEST_ROW`, which lets the game create the local player's co-op row in single-player runs and treats a missing lobby as connected, so the indicator can be checked on that row.
Without a lobby, the test row's nameplate shows the local platform player name instead of the net id.
The log then also contains `[WaitIHaveVuln] Solo test row enabled (test build).`.
A test build changes single-player presentation and must not be distributed; rebuild and redeploy without the property to restore the regular build.
It does not exercise remote hands, host/join, or reconnect behavior.

The internal identity change does not remove an older `CoopVulnIndicator` installation.
Do not enable both identities at once, and do not delete or modify game files based on this guide.

On the first modded launch, acknowledge the game's mods warning; the game skips loading mods until that warning has been seen.
After an explicit deployment, enable the mod in Settings > Mod Settings and restart the game if prompted.
Check `%APPDATA%\SlayTheSpire2\logs\godot.log` for `[WaitIHaveVuln] Mod initialized.`.
That message confirms the initializer installed the Harmony patches; it does not verify that the icon loaded or rendered correctly.

The solo test row has been checked in-game for icon loading, row fit, Bash draw/play transitions, and the speech bubble.
Joining an unmodded co-op host succeeds with a logged non-gameplay mod mismatch warning; hosting with the mod and reconnects have not been checked.
Compile and harness success are not substitutes for runtime, resource, layout, host/join, or reconnect checks.

## Clean and release procedures

Run the VS Code task `clean` or this command to remove workspace build outputs:

```powershell
dotnet clean .\WaitIHaveVuln.csproj
```

Clean does not remove deployed mod files.
Use the [commit workflow](.github/skills/commit/SKILL.md) for reviewed local commits and the [publish workflow](.github/skills/publish/SKILL.md) for tagged releases.
Player-facing changes belong in [CHANGELOG.md](CHANGELOG.md).
The manifest author is still `REPLACE_WITH_AUTHOR`; the owner must provide the correct identity before distribution.
