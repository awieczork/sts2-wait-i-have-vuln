using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using WaitIHaveVuln.Indicator;

namespace WaitIHaveVuln.Patches;

// Parameter names must match the game's method parameters and fields exactly; Harmony binds by name.

[HarmonyPatch(typeof(NMultiplayerPlayerState), "OnCombatSetUp")]
internal static class MultiplayerPlayerStateCombatSetUpPatch
{
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
