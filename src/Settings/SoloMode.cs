using MegaCrit.Sts2.Core.Logging;

namespace WaitIHaveVuln.Settings;

/// <summary>
/// The "Solo mode [dev]" testing aid: creates your own co-op row in single-player runs.
/// Runtime only and never saved, so every game start begins with it off. The game builds the
/// rows when a run loads, so a change applies from the next run start or continue.
/// </summary>
internal static class SoloMode
{
    private static bool _enabled;

    internal static bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            Log.Info($"[{ModEntry.ModId}] Solo mode [dev] {(value ? "on" : "off")}; applies from the next run start or continue.");
        }
    }

    // Called from the transpiled player-count check in NMultiplayerPlayerStateContainer.Initialize,
    // which creates the rows only when the player count is above this value.
    public static int PlayerCountThreshold()
    {
        return _enabled ? 0 : 1;
    }
}
