# wait-i-have-vuln

This private repository contains the source for a Slay the Spire 2 quality-of-life mod.
The public name is `wait-i-have-vuln`; the project, assembly, and manifest identity use `WaitIHaveVuln`.

## What it does

During connected co-op combat, the mod shows the existing Vulnerable power icon on a player's row when that player's current hand contains at least one supported card model.
The viewing player's own row is included.
The icon reports hand presence only; it does not account for energy or whether a valid target is available.
Single-player presentation is unchanged, and the mod does not change gameplay state, network messages, or save data.

The supported card models are exactly `Assassinate`, `Bash`, `BeamCell`, `Break`, `Comet`, `Dominate`, `Expose`, `FallingStar`, `Fear`, `GammaBlast`, `HighFive`, `KnowThyPlace`, `MeteorShower`, `Putrefy`, `Shockwave`, `Squash`, `Taunt`, `Thunderclap`, `Tremble`, and `Uppercut`.

When combat or hand data is unavailable, or a player disconnects, that row retains its last observed result, including a hidden result.
A visible icon can therefore be stale until hand tracking resumes; live reconnect freshness has not been verified.

## Repository context

This is a C# source workspace with one project and solution, a mod manifest, and a small regression harness.
The initializer is in [src/ModEntry.cs](src/ModEntry.cs), Harmony integration is in [src/Patches/MultiplayerPlayerStatePatches.cs](src/Patches/MultiplayerPlayerStatePatches.cs), and indicator behavior is in [src/Indicator/MultiplayerVulnerableIndicator.cs](src/Indicator/MultiplayerVulnerableIndicator.cs) and [src/Indicator/VulnerableIndicatorState.cs](src/Indicator/VulnerableIndicatorState.cs).
The main project and solution are [WaitIHaveVuln.csproj](WaitIHaveVuln.csproj) and [WaitIHaveVuln.sln](WaitIHaveVuln.sln), with metadata in [WaitIHaveVuln.json](WaitIHaveVuln.json).
The regression harness lives under [tests/RegressionHarness](tests/RegressionHarness/WaitIHaveVuln.RegressionHarness.csproj).

## Project status

The current indicator candidate has not been deployed or accepted in-game.
Build and harness results establish compilation and covered state behavior only; they do not establish texture loading, rendered row fit, multiplayer host/join behavior, or reconnect freshness.
The manifest author remains the placeholder `REPLACE_WITH_AUTHOR` and must be set by the owner before distribution.

## Further information

For portable machine setup, repository conventions, build and test commands, and the optional deployment/load procedure, see [CONTRIBUTING.md](CONTRIBUTING.md).
Project-wide agent guidance is in [.github/copilot-instructions.md](.github/copilot-instructions.md).
Player-facing changes are recorded in [CHANGELOG.md](CHANGELOG.md).
Use the [commit workflow](.github/skills/commit/SKILL.md) for reviewed local commits and the [publish workflow](.github/skills/publish/SKILL.md) for tagged releases.
