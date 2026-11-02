namespace WaitIHaveVuln.Settings;

/// <summary>
/// Reads and writes settings.json in one directory with plain file I/O, away from the game's
/// saves and cloud sync. I/O errors propagate; the caller logs them.
/// </summary>
internal sealed class SettingsStore
{
    internal const string FileName = "settings.json";
    private readonly string _directory;

    internal SettingsStore(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        _directory = directory;
    }

    internal string FilePath => Path.Combine(_directory, FileName);

    internal string DamagedFilePath => FilePath + ".bad";

    private string TemporaryFilePath => FilePath + ".tmp";

    internal SettingsLoadOutcome Load(out PlayerSettings settings)
    {
        if (!File.Exists(FilePath))
        {
            settings = PlayerSettings.Default;
            return SettingsLoadOutcome.Missing;
        }

        if (PlayerSettings.TryParse(File.ReadAllText(FilePath), out settings))
        {
            return SettingsLoadOutcome.Loaded;
        }

        // Kept for the player to inspect; the next save writes a fresh file.
        File.Move(FilePath, DamagedFilePath, overwrite: true);
        return SettingsLoadOutcome.Damaged;
    }

    // Written beside the target and moved over it, so a crash mid-write leaves the previous file.
    internal void Save(PlayerSettings settings)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(TemporaryFilePath, settings.ToJson());
        File.Move(TemporaryFilePath, FilePath, overwrite: true);
    }
}
