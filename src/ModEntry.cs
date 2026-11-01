using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using HarmonyLib;

namespace WaitIHaveVuln;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    internal const string ModId = "WaitIHaveVuln";

    private static readonly Harmony Harmony = new(ModId);
    private static bool _patched;

    public static void Initialize()
    {
        if (_patched)
        {
            return;
        }

        Harmony.PatchAll(typeof(ModEntry).Assembly);
        _patched = true;
        Log.Info($"[{ModId}] Mod initialized.");
#if SOLO_TEST_ROW
        Log.Info($"[{ModId}] Solo test row enabled (test build).");
#endif
    }
}
