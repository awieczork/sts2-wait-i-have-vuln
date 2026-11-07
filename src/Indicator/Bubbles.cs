using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
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
/// bubbles on screen say the same. The game's own bubbles over a player win: a mod bubble comes down
/// when one appears, an arrival waits until it ends, and a reaction due during it is dropped.
/// </summary>
internal static class Bubbles
{
    private static readonly Random Random = new();
    private static readonly BubbleBoard Board = new();
    private static readonly BubbleVoice Voice = new(Random);
    private static readonly ConditionalWeakTable<Player, Spoken> Speaking = new();
    private static readonly ConditionalWeakTable<Creature, GameBubble> GameBubbles = new();
    private static readonly ArrivalQueue<Player> Arrivals = new();

    private enum Kind
    {
        Arrival,
        Reaction
    }

    internal static double Now => Time.GetTicksMsec() / 1000.0;

    /// <summary>True while the mod creates its own speech bubble, so <see cref="OnGameBubbleCreated"/> ignores it.</summary>
    private static bool CreatingOwn { get; set; }

    /// <summary>Whether a hittable enemy still needs Vulnerable (<see cref="VulnerableNeed"/>); hittable already means alive and not shielded from hits.</summary>
    internal static bool EnemyNeedsVulnerable(ICombatState? combat)
    {
        return combat?.HittableEnemies.Any(NeedsVulnerable) ?? false;
    }

    internal static bool NeedsVulnerable(Creature enemy)
    {
        return VulnerableNeed.Of(enemy.HasPower<VulnerablePower>(), enemy.HasPower<ArtifactPower>());
    }

    internal static BubbleLine? NextRow(ICombatState? combat)
    {
        return Voice.NextRow(SettingsService.Current.VariedBubbleLines, combat, IsOnScreen, YourName(combat));
    }

    internal static BubbleLine? NextAngry(ICombatState? combat, Player attacker, int attackCount)
    {
        return Voice.NextAngry(SettingsService.Current.VariedBubbleLines, attackCount, IsOnScreen, YourName(combat), NameOf(attacker));
    }

    /// <summary>The arrival bubble: shows now, or waits while a game bubble shows over the player. Returns what happened, for the log.</summary>
    internal static string Arrive(Player speaker, Func<BubbleLine?> pick, double seconds)
    {
        if (!Arrivals.Arrive(speaker, IsGameBubbleShowing(speaker.Creature)))
        {
            return "waiting for the game's bubble";
        }

        return Say(speaker, pick, seconds, Kind.Arrival) is null ? "no line to show" : "shown";
    }

    /// <summary>A reaction line, dropped while a game bubble shows over the player. Returns the line shown, or null.</summary>
    internal static BubbleLine? React(Player speaker, Func<BubbleLine?> pick, double seconds)
    {
        return IsGameBubbleShowing(speaker.Creature) ? null : Say(speaker, pick, seconds, Kind.Reaction);
    }

    /// <summary>The player's icon left full: their arrival bubble, shown or waiting, has nothing left to announce.</summary>
    internal static void LeftFull(Player player)
    {
        Arrivals.Cancel(player);
        if (Speaking.TryGetValue(player, out var current) && current.Kind == Kind.Arrival)
        {
            current.TakeDown();
            Speaking.Remove(player);
        }
    }

    /// <summary>Takes down any bubble of the player, as when they die.</summary>
    internal static void TakeDown(Player player)
    {
        Arrivals.Cancel(player);
        if (Speaking.TryGetValue(player, out var current))
        {
            current.TakeDown();
            Speaking.Remove(player);
        }
    }

    /// <summary>
    /// The game created a speech or thought bubble over <paramref name="creature"/> (any caller but the mod):
    /// the mod's bubble over that player comes down, and the game bubble holds arrivals back until it is freed
    /// or its time is up.
    /// </summary>
    internal static void OnGameBubbleCreated(Node? bubble, Creature? creature, double holdSeconds)
    {
        if (CreatingOwn || bubble is null || creature?.Player is not { } player)
        {
            return;
        }

        if (Speaking.TryGetValue(player, out var current))
        {
            current.TakeDown();
            Speaking.Remove(player);
        }

        GameBubbles.AddOrUpdate(creature, new GameBubble(bubble, Now + holdSeconds));
        // The node holds this handler and is freed with it; it fires when the bubble leaves the tree.
        bubble.TreeExiting += () => HookGuard.Run("Game bubble ended", "a waiting arrival bubble stays unsaid", () => GameBubbleEnded(creature, bubble));
    }

    private static bool IsGameBubbleShowing(Creature? creature)
    {
        return creature is not null && GameBubbles.TryGetValue(creature, out var game) && game.IsShowing;
    }

    /// <summary>
    /// A line over the speaker's creature in the character's colour, as the game's own banter shows. It
    /// replaces the speaker's bubble still on screen, whose line counts as free for the pick; when there is
    /// no new line, the old bubble stays. Returns the line shown, or null when there was none to show.
    /// </summary>
    private static BubbleLine? Say(Player speaker, Func<BubbleLine?> pick, double seconds, Kind kind)
    {
        var creature = speaker.Creature;
        if (creature is null || creature.IsDead || creature.GetVfxContainer() is not { } container)
        {
            return null;
        }

        Speaking.TryGetValue(speaker, out var current);
        current?.Release();
        if (pick() is not { } line || CreateOwn(line.Text, creature, seconds, speaker.Character?.SpeechBubbleColor ?? VfxColor.White) is not { } bubble)
        {
            current?.Restore();
            return null;
        }

        current?.TakeDown();

        container.AddChildSafely(bubble);
        var until = Now + seconds;
        Board.Add(line, until);
        Speaking.AddOrUpdate(speaker, new Spoken(bubble, line, until, kind));
        return line;
    }

    private static NSpeechBubbleVfx? CreateOwn(string text, Creature creature, double seconds, VfxColor color)
    {
        CreatingOwn = true;
        try
        {
            return NSpeechBubbleVfx.Create(text, creature, seconds, color);
        }
        finally
        {
            CreatingOwn = false;
        }
    }

    private static void GameBubbleEnded(Creature creature, Node bubble)
    {
        // Only the creature's latest game bubble counts; an older one leaving changes nothing.
        if (!GameBubbles.TryGetValue(creature, out var game) || !ReferenceEquals(game.Node, bubble))
        {
            return;
        }

        GameBubbles.Remove(creature);
        if (creature.Player is { } player && Arrivals.GameBubbleEnded(player))
        {
            // Deferred, because the bubble is leaving the tree inside this call.
            Callable.From(() => HookGuard.Run("Waiting arrival", "the arrival bubble stays unsaid", () => MultiplayerPlayerStateIndicators.RetryArrival(player))).CallDeferred();
        }
    }

    /// <summary>A player's name for a bubble, as the player list shows it. Null when it cannot be read.</summary>
    private static string? NameOf(Player player)
    {
        try
        {
            return BubbleLines.CleanName(PlatformUtil.GetPlayerNameRaw(RunManager.Instance.NetService.Platform, player.NetId));
        }
        catch (Exception)
        {
            return null;
        }
    }

    // The local player's name; only a teammate speaks, so the line addresses you.
    private static string? YourName(ICombatState? combat)
    {
        return combat is not null && LocalContext.GetMe(combat) is { } me ? NameOf(me) : null;
    }

    private static bool IsOnScreen(BubbleLine line)
    {
        return Board.Contains(line, Now);
    }

    // A game bubble over a creature, showing until it is freed or its time, plus a margin, is up.
    private sealed class GameBubble
    {
        private readonly double _until;

        internal GameBubble(Node node, double until)
        {
            Node = node;
            _until = until;
        }

        internal Node Node { get; }

        internal bool IsShowing => GodotObject.IsInstanceValid(Node) && !Node.IsQueuedForDeletion() && Now < _until;
    }

    // A speech bubble still on screen or gone by itself. Its line leaves the board while a replacement
    // is picked, and comes back when there is none.
    private sealed class Spoken
    {
        private readonly Node _bubble;
        private readonly BubbleLine _line;
        private readonly double _until;
        private bool _released;

        internal Spoken(Node bubble, BubbleLine line, double until, Kind kind)
        {
            _bubble = bubble;
            _line = line;
            _until = until;
            Kind = kind;
        }

        internal Kind Kind { get; }

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

        // A bubble taken down early frees its line for the next pick; once its time is up the line is off the
        // board already, and the same line may then be on screen for another player.
        internal void TakeDown()
        {
            if (!_released && Now < _until)
            {
                Board.Remove(_line);
            }

            _released = true;
            if (GodotObject.IsInstanceValid(_bubble))
            {
                _bubble.QueueFree();
            }
        }
    }
}
