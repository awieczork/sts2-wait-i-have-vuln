namespace WaitIHaveVuln.Indicator;

/// <summary>
/// Engine-independent rules for the row icon. A row shows only while its player can act in a live
/// combat: the combat is not ending, the player is connected and alive, and their own turn phase is
/// Play. Then the icon is full while a supported card in hand passes the game's play check, dimmed
/// while supported cards are held but none passes, and off with none.
/// </summary>
internal static class IndicatorRule
{
    /// <summary>Whether the row may show at all; the hand is read only when it may, because the game's play check runs per card.</summary>
    internal static bool MayShow(bool combatLive, bool isConnected, bool isAlive, bool isInPlayPhase)
    {
        return combatLive && isConnected && isAlive && isInPlayPhase;
    }

    internal static IndicatorLevel Level(bool mayShow, bool holdsSupportedCard, bool canPlaySupportedCard)
    {
        if (!mayShow || !holdsSupportedCard)
        {
            return IndicatorLevel.Off;
        }

        return canPlaySupportedCard ? IndicatorLevel.Full : IndicatorLevel.Dimmed;
    }

    /// <summary>
    /// Whether a new level may show now. During a running action the state can be part-way through it,
    /// such as energy spent and not yet gained back, so a change waits until the action ends or pauses
    /// for a choice, where the state is the player's real state for as long as they choose.
    /// </summary>
    internal static bool CanCommit(ActionPhase phase) => phase != ActionPhase.Running;
}
