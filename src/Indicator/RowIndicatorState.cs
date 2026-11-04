namespace WaitIHaveVuln.Indicator;

/// <summary>
/// One row's committed level. Each observation brings the level computed from the game now and
/// whether it may show yet (<see cref="IndicatorRule.CanCommit"/>). A refused level that differs from
/// the committed one leaves the row <see cref="IsPending"/>, so the caller checks again when the action
/// may have moved on, such as into a pause for a player's choice, which raises no event.
/// </summary>
internal sealed class RowIndicatorState
{
    internal IndicatorLevel Committed { get; private set; } = IndicatorLevel.Off;

    internal bool IsPending { get; private set; }

    /// <summary>Commits <paramref name="level"/> when allowed and different; returns the change, or null when nothing changed.</summary>
    internal IndicatorChange? Observe(IndicatorLevel level, bool canCommit)
    {
        if (level == Committed)
        {
            IsPending = false;
            return null;
        }

        if (!canCommit)
        {
            IsPending = true;
            return null;
        }

        IsPending = false;
        var change = new IndicatorChange(Committed, level);
        Committed = level;
        return change;
    }

    /// <summary>Goes off at once, whatever runs, as at the end of a combat.</summary>
    internal IndicatorChange? Reset() => Observe(IndicatorLevel.Off, true);
}
