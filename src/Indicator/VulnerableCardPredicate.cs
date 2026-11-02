namespace WaitIHaveVuln.Indicator;

/// <summary>
/// Decides which cards light the indicator. A card qualifies when playing it applies Vulnerable
/// directly to an enemy; cards that only react to Vulnerable (such as Vicious) and conditional
/// appliers (Molten Fist, the Mad Science variants) are excluded by the owner's decision of 2026-10-25.
/// Ids are the game's card model ids as seen in <c>CardModel.Id.Entry</c>, and upgraded variants share
/// the id of their base card. When the game adds or renames a card, update this set and the README list.
/// </summary>
internal static class VulnerableCardPredicate
{
    private static readonly HashSet<string> SupportedModelIds = new(StringComparer.Ordinal)
    {
        "ASSASSINATE",
        "BASH",
        "BEAM_CELL",
        "BREAK",
        "COMET",
        "DOMINATE",
        "EXPOSE",
        "FALLING_STAR",
        "FEAR",
        "GAMMA_BLAST",
        "HIGH_FIVE",
        "KNOW_THY_PLACE",
        "METEOR_SHOWER",
        "PUTREFY",
        "SHOCKWAVE",
        "SQUASH",
        "TAUNT",
        "THUNDERCLAP",
        "TREMBLE",
        "UPPERCUT"
    };

    // Exposed for the regression harness, which checks the exact set.
    internal static IReadOnlySet<string> SupportedIds => SupportedModelIds;

    internal static bool IsSupported(string modelId) => SupportedModelIds.Contains(modelId);
}
