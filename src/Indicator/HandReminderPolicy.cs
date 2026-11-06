namespace WaitIHaveVuln.Indicator;

/// <summary>
/// Engine-independent rules for the bubble layer's reminders. A teammate is needed while their icon is
/// full (<see cref="HandReminderRow{TPlayer}.IsFull"/>), they are not ready to end the turn, and an enemy
/// still needs Vulnerable; the own row never counts, because the mod draws only on its own player's
/// screen, so a bubble over you would only tell you what you hold (owner's decision of 2026-11-04).
/// Every hover of an Attack the local player can play and that does not apply Vulnerable shows one card
/// cloud while a teammate is needed and the local player can act, and every such Attack another player
/// plays at an enemy that needs Vulnerable makes each needed teammate react, angrier with each one in the turn.
/// </summary>
internal sealed class HandReminderPolicy<TPlayer, TTurn>
    where TPlayer : notnull
{
    private readonly Dictionary<TPlayer, int> _attacksThisTurn = new();
    private TTurn? _turn;
    private bool _hasTurn;

    /// <summary>
    /// Whether a bubble may announce this row's card: the row is a teammate's, its icon is full, the
    /// player has not ended the turn, and an enemy needs Vulnerable.
    /// </summary>
    internal static bool IsWorthAnnouncing(HandReminderRow<TPlayer> row, bool enemyNeedsVulnerable)
    {
        return SkipReason(row, enemyNeedsVulnerable) is null;
    }

    /// <summary>Why a bubble may not announce this row's card, for the log; null when it may (<see cref="IsWorthAnnouncing"/>).</summary>
    internal static string? SkipReason(HandReminderRow<TPlayer> row, bool enemyNeedsVulnerable)
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
    /// Whether the hovered card gets the cloud: one for any number of needed teammates, and none for a
    /// card that is not an Attack, applies Vulnerable itself, or cannot be played now.
    /// </summary>
    internal static bool ShowsCloud(
        bool isAttack,
        bool isVulnerableCard,
        bool isPlayable,
        bool canAct,
        bool enemyNeedsVulnerable,
        IEnumerable<HandReminderRow<TPlayer>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return isAttack && !isVulnerableCard && isPlayable && canAct && rows.Any(row => IsWorthAnnouncing(row, enemyNeedsVulnerable));
    }

    /// <summary>
    /// An Attack that does not apply Vulnerable was played by <paramref name="attacker"/>, and
    /// <paramref name="hitsNeedingEnemy"/> tells whether it hits an enemy that needs Vulnerable
    /// (<see cref="VulnerableNeed.AttackHitsNeedingEnemy"/>): each needed teammate other than the
    /// attacker reacts, with the count of such Attacks they have seen this turn, this one included.
    /// </summary>
    internal List<(TPlayer Player, int Count)> AttackPlayed(
        TTurn turn,
        TPlayer attacker,
        bool hitsNeedingEnemy,
        IEnumerable<HandReminderRow<TPlayer>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (!_hasTurn || !EqualityComparer<TTurn>.Default.Equals(_turn, turn))
        {
            _attacksThisTurn.Clear();
            _turn = turn;
            _hasTurn = true;
        }

        var reactions = new List<(TPlayer, int)>();
        foreach (var row in rows)
        {
            if (!IsWorthAnnouncing(row, hitsNeedingEnemy) || EqualityComparer<TPlayer>.Default.Equals(row.Player, attacker))
            {
                continue;
            }

            var count = _attacksThisTurn.GetValueOrDefault(row.Player) + 1;
            _attacksThisTurn[row.Player] = count;
            reactions.Add((row.Player, count));
        }

        return reactions;
    }
}
