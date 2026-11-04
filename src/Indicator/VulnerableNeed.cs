namespace WaitIHaveVuln.Indicator;

/// <summary>
/// Whether an enemy still needs Vulnerable, so that a supported card would make the next hit bigger.
/// An enemy with Artifact does not: the card would only take one Artifact stack, and the hit after
/// it would not be any bigger (owner's decision of 2026-11-03).
/// </summary>
internal static class VulnerableNeed
{
    internal static bool Of(bool hasVulnerable, bool hasArtifact) => !hasVulnerable && !hasArtifact;

    /// <summary>
    /// Whether an Attack hits an enemy that needs Vulnerable: its own target when it has one enemy
    /// target, otherwise any enemy, as for an Attack that hits all enemies or a random one.
    /// </summary>
    internal static bool AttackHitsNeedingEnemy(bool targetsOneEnemy, bool targetNeeds, bool anyEnemyNeeds)
    {
        return targetsOneEnemy ? targetNeeds : anyEnemyNeeds;
    }
}
