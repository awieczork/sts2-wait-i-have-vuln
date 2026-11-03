# Changelog

All notable user-facing changes to this mod are documented here.
This project follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

### Added

- The card reminder shows a thought cloud with the Vulnerable icon beside the Attack you hover while a teammate holds a supported card they can play right now, and it skips cards that apply Vulnerable themselves.
- A teammate holding a supported card reacts with a line over their character when another player plays an Attack before Vulnerable is applied, angrier with each such Attack in the turn.
- The bubble over a player who draws a supported card varies its line, some lines with a player's name, and opens each combat with "Wait! I have vuln!".
- The Card reminder setting turns the cloud off, and the Varied bubble lines setting keeps every bubble on one fixed line.

### Changed

- The bubble over a player who draws a supported card appears in their character's colour, and only while an enemy lacks Vulnerable and the player has not ended their turn.
- The settings are grouped under Player list, Bubbles, and Developer, and the Show bubbles setting turns off every bubble and keeps only the icon.
- A new line replaces a player's bubble still on screen instead of stacking over it.

## [0.2.0] - 2026-11-02

### Added

- Settings for Wait! I Have Vuln! appear under its entry in Settings > Mod Settings on the main menu and are changed with the mouse before a run.
- The speech bubble setting turns off the "Wait! I have vuln!" bubble and keeps the icon.
- The own-row setting hides the icon and the bubble on your own row and leaves teammates' rows unchanged.
- The icon size setting scales the Vulnerable icon from 50% to 150% of its default size.
- The settings are kept between game sessions in `%APPDATA%\SlayTheSpire2\WaitIHaveVuln\settings.json`, and an unreadable settings file is set aside as `settings.json.bad` while the defaults apply.
- Solo mode [dev] in the settings shows your own co-op row in single-player runs from the next run you start or continue, and it turns off when the game restarts.

### Changed

- The Vulnerable icon on the co-op player list is larger by default.

## [0.1.2] - 2026-11-02

### Fixed

- The Vulnerable icon on the co-op player list appears at a size that fits the row, sits right after the player's name, powers, or health bar, and is centred on the whole row.

## [0.1.1] - 2026-11-02

### Changed

- The game mod list shows the mod as Wait! I Have Vuln! by Limonene.
- The mod description in the game mod list states that the mod is client-side and does not affect gameplay.

### Fixed

- The co-op player list keeps working when the mod hits an error; the affected icon keeps its last state and the error goes to the game log as a warning.
- The "Wait! I have vuln!" bubble appears only when the Vulnerable icon appears next to the player.

## [0.1.0] - 2026-11-01

### Added

- The co-op player list shows the Vulnerable icon next to each player, including you, who has a card that applies Vulnerable in hand.
- A player's icon keeps its last state after a disconnect or while their hand is unavailable, until the mod can see that hand again.
- A "Wait! I have vuln!" speech bubble appears over a player's character for four seconds when their Vulnerable icon turns on.
- The game mod list identifies the mod as `wait-i-have-vuln`.

[0.2.0]: https://github.com/awieczork/sts2-wait-i-have-vuln/releases/tag/v0.2.0
[0.1.2]: https://github.com/awieczork/sts2-wait-i-have-vuln/releases/tag/v0.1.2
[0.1.1]: https://github.com/awieczork/sts2-wait-i-have-vuln/releases/tag/v0.1.1
[0.1.0]: https://github.com/awieczork/sts2-wait-i-have-vuln/releases/tag/v0.1.0
