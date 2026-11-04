namespace WaitIHaveVuln.Indicator;

/// <summary>
/// How long a game bubble over a creature holds the mod's arrival back. The bubble's end is normally
/// seen when the game frees it; this only bounds a bubble that is never freed, taken from how long
/// the game shows it, with a margin.
/// </summary>
internal static class GameBubbleTiming
{
    // A thought bubble with no duration stays until its caller takes it down.
    internal const double UntimedSeconds = 5.0;
    private const double Margin = 0.5;

    /// <summary>
    /// NSpeechBubbleVfx.AnimateSpeechBubble: 0.5 s in, then max(seconds − 1, 1) held, then 0.4 s out.
    /// </summary>
    internal static double SpeechSeconds(double seconds) => 0.5 + Math.Max(seconds - 1.0, 1.0) + 0.4 + Margin;

    /// <summary>NThoughtBubbleVfx: holds max(seconds, 1) through Cmd.Wait, then goes away; null holds until taken down.</summary>
    internal static double ThoughtSeconds(double? seconds) => seconds is { } held ? Math.Max(held, 1.0) + 0.5 + Margin : UntimedSeconds;
}
