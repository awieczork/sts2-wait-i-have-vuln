namespace WaitIHaveVuln.Indicator;

/// <summary>
/// Which level a reminder reads for a row. A reaction comes while an Attack is being played, before
/// its action ends, so teammates are read from the game then; the own row, which counts only in solo
/// mode, keeps its committed level, which is its state from before the play, as the cost of the Attack
/// itself is not the teammate's.
/// </summary>
internal static class ReminderLevel
{
    internal static bool IsFull(bool live, bool isOwnRow, bool liveIsFull, bool committedIsFull)
    {
        return live && !isOwnRow ? liveIsFull : committedIsFull;
    }
}
