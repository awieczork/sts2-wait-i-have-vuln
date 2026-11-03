namespace WaitIHaveVuln.Indicator;

/// <summary>
/// Chooses the line of each character bubble: from the varied pools, or, with varied lines off, the
/// fixed line of its moment, so a player who turns them off sees the same line every time. A fixed
/// line shows for every speaker at once, as every row did in v0.2.0; only varied lines avoid one on screen.
/// </summary>
internal sealed class BubbleVoice
{
    private readonly BubbleLinePool _row;
    private readonly BubbleLinePool[] _angry;

    internal BubbleVoice(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        _row = new BubbleLinePool(BubbleLines.Row, random, BubbleLines.Signature);
        _angry = BubbleLines.Angry.Select(lines => new BubbleLinePool(lines, random)).ToArray();
    }

    /// <summary>The angry tier for the count of Attacks so far this turn: 0 for the first, and the last tier from there on.</summary>
    internal static int AngryTier(int attackCount)
    {
        return Math.Clamp(attackCount, 1, BubbleLines.Angry.Count) - 1;
    }

    /// <summary>The line when a supported card arrives in a teammate's hand; each combat opens with the signature.</summary>
    internal BubbleLine? NextRow(bool varied, object? combat, Func<BubbleLine, bool> isOnScreen, string? you, string? them)
    {
        return varied ? _row.Next(combat, isOnScreen, you, them) : new BubbleLine(BubbleLines.Signature);
    }

    /// <summary>The teammate's line for another player's Attack, by the count of such Attacks this turn.</summary>
    internal BubbleLine? NextAngry(bool varied, int attackCount, Func<BubbleLine, bool> isOnScreen, string? you, string? them, string? attacker)
    {
        return varied ? _angry[AngryTier(attackCount)].Next(null, isOnScreen, you, them, attacker) : new BubbleLine(BubbleLines.FixedAngry);
    }
}
