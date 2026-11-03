// Patches behind the "Solo mode [dev]" testing aid. They ship in every build but change
// single-player presentation only while SoloMode.Enabled is on, which it never is at game start.
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using WaitIHaveVuln.Patches;
using WaitIHaveVuln.Settings;

namespace WaitIHaveVuln.Testing;

// The container returns early unless the run has more than one player.
// The threshold becomes a call that returns zero while solo mode is on, which creates the
// local player's row in single-player runs, and the game's own 1 otherwise.
[HarmonyPatch(typeof(NMultiplayerPlayerStateContainer), nameof(NMultiplayerPlayerStateContainer.Initialize))]
internal static class MultiplayerPlayerStateContainerSoloTestRowPatch
{
    // Harmony calls it first with no target, before it binds anything.
    private static bool Prepare(MethodBase? original)
    {
        return original is not null || PatchTarget.Exists(nameof(MultiplayerPlayerStateContainerSoloTestRowPatch), "solo mode", typeof(NMultiplayerPlayerStateContainer), nameof(NMultiplayerPlayerStateContainer.Initialize));
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var matches = new List<int>();
        for (var i = 1; i + 1 < codes.Count; i++)
        {
            if (codes[i - 1].operand is MethodInfo { Name: "get_Count" }
                && codes[i].opcode == OpCodes.Ldc_I4_1
                && (codes[i + 1].opcode == OpCodes.Bgt_S || codes[i + 1].opcode == OpCodes.Bgt))
            {
                matches.Add(i);
            }
        }

        // Only a single match is surely the player-count check; with none or several, a game update
        // changed the method, and the game's own code is left as it is.
        if (matches.Count != 1)
        {
            Log.Warn($"[{ModEntry.ModId}] Solo mode [dev]: found {matches.Count} player-count checks instead of one; solo mode cannot add your row.");
            return codes;
        }

        // Changed in place, so labels and exception blocks on the instruction stay attached.
        var at = matches[0];
        codes[at].opcode = OpCodes.Call;
        codes[at].operand = AccessTools.Method(typeof(SoloMode), nameof(SoloMode.PlayerCountThreshold));
        return codes;
    }
}

// Without a lobby the nameplate resolves the net id, so show the local platform name instead.
[HarmonyPatch(typeof(NMultiplayerPlayerState), nameof(NMultiplayerPlayerState._Ready))]
internal static class MultiplayerPlayerStateSoloTestRowNamePatch
{
    // Harmony calls it first with no target, before it binds anything.
    private static bool Prepare(MethodBase? original)
    {
        return original is not null || PatchTarget.Exists(nameof(MultiplayerPlayerStateSoloTestRowNamePatch), "solo mode's name", typeof(NMultiplayerPlayerState), nameof(NMultiplayerPlayerState._Ready), null, "_nameplateLabel");
    }

    [HarmonyPostfix]
    private static void Postfix(MegaLabel ____nameplateLabel)
    {
        HookGuard.Run("Solo mode name", "the row keeps the game's name", () =>
        {
            if (!SoloMode.Enabled || RunManager.Instance.RunLobby is not null)
            {
                return;
            }

            var platform = PlatformUtil.PrimaryPlatform;
            ____nameplateLabel.SetTextAutoSize(PlatformUtil.GetPlayerNameRaw(platform, PlatformUtil.GetLocalPlayerId(platform)));
        });
    }
}
