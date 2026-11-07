using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;
using WaitIHaveVuln.Indicator;

namespace WaitIHaveVuln.Patches;

// Runs on every client for every card play, so a teammate's Attack reaches the reminder as well as the
// local player's; HandReminders keeps only the plays ReminderRule.CountsAsAttack accepts. The combat and
// the play are bound by position (__0, __1), because the game's parameter names are not in the metadata
// the project reads.
[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardPlayStarted))]
internal static class CombatHistoryCardPlayStartedPatch
{
    private static bool Prepare(MethodBase? original)
    {
        return original is not null || PatchTarget.Exists(nameof(CombatHistoryCardPlayStartedPatch), "the teammates' reactions", typeof(CombatHistory), nameof(CombatHistory.CardPlayStarted));
    }

    [HarmonyPostfix]
    private static void Postfix(ICombatState? __0, CardPlay __1)
    {
        HandReminders.OnCardPlayStarted(__0, __1);
    }
}
