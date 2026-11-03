using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using WaitIHaveVuln.Settings;

namespace WaitIHaveVuln.Indicator;

/// <summary>
/// The character bubbles: which line comes next, who is named in it, and showing it through the
/// game's own speech bubble over the player's creature, one bubble per player at a time, so no two
/// bubbles on screen say the same.
/// </summary>
internal static class Bubbles
{
    // The game draws the thought cloud larger than the speech bubble; this brings it to about the same size.
    internal const float CloudScale = 0.55f;
    private static readonly Random Random = new();
    private static readonly BubbleBoard Board = new();
    private static readonly BubbleVoice Voice = new(Random);
    private static readonly ConditionalWeakTable<Player, Spoken> Speaking = new();

    internal static double Now => Time.GetTicksMsec() / 1000.0;

    /// <summary>Whether an enemy that can be hit and can take powers still lacks Vulnerable.</summary>
    internal static bool EnemyNeedsVulnerable(ICombatState? combat)
    {
        return combat?.HittableEnemies.Any(enemy => enemy.IsAlive && enemy.CanReceivePowers && !enemy.HasPower<VulnerablePower>()) ?? false;
    }

    internal static BubbleLine? NextRow(ICombatState? combat, Player speaker)
    {
        return Voice.NextRow(SettingsService.Current.VariedBubbleLines, combat, IsOnScreen, YourName(combat, speaker), NameOf(speaker));
    }

    internal static BubbleLine? NextAngry(ICombatState? combat, Player speaker, Player attacker, int attackCount)
    {
        return Voice.NextAngry(SettingsService.Current.VariedBubbleLines, attackCount, IsOnScreen, YourName(combat, speaker), NameOf(speaker), NameOf(attacker));
    }

    /// <summary>
    /// A line over the speaker's creature in the character's colour, as the game's own banter shows. It
    /// replaces the speaker's bubble still on screen, whose line counts as free for the pick; when there is
    /// no new line, the old bubble stays. Returns the line shown, or null when there was none to show.
    /// </summary>
    internal static BubbleLine? Say(Player speaker, Func<BubbleLine?> pick, double seconds)
    {
        var creature = speaker.Creature;
        if (creature is null || creature.IsDead)
        {
            return null;
        }

        Speaking.TryGetValue(speaker, out var current);
        current?.Release();
        if (pick() is not { } line || NSpeechBubbleVfx.Create(line.Text, creature, seconds, speaker.Character?.SpeechBubbleColor ?? VfxColor.White) is not { } bubble)
        {
            current?.Restore();
            return null;
        }

        current?.TakeDown();

        creature.GetVfxContainer()?.AddChildSafely(bubble);
        var until = Now + seconds;
        Board.Add(line, until);
        Speaking.AddOrUpdate(speaker, new Spoken(bubble, line, until));
        return line;
    }

    /// <summary>A player's name for a bubble, as the player list shows it; in solo mode every row is you. Null when it cannot be read.</summary>
    internal static string? NameOf(Player player)
    {
        try
        {
            if (RunManager.Instance.RunLobby is null)
            {
                var platform = PlatformUtil.PrimaryPlatform;
                return BubbleLines.CleanName(PlatformUtil.GetPlayerNameRaw(platform, PlatformUtil.GetLocalPlayerId(platform)));
            }

            return BubbleLines.CleanName(PlatformUtil.GetPlayerNameRaw(RunManager.Instance.NetService.Platform, player.NetId));
        }
        catch (Exception)
        {
            return null;
        }
    }

    // The local player's name, except on their own row in co-op, where they would address themselves;
    // in solo mode every row is theirs, and talking to yourself is the joke.
    private static string? YourName(ICombatState? combat, Player speaker)
    {
        if (RunManager.Instance.RunLobby is not null && LocalContext.IsMe(speaker))
        {
            return null;
        }

        return combat is not null && LocalContext.GetMe(combat) is { } me ? NameOf(me) : null;
    }

    private static bool IsOnScreen(BubbleLine line)
    {
        return Board.Contains(line, Now);
    }

    // A speech bubble still on screen or gone by itself. Its line leaves the board while a replacement
    // is picked, and comes back when there is none.
    private sealed class Spoken
    {
        private readonly Node _bubble;
        private readonly BubbleLine _line;
        private readonly double _until;
        private bool _released;

        internal Spoken(Node bubble, BubbleLine line, double until)
        {
            _bubble = bubble;
            _line = line;
            _until = until;
        }

        internal void Release()
        {
            if (Now < _until)
            {
                Board.Remove(_line);
                _released = true;
            }
        }

        internal void Restore()
        {
            if (_released && Now < _until)
            {
                Board.Add(_line, _until);
            }

            _released = false;
        }

        internal void TakeDown()
        {
            if (GodotObject.IsInstanceValid(_bubble))
            {
                _bubble.QueueFree();
            }
        }
    }
}
