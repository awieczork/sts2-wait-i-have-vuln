namespace WaitIHaveVuln.Indicator;

/// <summary>
/// Engine-independent rules for the bubble layer's reminders. A teammate is needed while their icon is
/// full (<see cref="HandReminderRow{TPlayer}.IsFull"/>), they are not ready to end the turn, and an enemy
/// still needs Vulnerable; in co-op the own row never counts, because the mod draws only on its own
/// player's screen, so a bubble over you would only tell you what you hold (owner's decision of
/// 2026-11-04), and in solo mode it stands for a teammate.
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
    /// Whether a bubble may announce this row's card: the row is a teammate's, or the own row in solo mode
    /// (<paramref name="ownRowEligible"/>), its icon is full, the player has not ended the turn, and an enemy needs Vulnerable.
    /// </summary>
    internal static bool IsWorthAnnouncing(HandReminderRow<TPlayer> row, bool ownRowEligible, bool enemyNeedsVulnerable)
    {
        return (!row.IsOwnRow || ownRowEligible) && enemyNeedsVulnerable && row.IsFull && !row.IsReadyToEndTurn;
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
        bool ownRowEligible,
        bool enemyNeedsVulnerable,
        IEnumerable<HandReminderRow<TPlayer>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return isAttack && !isVulnerableCard && isPlayable && canAct && rows.Any(row => IsWorthAnnouncing(row, ownRowEligible, enemyNeedsVulnerable));
    }

    /// <summary>
    /// An Attack that does not apply Vulnerable was played by <paramref name="attacker"/>, and
    /// <paramref name="hitsNeedingEnemy"/> tells whether it hits an enemy that needs Vulnerable
    /// (<see cref="VulnerableNeed.AttackHitsNeedingEnemy"/>): each needed teammate other than the
    /// attacker reacts, with the count of such Attacks they have seen this turn, this one included.
    /// In solo mode your own row stands for a teammate, so your own Attack counts for it.
    /// </summary>
    internal List<(TPlayer Player, int Count)> AttackPlayed(
        TTurn turn,
        TPlayer attacker,
        bool ownRowEligible,
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
            var isAttacker = EqualityComparer<TPlayer>.Default.Equals(row.Player, attacker);
            if (!IsWorthAnnouncing(row, ownRowEligible, hitsNeedingEnemy) || (isAttacker && !(row.IsOwnRow && ownRowEligible)))
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
