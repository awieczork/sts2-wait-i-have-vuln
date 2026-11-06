using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;

namespace WaitIHaveVuln.HandPreview;

/// <summary>One hand preview per co-op row, kept apart from the Vulnerable indicator so either works without the other.</summary>
internal static class HandPreviews
{
    private static readonly ConditionalWeakTable<NMultiplayerPlayerState, RowHandPreview> Previews = new();

    internal static void OnCombatSetUp(NMultiplayerPlayerState row, Control counter, HBoxContainer topLine, NHealthBar healthBar)
    {
        HookGuard.Run("Hand preview combat setup", "the hand preview stays off for this row", () =>
            Previews.GetValue(row, state => new RowHandPreview(state, counter, topLine, healthBar)).BeginCombat());
    }

    internal static void OnCombatEnded(NMultiplayerPlayerState row)
    {
        HookGuard.Run("Hand preview combat end", "the hand preview may stay open until the row leaves", () =>
        {
            if (Previews.TryGetValue(row, out var preview))
            {
                preview.EndCombat();
            }
        });
    }

    internal static void OnExitTree(NMultiplayerPlayerState row)
    {
        HookGuard.Run("Hand preview cleanup", "the preview's cards may not return to the game's pool", () =>
        {
            if (Previews.TryGetValue(row, out var preview))
            {
                preview.Dispose();
                Previews.Remove(row);
            }
        });
    }
}
