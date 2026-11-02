using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using WaitIHaveVuln.Settings;

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

        SettingsService.Initialize();
        PatchEachClass();
        _patched = true;
        Log.Info($"[{ModId}] Mod initialized.");
    }

    // PatchAll stops at the first class that fails to bind, so a game update that breaks the
    // settings panel or solo mode patches would also leave the indicator unpatched. Each class
    // is patched on its own, and a failure turns off only that class.
    private static void PatchEachClass()
    {
        foreach (var type in AccessTools.GetTypesFromAssembly(typeof(ModEntry).Assembly))
        {
            try
            {
                Harmony.CreateClassProcessor(type).Patch();
            }
            catch (Exception exception)
            {
                Log.Warn($"[{ModId}] Could not patch {type.Name}; that part of the mod stays off. {exception.Message}");
            }
        }
    }
}
