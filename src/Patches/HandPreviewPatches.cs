using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using WaitIHaveVuln.HandPreview;

namespace WaitIHaveVuln.Patches;

// The hand preview binds its own patch classes on the row methods the indicator also patches, so a game
// update that breaks one feature's binding leaves the other working. Parameter names must match the
// game's fields exactly; Harmony binds by name.

// The game calls OnCombatSetUp on every row when a combat starts; for a teammate's row it has just shown
// the card-count icon (_cardContainer).
[HarmonyPatch(typeof(NMultiplayerPlayerState), "OnCombatSetUp")]
internal static class HandPreviewCombatSetUpPatch
{
    // Harmony calls it first with no target, before it binds anything.
    private static bool Prepare(MethodBase? original)
    {
        return original is not null || PatchTarget.Exists(nameof(HandPreviewCombatSetUpPatch), "the teammate hand preview", typeof(NMultiplayerPlayerState), "OnCombatSetUp", null,
            "_cardContainer", "_topContainer", "_healthBar");
    }

    [HarmonyPostfix]
    private static void Postfix(NMultiplayerPlayerState __instance, Control ____cardContainer, HBoxContainer ____topContainer, NHealthBar ____healthBar)
    {
        HandPreviews.OnCombatSetUp(__instance, ____cardContainer, ____topContainer, ____healthBar);
    }
}

// The game hides the card-count icon here and unsubscribes the row from the hand.
[HarmonyPatch(typeof(NMultiplayerPlayerState), "OnCombatEnded")]
internal static class HandPreviewCombatEndedPatch
{
    // Harmony calls it first with no target, before it binds anything.
    private static bool Prepare(MethodBase? original)
    {
        return original is not null || PatchTarget.Exists(nameof(HandPreviewCombatEndedPatch), "closing the hand preview at combat end", typeof(NMultiplayerPlayerState), "OnCombatEnded");
    }

    [HarmonyPostfix]
    private static void Postfix(NMultiplayerPlayerState __instance)
    {
        HandPreviews.OnCombatEnded(__instance);
    }
}

[HarmonyPatch(typeof(NMultiplayerPlayerState), nameof(NMultiplayerPlayerState._ExitTree))]
internal static class HandPreviewExitTreePatch
{
    // Harmony calls it first with no target, before it binds anything.
    private static bool Prepare(MethodBase? original)
    {
        return original is not null || PatchTarget.Exists(nameof(HandPreviewExitTreePatch), "the hand preview's cleanup", typeof(NMultiplayerPlayerState), nameof(NMultiplayerPlayerState._ExitTree));
    }

    [HarmonyPostfix]
    private static void Postfix(NMultiplayerPlayerState __instance)
    {
        HandPreviews.OnExitTree(__instance);
    }
}
