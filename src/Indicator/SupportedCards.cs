using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace WaitIHaveVuln.Indicator;

/// <summary>The game side of <see cref="VulnerableCardPredicate"/>: reads a card's id and, for Mad Science, its Tinker Time rider.</summary>
internal static class SupportedCards
{
    // MadScience.TinkerTimeRider (public) is the rider the Tinker Time event gave this copy; every copy shares one id.
    internal static bool IsSupported(CardModel card)
    {
        return VulnerableCardPredicate.IsSupported(card.Id.Entry, card is MadScience madScience ? madScience.TinkerTimeRider.ToString() : null);
    }
}
