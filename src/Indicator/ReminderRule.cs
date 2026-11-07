namespace WaitIHaveVuln.Indicator;

/// <summary>
/// Engine-independent rules for the bubble layer's reminders. A teammate is needed while their icon is
/// full (<see cref="HandReminderRow{TPlayer}.IsFull"/>), they are not ready to end the turn, and an enemy
/// still needs Vulnerable; the own row never counts, because the mod draws only on its own player's
/// screen, so a bubble over you would only tell you what you hold (owner's decision of 2026-11-04).
/// Every hover of an Attack the local player can play and that does not apply Vulnerable shows one card
/// cloud while a teammate is needed and the local player can act, and every such Attack another player
/// plays at an enemy that needs Vulnerable makes each needed teammate react
/// (<see cref="HandReminderPolicy{TPlayer, TTurn}"/>), angrier with each one in the turn.
/// </summary>
internal static class ReminderRule
{
    /// <summary>The cards the reminders are about: an Attack that does not apply Vulnerable itself.</summary>
    internal static bool IsReminderCard(bool isAttack, bool appliesVulnerable)
    {
        return isAttack && !appliesVulnerable;
    }

    /// <summary>
    /// Whether a card play counts as an Attack the teammates react to: a reminder card, played once per
    /// play (the first of a series of repeats) and not by the game's auto-play.
    /// </summary>
    internal static bool CountsAsAttack(bool isFirstInSeries, bool isAutoPlay, bool isAttack, bool appliesVulnerable)
    {
        return isFirstInSeries && !isAutoPlay && IsReminderCard(isAttack, appliesVulnerable);
    }

    /// <summary>
    /// Whether a bubble may announce this row's card: the row is a teammate's, its icon is full, the
    /// player has not ended the turn, and an enemy needs Vulnerable.
    /// </summary>
    internal static bool IsWorthAnnouncing<TPlayer>(HandReminderRow<TPlayer> row, bool enemyNeedsVulnerable)
    {
        return SkipReason(row, enemyNeedsVulnerable) is null;
    }

    /// <summary>Why a bubble may not announce this row's card, for the log; null when it may (<see cref="IsWorthAnnouncing"/>).</summary>
    internal static string? SkipReason<TPlayer>(HandReminderRow<TPlayer> row, bool enemyNeedsVulnerable)
    {
        if (row.IsOwnRow)
        {
            return "own row";
        }

        if (!row.IsFull)
        {
            return "icon not full";
        }

        if (row.IsReadyToEndTurn)
        {
            return "turn ended";
        }

        return enemyNeedsVulnerable ? null : "no enemy needs Vulnerable";
    }

    /// <summary>
    /// Whether the hovered reminder card (<see cref="IsReminderCard"/>) gets the cloud: one for any number
    /// of needed teammates, and none while the card cannot be played now or the local player cannot act.
    /// </summary>
    internal static bool ShowsCloud<TPlayer>(bool isPlayable, bool canAct, bool enemyNeedsVulnerable, IEnumerable<HandReminderRow<TPlayer>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return isPlayable && canAct && rows.Any(row => IsWorthAnnouncing(row, enemyNeedsVulnerable));
    }
}
