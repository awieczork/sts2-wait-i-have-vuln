using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;
using WaitIHaveVuln.Settings;

namespace WaitIHaveVuln.Patches;

// Fill runs each time a mod is selected on the modding screen. Its parameter is bound by
// position (__0), so a renamed parameter in a game update cannot break the binding.
[HarmonyPatch(typeof(NModInfoContainer), nameof(NModInfoContainer.Fill))]
internal static class ModInfoContainerFillPatch
{
    // Harmony calls it first with no target, before it binds anything.
    private static bool Prepare(MethodBase? original)
    {
        return original is not null || PatchTarget.Exists(nameof(ModInfoContainerFillPatch), "the settings panel", typeof(NModInfoContainer), nameof(NModInfoContainer.Fill), null, "_image", "_description");
    }

    [HarmonyPostfix]
    private static void Postfix(NModInfoContainer __instance, Mod __0, TextureRect ____image, MegaRichTextLabel ____description)
    {
        SettingsPanel.OnModInfoFilled(__instance, __0, ____image, ____description);
    }
}
