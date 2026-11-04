namespace WaitIHaveVuln.Indicator;

/// <summary>
/// The player turn the reactions count Attacks in. The round number alone misses an extra player
/// turn, which raises only the turn numbers of the players taking it, and one player's turn number
/// differs from another's for the rest of the combat after that; the sum of every player's turn number
/// rises at each player turn and at no other time, the same test the game makes for "this turn".
/// </summary>
internal readonly record struct ReactionTurn(int Round, int TurnNumberSum)
{
    internal static ReactionTurn Of(int round, IEnumerable<int> playerTurnNumbers)
    {
        ArgumentNullException.ThrowIfNull(playerTurnNumbers);
        return new ReactionTurn(round, playerTurnNumbers.Sum());
    }
}
