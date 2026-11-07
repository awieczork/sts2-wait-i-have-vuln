using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using WaitIHaveVuln.Indicator;

namespace WaitIHaveVuln.Patches;

// The game's own bubbles over a creature all come from these two factory overloads: TalkCmd.Play,
// ThinkCmd.Play, the end-turn ping (FlavorSynchronizer), the refusal thoughts of a card play, auto-play,
// and the empty pile. The postfixes only record the bubble; arguments are bound by position (__0, __1, __2),
// because the game's parameter names are not in the metadata the project reads.

[HarmonyPatch(typeof(NSpeechBubbleVfx), nameof(NSpeechBubbleVfx.Create), new[] { typeof(string), typeof(Creature), typeof(double), typeof(VfxColor) })]
internal static class SpeechBubbleCreatePatch
{
    private static bool Prepare(MethodBase? original)
    {
        return original is not null || PatchTarget.Exists(nameof(SpeechBubbleCreatePatch), "giving way to the game's speech bubbles", typeof(NSpeechBubbleVfx),
            nameof(NSpeechBubbleVfx.Create), new[] { typeof(string), typeof(Creature), typeof(double), typeof(VfxColor) });
    }

    [HarmonyPostfix]
    private static void Postfix(NSpeechBubbleVfx? __result, Creature __1, double __2)
    {
        HookGuard.Run("Game speech bubble", "the mod's bubble may overlap it", () => Bubbles.OnGameBubbleCreated(__result, __1, GameBubbleTiming.SpeechSeconds(__2)));
    }
}

[HarmonyPatch(typeof(NThoughtBubbleVfx), nameof(NThoughtBubbleVfx.Create), new[] { typeof(string), typeof(Creature), typeof(double?) })]
internal static class ThoughtBubbleCreatePatch
{
    private static bool Prepare(MethodBase? original)
    {
        return original is not null || PatchTarget.Exists(nameof(ThoughtBubbleCreatePatch), "giving way to the game's thought bubbles", typeof(NThoughtBubbleVfx),
            nameof(NThoughtBubbleVfx.Create), new[] { typeof(string), typeof(Creature), typeof(double?) });
    }

    [HarmonyPostfix]
    private static void Postfix(NThoughtBubbleVfx? __result, Creature __1, double? __2)
    {
        HookGuard.Run("Game thought bubble", "the mod's bubble may overlap it", () => Bubbles.OnGameBubbleCreated(__result, __1, GameBubbleTiming.ThoughtSeconds(__2)));
    }
}
