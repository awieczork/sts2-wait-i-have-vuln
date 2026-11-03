using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Combat;
using WaitIHaveVuln.Indicator;

namespace WaitIHaveVuln.Patches;

// Parameter names must match the game's method parameters and fields exactly; Harmony binds by name.

// Private; wired to each card holder's HolderFocused signal in NPlayerHand.AddCardHolder, so it runs
// when the local player hovers or focuses a card in hand.
[HarmonyPatch(typeof(NPlayerHand), "OnHolderFocused")]
internal static class PlayerHandHolderFocusedPatch
{
    // Harmony calls it first with no target, before it binds anything.
    private static bool Prepare(MethodBase? original)
    {
        return original is not null || PatchTarget.Exists(nameof(PlayerHandHolderFocusedPatch), "the card reminder", typeof(NPlayerHand), "OnHolderFocused", null, "_combatState");
    }

    [HarmonyPostfix]
    private static void Postfix(NPlayerHand __instance, CombatState? ____combatState)
    {
        HandReminders.OnLocalCardHovered(__instance, ____combatState);
    }
}

// Private; starts a card play from the hand, mouse or controller, before the card is dragged or targeted,
// so the card clouds go when a card is picked up.
[HarmonyPatch(typeof(NPlayerHand), "StartCardPlay")]
internal static class PlayerHandStartCardPlayPatch
{
    // Harmony calls it first with no target, before it binds anything.
    private static bool Prepare(MethodBase? original)
    {
        return original is not null || PatchTarget.Exists(nameof(PlayerHandStartCardPlayPatch), "clearing the card cloud on pick-up", typeof(NPlayerHand), "StartCardPlay");
    }

    [HarmonyPostfix]
    private static void Postfix()
    {
        HandReminders.OnLocalCardPickedUp();
    }
}
