namespace WaitIHaveVuln.Indicator;

/// <summary>What a player's row icon shows: nothing, a dimmed icon for a supported card they hold but cannot play now, or the full icon.</summary>
internal enum IndicatorLevel
{
    Off,
    Dimmed,
    Full
}
