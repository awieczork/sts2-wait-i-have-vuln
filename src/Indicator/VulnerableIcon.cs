using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace WaitIHaveVuln.Indicator;

/// <summary>The game's Vulnerable power icon, for the row icon and the card reminder's thought cloud.</summary>
internal static class VulnerableIcon
{
    private static bool _loadAttempted;
    private static Texture2D? _texture;

    // Loaded once per process through the game's own accessor (PowerModel.Icon, as the game's power icons
    // load it), so a moved atlas follows the game. A failure is logged once and leaves the icon off until restart.
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
                _texture = ModelDb.Power<VulnerablePower>().Icon;
            }
            catch (Exception exception)
            {
                Log.Warn($"[{ModEntry.ModId}] Could not load the Vulnerable icon: {exception.Message}");
                return null;
            }

            if (_texture is null)
            {
                Log.Warn($"[{ModEntry.ModId}] Could not load the Vulnerable icon.");
            }

            return _texture;
        }
    }
}
