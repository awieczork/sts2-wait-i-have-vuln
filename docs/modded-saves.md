# Bring your normal progress into modded mode

When mods are on, Slay the Spire 2 keeps **separate modded save profiles**.
The first time you play with mods, your unlocks, settings, and run history from normal play are not there.
Your normal saves are not changed and come back when you turn mods off.

This guide copies your normal progress into the modded profile, either with a script or by hand.
A run that is still in progress is not copied, so finish or abandon it first.

## Before you start: turn off Steam Cloud

The game also syncs modded saves with Steam Cloud, which can overwrite the copied files with its own copies when the game starts.
Turn it off before copying:

1. Close the game.
2. In Steam, **right-click Slay the Spire 2 > Properties > General** and turn off **Keep game saves in the Steam Cloud**.

If you turn Steam Cloud back on later, it may bring back older copies.
Keep your backup until you are sure everything is in place.

## Option A: use the script

The [scripts](../scripts/) folder has the same script for PowerShell and Bash.
It refuses to run while the game is open, asks you to confirm, backs up all your saves first, and then copies the files.

**Windows (PowerShell):** download [copy-vanilla-saves.ps1](../scripts/copy-vanilla-saves.ps1), open PowerShell in the folder where you saved it, and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\copy-vanilla-saves.ps1
```

**Bash (Git Bash on Windows, or Linux):** download [copy-vanilla-saves.sh](../scripts/copy-vanilla-saves.sh) and run:

```bash
bash copy-vanilla-saves.sh
```

Both copy profile 1 by default.
For another profile, add `-ProfileNumber 2` (PowerShell) or `--profile 2` (Bash).
The backup goes to `%APPDATA%\SlayTheSpire2\save-backups\<date and time>`; you can move it anywhere afterwards.
On Linux the save folder location has not been checked; if the script cannot find it, pass it with `--saves-root <folder>`.

## Option B: copy the files by hand

1. Make sure the game is closed and Steam Cloud is off (see above).
2. Press **Win + R**, type `%APPDATA%\SlayTheSpire2\steam` and press **Enter**.
3. **Back up:** copy the folder you see there (a long number, your Steam ID) to a safe place, for example your Desktop.
4. Open that folder. `profile1`, `profile2`, and `profile3` are your **normal** profiles; `modded\profile1`, `modded\profile2`, and `modded\profile3` are your **modded** profiles.
5. From `profile1\saves`, copy `progress.save`, `progress.save.backup`, `prefs.save`, `prefs.save.backup`, and the `history` folder.
6. Paste them into `modded\profile1\saves` and replace the existing files. Use the matching numbers if you play on profile 2 or 3.
7. Start the game with mods on and check that your unlocks are there.
