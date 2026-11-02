# Wait! I Have Vuln!

A Slay the Spire 2 co-op mod that shows which players are holding a card that applies Vulnerable, so the team applies it before the big hits.

**[Get it on the Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3831970006)** · pictures are on the Workshop page.

## What you see

During a co-op combat:

- **Vulnerable icon.** The Vulnerable icon appears next to a player's name in the top-left player list while that player has a supported card in hand. Your own row is included.
- **"Wait! I have vuln!" bubble.** When a player's icon turns on, for example after drawing Bash, a speech bubble with that text appears over their character for four seconds.

Supported cards: Assassinate, Bash, Beam Cell, Break, Comet, Dominate, Expose, Falling Star, Fear, Gamma Blast, High Five, Know Thy Place, Meteor Shower, Putrefy, Shockwave, Squash, Taunt, Thunderclap, Tremble, and Uppercut.

## Good to know

- **Only the players who want to see it need the mod.** The host does not. Each player sees the icons and bubbles on their own screen only.
- **It cannot desync a run.** The mod only reads game state and never runs game commands. It is marked as not affecting gameplay, so the game lets you join a host whose mods differ and only notes it in the log.
- **In hand means in hand.** The icon does not check whether the player has the energy or a target to play the card.
- **Single-player looks the same as without the mod.**
- **After a disconnect,** a player's icon keeps its last state until the mod can see their hand again.
- **The bubble text is always English.**
- **Not tested yet:** hosting with the mod, reconnecting in the middle of a combat, Linux, and Steam Deck.
- **Needs game version 0.107.1 or newer.**

## Install

**Steam Workshop (recommended):** subscribe on the [Workshop page](https://steamcommunity.com/sharedfiles/filedetails/?id=3831970006) and start the game. The item is visible to the author's Steam friends.

**Manually:** download `wait-i-have-vuln-v<version>.zip` from the [latest release](https://github.com/awieczork/sts2-wait-i-have-vuln/releases/latest), create the folder `mods\WaitIHaveVuln` inside your game folder (in Steam: right-click the game > Manage > Browse local files), and put `WaitIHaveVuln.dll` and `WaitIHaveVuln.json` from the ZIP into it.

Then start the game, accept the mods warning on the first modded launch, and check that **Wait! I Have Vuln!** is enabled under Settings > Mod Settings.

To remove the mod, unsubscribe on the Workshop or delete the `mods\WaitIHaveVuln` folder.

## Your saves in modded mode

With mods on, the game uses separate modded save profiles, so your unlocks and run history look gone the first time. Your normal saves are untouched and come back when you turn mods off.
To bring your normal progress into the modded profile, follow the [save guide](docs/modded-saves.md).

## Building from source

1. Install the .NET 9 SDK and Slay the Spire 2.
2. If the build cannot find the game, copy `Directory.Build.props.example` to `Directory.Build.props` and set `Sts2InstallDir`.
3. Run `dotnet build WaitIHaveVuln.csproj`.
4. Run `dotnet run --project tests/RegressionHarness/WaitIHaveVuln.RegressionHarness.csproj`.

Deployment, the solo test build, and the release checklist are in [CONTRIBUTING.md](CONTRIBUTING.md).

## What's changed

See [CHANGELOG.md](CHANGELOG.md).

## License

[MIT](LICENSE) © Limonene.
Not affiliated with or endorsed by Mega Crit. Slay the Spire 2 and its assets belong to Mega Crit; this repository contains no game files.
