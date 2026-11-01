#if SOLO_TEST_ROW
using System.Reflection;
using System.Reflection.Emit;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
#endif
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

#if SOLO_TEST_ROW
// Test builds only: the container returns early unless the run has more than one player.
// Lowering that threshold to zero creates the local player's row in single-player runs.
[HarmonyPatch(typeof(NMultiplayerPlayerStateContainer), nameof(NMultiplayerPlayerStateContainer.Initialize))]
internal static class MultiplayerPlayerStateContainerSoloTestRowPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        for (var i = 1; i + 1 < codes.Count; i++)
        {
            if (codes[i - 1].operand is MethodInfo { Name: "get_Count" }
                && codes[i].opcode == OpCodes.Ldc_I4_1
                && (codes[i + 1].opcode == OpCodes.Bgt_S || codes[i + 1].opcode == OpCodes.Bgt))
            {
                codes[i].opcode = OpCodes.Ldc_I4_0;
                return codes;
            }
        }

        Log.Warn($"[{ModEntry.ModId}] Solo test row: player-count check not found; single-player rows stay disabled.");
        return codes;
    }
}

// Test builds only: without a lobby the nameplate resolves the net id, so show the local platform name instead.
[HarmonyPatch(typeof(NMultiplayerPlayerState), nameof(NMultiplayerPlayerState._Ready))]
internal static class MultiplayerPlayerStateSoloTestRowNamePatch
{
    [HarmonyPostfix]
    private static void Postfix(MegaLabel ____nameplateLabel)
    {
        if (RunManager.Instance.RunLobby is not null)
        {
            return;
        }

        var platform = PlatformUtil.PrimaryPlatform;
        ____nameplateLabel.SetTextAutoSize(PlatformUtil.GetPlayerNameRaw(platform, PlatformUtil.GetLocalPlayerId(platform)));
    }
}
#endif
