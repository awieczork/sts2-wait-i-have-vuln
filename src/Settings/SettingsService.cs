using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace WaitIHaveVuln.Settings;

/// <summary>
/// Holds the current settings for the game process and saves each change at once.
/// Changes come only from the main-menu panel, because the game greys out Mod Settings
/// during a run, so readers take Current when they build an icon and need no change event.
/// A failed load leaves the defaults in place and turns saving off for the session,
/// so a file that could not be read is never overwritten.
/// </summary>
internal static class SettingsService
{
    private const string SettingsDirectory = "user://" + ModEntry.ModId;
    private static SettingsStore? _store;

    internal static PlayerSettings Current { get; private set; } = PlayerSettings.Default;

    internal static void Initialize()
    {
        SettingsStore store;
        try
        {
            // Godot globalizes with forward slashes; GetFullPath gives the platform's own
            // separators, so the paths in the log match what the player sees in Explorer.
            store = new SettingsStore(Path.GetFullPath(ProjectSettings.GlobalizePath(SettingsDirectory)));
            var outcome = store.Load(out var settings);
            Current = settings;
            if (outcome == SettingsLoadOutcome.Damaged)
            {
                Log.Warn($"[{ModEntry.ModId}] Settings file could not be read; moved it to '{store.DamagedFilePath}' and using the defaults.");
            }
        }
        catch (Exception exception)
        {
            Log.Warn($"[{ModEntry.ModId}] Settings could not be loaded; using the defaults, and changes are not saved this session. {exception.Message}");
            return;
        }

        _store = store;
    }

    internal static void Update(PlayerSettings settings)
    {
        var normalized = settings.Normalize();
        if (normalized == Current)
        {
            return;
        }

        Current = normalized;
        if (_store is null)
        {
            return;
        }

        try
        {
            _store.Save(normalized);
        }
        catch (Exception exception)
        {
            Log.Warn($"[{ModEntry.ModId}] Settings could not be saved to '{_store.FilePath}'; the change applies until the game restarts. {exception.Message}");
        }
    }
}
