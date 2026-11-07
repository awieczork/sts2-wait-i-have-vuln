namespace WaitIHaveVuln.Indicator;

/// <summary>
/// Where the card cloud goes at the hovered card: first its top corner on the side away from its tips,
/// then the other top corner, then above the card; nowhere when none of these is free. A spot is free
/// when the cloud keeps the gap to the card, to each tip, and to the screen edge. The game shows the
/// tips on one side of the card, or on both; with tips on both sides or none, the side with more room
/// on screen comes first.
/// </summary>
internal static class CloudPlacement
{
    // The hovered card lifts and grows at once but slides sideways to its spot over several frames, from
    // where its neighbour's hover pushed it. The cloud shows at once beside that spot and moves with the
    // slide; once the card has settled, its spot is checked once more.
    internal const double MinWaitSeconds = 0.15;
    internal const double MaxWaitSeconds = 0.6;
    internal const float SettledOffset = 1f;

    /// <summary>Whether the hovered card has settled enough to check the cloud's spot again: close to its spot after the shortest wait, or the longest wait is over.</summary>
    internal static bool IsSettled(float remainingOffset, double waitedSeconds)
    {
        return waitedSeconds >= MaxWaitSeconds || (waitedSeconds >= MinWaitSeconds && remainingOffset <= SettledOffset);
    }

    internal static CloudSpot? Place(Box card, IReadOnlyList<Box> tips, Box screen, float width, float height, float gap)
    {
        ArgumentNullException.ThrowIfNull(tips);
        if (width <= 0f || height <= 0f)
        {
            return null;
        }

        var first = FirstSide(card, tips, screen);
        foreach (var side in new[] { first, first == CloudSide.Left ? CloudSide.Right : CloudSide.Left, CloudSide.Above })
        {
            var cloud = Spot(card, side, width, height, gap);
            if (IsFree(cloud, card, tips, screen, gap))
            {
                return new CloudSpot(side, cloud);
            }
        }

        return null;
    }

    private static CloudSide FirstSide(Box card, IReadOnlyList<Box> tips, Box screen)
    {
        var tipsLeft = tips.Any(tip => tip.CenterX < card.CenterX);
        var tipsRight = tips.Any(tip => tip.CenterX >= card.CenterX);
        if (tipsRight != tipsLeft)
        {
            return tipsRight ? CloudSide.Left : CloudSide.Right;
        }

        return card.X - screen.X >= screen.Right - card.Right ? CloudSide.Left : CloudSide.Right;
    }

    private static Box Spot(Box card, CloudSide side, float width, float height, float gap)
    {
        return side switch
        {
            CloudSide.Left => new Box(card.X - gap - width, card.Y, width, height),
            CloudSide.Right => new Box(card.Right + gap, card.Y, width, height),
            _ => new Box(card.CenterX - width / 2f, card.Y - gap - height, width, height)
        };
    }

    // The spots are built a gap away from the card, so they are tested against the card itself: against
    // the grown card, rounding could make a cloud at exactly that gap count as overlapping.
    private static bool IsFree(Box cloud, Box card, IReadOnlyList<Box> tips, Box screen, float gap)
    {
        return cloud.IsInside(screen.Grow(-gap)) && !cloud.Intersects(card) && !tips.Any(tip => cloud.Intersects(tip.Grow(gap)));
    }
}
