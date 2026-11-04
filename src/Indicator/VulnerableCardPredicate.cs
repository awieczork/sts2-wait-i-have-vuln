namespace WaitIHaveVuln.Indicator;

/// <summary>
/// Decides which cards light the indicator. A card qualifies when playing it applies Vulnerable
/// directly to an enemy. Excluded: cards that only react to Vulnerable (such as Vicious), Molten Fist,
/// which adds only to Vulnerable an enemy already has, and Misery, which spreads debuffs the target
/// already has (owner's decision of 2026-10-25). Mad Science counts only for a copy with the Sapping
/// rider, which applies Vulnerable on every play (owner's decision of 2026-11-03).
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

    // The Mad Science model and the name of the Tinker Time rider that makes every copy apply Vulnerable.
    internal const string MadScienceId = "MAD_SCIENCE";
    internal const string SappingRider = "Sapping";

    internal static bool IsSupported(string modelId) => SupportedModelIds.Contains(modelId);

    /// <summary>
    /// Whether a card applies Vulnerable when played: one of the supported ids, or a Mad Science
    /// copy whose Tinker Time rider (<paramref name="madScienceRider"/>, the rider's enum name, null for
    /// other cards) is Sapping, which applies Weak and Vulnerable on every play.
    /// </summary>
    internal static bool IsSupported(string modelId, string? madScienceRider)
    {
        return SupportedModelIds.Contains(modelId)
            || (string.Equals(modelId, MadScienceId, StringComparison.Ordinal) && string.Equals(madScienceRider, SappingRider, StringComparison.Ordinal));
    }
}
