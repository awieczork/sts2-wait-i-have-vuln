namespace WaitIHaveVuln.Indicator;

/// <summary>
/// The minimal view of a hand that <see cref="HandIndicatorState{TCard}"/> needs, so the state
/// can be driven by the game's card pile in the mod and by a fake hand in the regression harness.
/// </summary>
internal interface ICardHand<TCard>
{
    IReadOnlyList<TCard> Cards { get; }
    event Action<TCard> CardAdded;
    event Action<TCard> CardRemoved;
}
