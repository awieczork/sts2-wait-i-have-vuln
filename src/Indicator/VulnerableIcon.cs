using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace WaitIHaveVuln.Indicator;

/// <summary>The game's Vulnerable power icon, for the row icon and the arm's thought cloud.</summary>
internal static class VulnerableIcon
{
    private const string Path = "res://images/atlases/power_atlas.sprites/vulnerable_power.tres";
    private static bool _loadAttempted;
    private static Texture2D? _texture;

    // Loaded once per process. A failure is logged once and leaves the icon off until restart.
    internal static Texture2D? Texture
    {
        get
        {
            if (_loadAttempted)
            {
                return _texture;
            }

            _loadAttempted = true;
            try
            {
                _texture = ResourceLoader.Load<Texture2D>(Path);
            }
            catch (Exception exception)
            {
                Log.Warn($"[{ModEntry.ModId}] Could not load the Vulnerable icon at '{Path}': {exception.Message}");
                return null;
            }

            if (_texture is null)
            {
                Log.Warn($"[{ModEntry.ModId}] Could not load the Vulnerable icon at '{Path}'.");
            }

            return _texture;
        }
    }
}
