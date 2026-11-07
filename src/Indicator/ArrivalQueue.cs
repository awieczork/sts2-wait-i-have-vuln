namespace WaitIHaveVuln.Indicator;

/// <summary>
/// The game's own bubbles win over the mod's (owner's decision of 2026-11-04): an arrival bubble due
/// while a game bubble shows over that player waits until the game bubble ends, then shows if the
/// row is still worth announcing; a reaction due then is dropped (<c>Bubbles.React</c>); a game
/// bubble that appears takes the mod's bubble down. This keeps the waiting arrivals.
/// </summary>
internal sealed class ArrivalQueue<TPlayer>
    where TPlayer : notnull
{
    private readonly HashSet<TPlayer> _waiting = new();

    /// <summary>True when the arrival bubble may show now; false when it waits for the game's bubble to end.</summary>
    internal bool Arrive(TPlayer player, bool gameBubbleShowing)
    {
        if (gameBubbleShowing)
        {
            _waiting.Add(player);
            return false;
        }

        _waiting.Remove(player);
        return true;
    }

    /// <summary>The game's bubble over <paramref name="player"/> ended: true when an arrival was waiting, which the caller shows if the row is still worth announcing.</summary>
    internal bool GameBubbleEnded(TPlayer player) => _waiting.Remove(player);

    /// <summary>The row left full, so its waiting arrival has nothing left to announce.</summary>
    internal void Cancel(TPlayer player) => _waiting.Remove(player);

    // For the regression harness.
    internal bool IsWaiting(TPlayer player) => _waiting.Contains(player);
}
