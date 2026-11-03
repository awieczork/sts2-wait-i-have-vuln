namespace WaitIHaveVuln.Indicator;

/// <summary>
/// Engine-independent rules for the bubble layer's reminders. A teammate is needed while they hold a
/// supported card they can play now, are alive, connected, and not ready to end the turn, and an enemy
/// still lacks Vulnerable; in co-op the own row never counts, and in solo mode it stands for a teammate. Every
/// hover of an Attack that does not apply Vulnerable shows one card cloud while a teammate is
/// needed and the local player can act, and every such Attack another player plays makes each needed
/// teammate react, angrier with each one in the turn.
/// </summary>
internal sealed class HandReminderPolicy<TPlayer, TTurn>
    where TPlayer : notnull
{
    private readonly Dictionary<TPlayer, int> _attacksThisTurn = new();
    private TTurn? _turn;
    private bool _hasTurn;

    /// <summary>Whether a bubble may announce this row's card: the row's player could still play it and an enemy lacks Vulnerable.</summary>
    internal static bool IsWorthAnnouncing(HandReminderRow<TPlayer> row, bool enemyNeedsVulnerable)
    {
        return enemyNeedsVulnerable && row.HoldsCard && row.IsAlive && row.IsConnected && !row.IsReadyToEndTurn;
    }

    /// <summary>Whether the hovered card gets the cloud: one for any number of needed teammates, and none for a card that is not an Attack or applies Vulnerable itself.</summary>
    internal static bool ShowsCloud(
        bool isAttack,
        bool isVulnerableCard,
        bool canAct,
        bool ownRowEligible,
        bool enemyNeedsVulnerable,
        IEnumerable<HandReminderRow<TPlayer>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return isAttack && !isVulnerableCard && canAct && rows.Any(row => IsNeeded(row, ownRowEligible, enemyNeedsVulnerable));
    }

    /// <summary>
    /// An Attack that does not apply Vulnerable was played by <paramref name="attacker"/>: each needed
    /// teammate other than the attacker reacts, with the count of such Attacks they have seen this
    /// turn, this one included. In solo mode your own row stands for a teammate, so your own Attack counts for it.
    /// </summary>
    internal List<(TPlayer Player, int Count)> AttackPlayed(
        TTurn turn,
        TPlayer attacker,
        bool ownRowEligible,
        bool enemyNeedsVulnerable,
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
            if (!IsNeeded(row, ownRowEligible, enemyNeedsVulnerable) || (isAttacker && !(row.IsOwnRow && ownRowEligible)))
            {
                continue;
            }

            var count = _attacksThisTurn.GetValueOrDefault(row.Player) + 1;
            _attacksThisTurn[row.Player] = count;
            reactions.Add((row.Player, count));
        }

        return reactions;
    }

    private static bool IsNeeded(HandReminderRow<TPlayer> row, bool ownRowEligible, bool enemyNeedsVulnerable)
    {
        return (!row.IsOwnRow || ownRowEligible) && row.CanPlayCard && IsWorthAnnouncing(row, enemyNeedsVulnerable);
    }
}
