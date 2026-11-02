using MegaCrit.Sts2.Core.Logging;

namespace WaitIHaveVuln;

// The patches run inside the game's own UI callbacks. A failure there must never
// propagate into the game, so it is logged and the game carries on.
internal static class HookGuard
{
    internal static void Run(string hook, string consequence, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Log.Warn($"[{ModEntry.ModId}] {hook} failed; {consequence}. {exception}");
        }
    }
}
