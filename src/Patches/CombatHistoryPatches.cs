using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;
using WaitIHaveVuln.Indicator;

namespace WaitIHaveVuln.Patches;

// Runs on every client for every card play, so a teammate's Attack reaches the reminder as well as the
// local player's; only Attacks that do not apply Vulnerable themselves count, once per play and not
// for autoplays. The combat and the play are bound by position (__0, __1), because the game's
// parameter names are not in the metadata the project reads.
[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardPlayStarted))]
internal static class CombatHistoryCardPlayStartedPatch
{
    // Harmony calls it first with no target, before it binds anything.
    private static bool Prepare(MethodBase? original)
    {
        return original is not null || PatchTarget.Exists(nameof(CombatHistoryCardPlayStartedPatch), "the teammates' reactions", typeof(CombatHistory), nameof(CombatHistory.CardPlayStarted));
    }

    [HarmonyPostfix]
    private static void Postfix(ICombatState? __0, CardPlay __1)
    {
        HookGuard.Run("Card play started", "no teammate reacts to the Attack", () =>
        {
            if (__1.IsFirstInSeries && !__1.IsAutoPlay && __1.Card is { Type: CardType.Attack, Owner: { } attacker } card
                && !SupportedCards.IsSupported(card))
            {
                HandReminders.OnAttackPlayed(attacker, __0, __1.Target);
            }
        });
    }
}
