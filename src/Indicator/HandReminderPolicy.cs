namespace WaitIHaveVuln.Indicator;

/// <summary>
/// The teammates' reactions in one combat: who reacts to an Attack (<see cref="ReminderRule"/>), and the
/// count of such Attacks each teammate has seen this turn, which makes them angrier with each one.
/// </summary>
internal sealed class HandReminderPolicy<TPlayer, TTurn>
    where TPlayer : notnull
{
    private readonly Dictionary<TPlayer, int> _attacksThisTurn = new();
    private TTurn? _turn;
    private bool _hasTurn;

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
            if (!ReminderRule.IsWorthAnnouncing(row, hitsNeedingEnemy) || EqualityComparer<TPlayer>.Default.Equals(row.Player, attacker))
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
