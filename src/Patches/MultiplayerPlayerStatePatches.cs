using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using WaitIHaveVuln.Indicator;

namespace WaitIHaveVuln.Patches;

[HarmonyPatch(typeof(NMultiplayerPlayerState), "OnCombatSetUp")]
internal static class MultiplayerPlayerStateCombatSetUpPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        NMultiplayerPlayerState __instance,
        HBoxContainer ____topContainer)
    {
        MultiplayerPlayerStateIndicators.OnCombatSetUp(__instance, ____topContainer);
    }
}

[HarmonyPatch(typeof(NMultiplayerPlayerState), "RefreshConnectedState", new Type[] { })]
internal static class MultiplayerPlayerStateConnectionStatePatch
{
    [HarmonyPostfix]
    private static void Postfix(NMultiplayerPlayerState __instance)
    {
        MultiplayerPlayerStateIndicators.OnConnectionStateChanged(__instance);
    }
}

[HarmonyPatch(typeof(NMultiplayerPlayerState), "OnCombatEnded")]
internal static class MultiplayerPlayerStateCombatEndedPatch
{
    [HarmonyPostfix]
    private static void Postfix(NMultiplayerPlayerState __instance)
    {
        MultiplayerPlayerStateIndicators.OnCombatEnded(__instance);
    }
}

[HarmonyPatch(typeof(NMultiplayerPlayerState), "_ExitTree")]
internal static class MultiplayerPlayerStateExitTreePatch
{
    [HarmonyPostfix]
    private static void Postfix(NMultiplayerPlayerState __instance)
    {
        MultiplayerPlayerStateIndicators.OnExitTree(__instance);
    }
}
