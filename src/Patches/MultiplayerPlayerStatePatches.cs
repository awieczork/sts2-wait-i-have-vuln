using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using WaitIHaveVuln.Indicator;

namespace WaitIHaveVuln.Patches;

// Injected field names must match the game's private fields exactly; Harmony binds by name.

[HarmonyPatch(typeof(NMultiplayerPlayerState), "OnCombatSetUp")]
internal static class MultiplayerPlayerStateCombatSetUpPatch
{
    private static bool Prepare(MethodBase? original)
    {
        return original is not null || PatchTarget.Exists(nameof(MultiplayerPlayerStateCombatSetUpPatch), "the row icon and bubbles", typeof(NMultiplayerPlayerState), "OnCombatSetUp", null,
            "_topContainer", "_healthBar", "_characterIcon");
    }

    [HarmonyPostfix]
    private static void Postfix(
        NMultiplayerPlayerState __instance,
        HBoxContainer ____topContainer,
        NHealthBar ____healthBar,
        TextureRect ____characterIcon)
    {
        MultiplayerPlayerStateIndicators.OnCombatSetUp(__instance, ____topContainer, ____healthBar, ____characterIcon);
    }
}

// The empty array selects the parameterless overload; attribute arguments cannot use Type.EmptyTypes.
[HarmonyPatch(typeof(NMultiplayerPlayerState), "RefreshConnectedState", new Type[0])]
internal static class MultiplayerPlayerStateConnectionStatePatch
{
    private static bool Prepare(MethodBase? original)
    {
        return original is not null || PatchTarget.Exists(nameof(MultiplayerPlayerStateConnectionStatePatch), "the row's disconnect handling", typeof(NMultiplayerPlayerState), "RefreshConnectedState", Type.EmptyTypes);
    }

    [HarmonyPostfix]
    private static void Postfix(NMultiplayerPlayerState __instance)
    {
        MultiplayerPlayerStateIndicators.OnConnectionStateChanged(__instance);
    }
}

[HarmonyPatch(typeof(NMultiplayerPlayerState), "OnCombatEnded")]
internal static class MultiplayerPlayerStateCombatEndedPatch
{
    private static bool Prepare(MethodBase? original)
    {
        return original is not null || PatchTarget.Exists(nameof(MultiplayerPlayerStateCombatEndedPatch), "hiding the row icon at combat end", typeof(NMultiplayerPlayerState), "OnCombatEnded");
    }

    [HarmonyPostfix]
    private static void Postfix(NMultiplayerPlayerState __instance)
    {
        MultiplayerPlayerStateIndicators.OnCombatEnded(__instance);
    }
}

[HarmonyPatch(typeof(NMultiplayerPlayerState), nameof(NMultiplayerPlayerState._ExitTree))]
internal static class MultiplayerPlayerStateExitTreePatch
{
    private static bool Prepare(MethodBase? original)
    {
        return original is not null || PatchTarget.Exists(nameof(MultiplayerPlayerStateExitTreePatch), "the row's cleanup", typeof(NMultiplayerPlayerState), nameof(NMultiplayerPlayerState._ExitTree));
    }

    [HarmonyPostfix]
    private static void Postfix(NMultiplayerPlayerState __instance)
    {
        MultiplayerPlayerStateIndicators.OnExitTree(__instance);
    }
}
