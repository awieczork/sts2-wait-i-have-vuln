# wait-i-have-vuln

A small quality-of-life mod for **Slay the Spire 2 co-op**.
It tells you which players are holding a card that applies **Vulnerable**, so the team can apply Vulnerable first and hit harder.

## What you see

During a co-op combat:

- **Vulnerable icon on the player list.** The Vulnerable icon appears next to a player's row in the top-left player list while that player has a supported card in hand. Your own row is included.
- **"Wait! I have vuln!" speech bubble.** When a player's icon turns on, for example after drawing Bash, a speech bubble with that text appears over their character for four seconds.

The icon only tells you that the card is **in hand**. It does not check whether the player has enough energy to play it, or whether there is a valid target.

The mod does not change gameplay, network messages, or saves.
Single-player games look exactly the same as without the mod.

### Supported cards

Assassinate, Bash, Beam Cell, Break, Comet, Dominate, Expose, Falling Star, Fear, Gamma Blast, High Five, Know Thy Place, Meteor Shower, Putrefy, Shockwave, Squash, Taunt, Thunderclap, Tremble, and Uppercut.

## Install

### From Steam Workshop (recommended)

1. Open the mod's [Steam Workshop page](https://steamcommunity.com/sharedfiles/filedetails/?id=3831970006) and click **Subscribe**. The item is visible to Limoonene's Steam friends.
2. Start Slay the Spire 2. Steam downloads the mod and keeps it updated.

### Manually

1. Download `wait-i-have-vuln-v<version>.zip` from the [latest release](https://github.com/awieczork/sts2-wait-i-have-vuln/releases/latest).
2. Create the folder `mods\WaitIHaveVuln` inside your game folder. Usually this is `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\WaitIHaveVuln`. In Steam, you can find the game folder with **right-click the game > Manage > Browse local files**.
3. Put `WaitIHaveVuln.dll` and `WaitIHaveVuln.json` from the ZIP into that folder.

### Turn the mod on

1. Start the game. The first time you run it with mods, the game shows a warning about mods. Accept it.
2. Open **Settings > Mod Settings** and make sure **wait-i-have-vuln** is enabled.
3. Restart the game if it asks you to.

The game version should be **v0.107.1** or newer.
The mod has been tested on Windows.

## Playing with friends

- Only the players who want to see the icons and bubbles need the mod. The host does not need it.
- When your mods differ from the host's, the game allows you to join and only writes a warning to its log, because this mod does not affect gameplay.
- Each player sees the indicators on their own screen only. If your friend doesn't have the mod, they won't see your speech bubble.

## Your progress in modded mode

When mods are on, Slay the Spire 2 uses **separate modded save profiles**.
The first time you play with mods, your unlocks and run history from normal play are not there.
Your normal saves are not changed and come back when you turn mods off.

To bring your normal progress (unlocks, settings, and run history) into the modded profile, copy it there yourself, either with a script or by hand.
A run that is still in progress is not copied, so finish or abandon it first.

### Before you start: turn off Steam Cloud

The game also keeps modded saves in Steam Cloud.
When it starts, it replaces local save files with the Steam Cloud copies, and deletes local files that are not in the cloud.
Copied files survive only if Steam Cloud is off for the game:

1. Close the game.
2. In Steam, **right-click Slay the Spire 2 > Properties > General** and turn off **Keep game saves in the Steam Cloud**.

If you turn Steam Cloud back on later, the game may replace the copied files with older copies from the cloud.
Keep your backup until you are sure everything is in place.

### Option A: Use the script

The [scripts](scripts/) folder has the same script for PowerShell and Bash.
It refuses to run while the game is open, asks you to confirm, backs up all your saves first, and then copies the files.

**Windows (PowerShell):** download [copy-vanilla-saves.ps1](scripts/copy-vanilla-saves.ps1), open PowerShell in the folder where you saved it, and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\copy-vanilla-saves.ps1
```

**Bash (Git Bash on Windows, or Linux):** download [copy-vanilla-saves.sh](scripts/copy-vanilla-saves.sh) and run:

```bash
bash copy-vanilla-saves.sh
```

Both copy profile 1 by default.
For another profile, add `-ProfileNumber 2` (PowerShell) or `--profile 2` (Bash).
The backup goes to `%APPDATA%\SlayTheSpire2\save-backups\<date and time>`.
On Linux, the save folder location has not been checked; if the script cannot find it, pass it with `--saves-root <folder>`.

### Option B: Copy the files by hand

1. Make sure the game is closed and Steam Cloud is off (see above).
2. Press **Win + R**, type `%APPDATA%\SlayTheSpire2\steam` and press **Enter**.
3. **Back up:** copy the folder you see there (a long number, your Steam ID) to a safe place, for example your Desktop.
4. Open that folder. `profile1`, `profile2`, and `profile3` are your **normal** profiles; `modded\profile1`, `modded\profile2`, and `modded\profile3` are your **modded** profiles.
5. From `profile1\saves`, copy `progress.save`, `progress.save.backup`, `prefs.save`, `prefs.save.backup`, and the `history` folder.
6. Paste them into `modded\profile1\saves` and replace the existing files. Use the matching numbers if you play on profile 2 or 3.
7. Start the game with mods on and check that your unlocks are there.

## Known limitations

- After a player disconnects, or when the game temporarily has no hand data, their icon keeps its last state until the mod can see their hand again.
- The speech bubble text is always English.
- The mod has been tested when joining a host. Hosting a game with the mod and reconnecting in the middle of a combat have not been tested yet.

## Remove the mod

Unsubscribe on Steam Workshop, or delete the `mods\WaitIHaveVuln` folder.
You can also just turn it off in **Settings > Mod Settings**.

## For developers

Build, test, and release instructions are in [CONTRIBUTING.md](CONTRIBUTING.md).
Changes are listed in [CHANGELOG.md](CHANGELOG.md).

## License

[MIT](LICENSE) © Limoonene.
Slay the Spire 2 and its assets belong to Mega Crit; this repository contains no game files.
