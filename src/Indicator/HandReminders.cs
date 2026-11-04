using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Runs;
using WaitIHaveVuln.Settings;

namespace WaitIHaveVuln.Indicator;

/// <summary>
/// The card reminder and the teammates' reactions, from the rules in
/// <see cref="HandReminderPolicy{TPlayer, TTurn}"/>: one cloud with the Vulnerable icon at the hovered
/// Attack while any teammate is needed, placed by <see cref="CloudPlacement"/> once the card has
/// settled, never moved, and gone with the hover; and a line over a needed teammate when another
/// player plays an Attack. In solo mode your own row counts.
/// </summary>
internal static class HandReminders
{
    // A few checks a second bring the cloud and take it down as teammates become needed or not.
    private const double TickSeconds = 0.2;
    // The gap kept around each cloud, as a share of the viewport height.
    private const float GapRatio = 0.01f;
    private const double ReactionSeconds = 2.5;
    // How many frames the unseen cloud may take to reach a size that holds.
    private const int MeasureFrames = 10;

    private enum Phase
    {
        None,
        Settling,
        Shown,
        // Hovered, with no cloud while no teammate is needed.
        Watching
    }

    private static readonly ConditionalWeakTable<NPlayerHand, Reminder> Reminders = new();
    // The measured box of each cloud side, with the screen size it was measured at; the cloud never changes size otherwise.
    private static (Rect2 Left, Rect2 Right, Vector2 Viewport)? _knownBoxes;
    private static Func<NPlayerHand, bool>? _areCardActionsAllowed;
    private static bool _gateResolved;
    private static FieldInfo? _activeTipsField;
    private static bool _activeTipsResolved;

    internal static void OnLocalCardHovered(NPlayerHand hand, CombatState? combatState)
    {
        HookGuard.Run(nameof(OnLocalCardHovered), "no card reminder shows", () =>
        {
            var reminder = Reminders.GetValue(hand, _ => new Reminder(hand));
            reminder.Clear();
            var holder = hand.FocusedHolder;
            if (!SettingsService.Current.ShowsCardReminder || combatState is null || holder?.CardNode?.Model is not { } card)
            {
                return;
            }

            // Every hovered Attack that does not apply Vulnerable is watched while it stays hovered, so the
            // cloud comes and goes as teammates become needed or not; the rest of the rule is checked there.
            if (card.Type == CardType.Attack && !SupportedCards.IsSupported(card))
            {
                reminder.Pend(holder, combatState);
            }
        });
    }

    internal static void OnLocalCardPickedUp()
    {
        HookGuard.Run(nameof(OnLocalCardPickedUp), "the clouds stay until the hover ends", () =>
        {
            if (NPlayerHand.Instance is { } hand && Reminders.TryGetValue(hand, out var reminder))
            {
                reminder.Clear();
            }
        });
    }

    internal static void OnAttackPlayed(Player attacker, ICombatState? combat, Creature? target)
    {
        HookGuard.Run(nameof(OnAttackPlayed), "no teammate reacts to the Attack", () =>
        {
            if (!SettingsService.Current.ShowBubble || combat is null || NPlayerHand.Instance is not { } hand)
            {
                return;
            }

            // CardPlay.Target is set only for one-enemy and one-ally cards; an ally target is a player, not an enemy.
            var targetsOneEnemy = target is { IsEnemy: true };
            var hitsNeedingEnemy = VulnerableNeed.AttackHitsNeedingEnemy(targetsOneEnemy, targetsOneEnemy && Bubbles.NeedsVulnerable(target!),
                Bubbles.EnemyNeedsVulnerable(combat));
            var turn = ReactionTurn.Of(combat.RoundNumber, combat.Players.Select(player => player.PlayerCombatState?.TurnNumber ?? 0));
            var reminder = Reminders.GetValue(hand, _ => new Reminder(hand));
            foreach (var (player, count) in reminder.Reactions.AttackPlayed((combat, turn), attacker, OwnRowEligible(), hitsNeedingEnemy,
                MultiplayerPlayerStateIndicators.ReminderRows(live: true)))
            {
                if (Bubbles.React(player, () => Bubbles.NextAngry(combat, player, attacker, count), ReactionSeconds) is not null)
                {
                    Log.Info($"[{ModEntry.ModId}] Reaction: tier {BubbleVoice.AngryTier(count) + 1}, Attack {count} this turn.");
                }
            }
        });
    }

    // In solo mode your own row stands for a teammate; with "Show icon on my own row" off it shows
    // nothing at all, as its icon and row bubble do.
    private static bool OwnRowEligible() => SoloMode.Enabled && RunManager.Instance.RunLobby is null && SettingsService.Current.ShowOnOwnRow;

    // A won combat empties the enemies and discards the hand; IsInProgress stays true until the win is checked.
    private static bool CombatOver(CombatState combat)
    {
        var manager = CombatManager.Instance;
        return !manager.IsInProgress || manager.IsOverOrEnding || combat.HittableEnemies.Count == 0;
    }

    // The game's own check for playing a card from the hand (NPlayerHand.CanPlayCards, private: no card play in
    // progress and card actions allowed), no open overlay, and the local player's own Play phase, which
    // starts after the turn-start draw.
    private static bool CanAct(NPlayerHand hand, CombatState combat)
    {
        return !hand.InCardPlay && CardActionsAllowed(hand) && (NOverlayStack.Instance?.ScreenCount ?? 0) == 0
            && LocalContext.GetMe(combat)?.PlayerCombatState?.Phase == PlayerTurnPhase.Play;
    }

    // The game's own gate for card actions (private), resolved once; when a game update renames
    // it, the card reminder stays off with one warning.
    private static bool CardActionsAllowed(NPlayerHand hand)
    {
        if (!_gateResolved)
        {
            _gateResolved = true;
            var method = AccessTools.Method(typeof(NPlayerHand), "AreCardActionsAllowed");
            if (method is null)
            {
                Log.Warn($"[{ModEntry.ModId}] NPlayerHand.AreCardActionsAllowed not found; the card reminder stays off.");
            }
            else
            {
                _areCardActionsAllowed = AccessTools.MethodDelegate<Func<NPlayerHand, bool>>(method);
            }
        }

        return hand.CurrentMode == NPlayerHand.Mode.Play && (_areCardActionsAllowed?.Invoke(hand) ?? false);
    }

    /// <summary>The on-screen bounding box of a control, with its scale and rotation, or null when it has no size.</summary>
    private static Rect2? BoxOf(Control? control)
    {
        if (control is null || !GodotObject.IsInstanceValid(control) || control.Size == Vector2.Zero)
        {
            return null;
        }

        return control.GetGlobalTransform() * new Rect2(Vector2.Zero, control.Size);
    }

    // The hitbox and the card as drawn, together, because the scene sets their sizes and either may be the larger.
    private static Rect2? CardBox(NHandCardHolder holder)
    {
        var hitbox = BoxOf(holder.Hitbox);
        var art = BoxOf(holder.CardNode);
        return hitbox is { } a && art is { } b ? a.Merge(b) : hitbox ?? art;
    }

    private static Box ToBox(Rect2 rect) => new(rect.Position.X, rect.Position.Y, rect.Size.X, rect.Size.Y);

    // Every visible tip panel the game shows (private static NHoverTipSet._activeHoverTips), not the
    // containers, which span both sides of the card.
    private static List<Box> TipBoxes()
    {
        if (!_activeTipsResolved)
        {
            _activeTipsResolved = true;
            _activeTipsField = AccessTools.Field(typeof(NHoverTipSet), "_activeHoverTips");
            if (_activeTipsField is null)
            {
                Log.Warn($"[{ModEntry.ModId}] NHoverTipSet._activeHoverTips not found; the card clouds avoid only the hovered card.");
            }
        }

        var boxes = new List<Box>();
        if (_activeTipsField?.GetValue(null) is not IDictionary sets)
        {
            return boxes;
        }

        foreach (var set in sets.Values)
        {
            if (set is not Node node || !GodotObject.IsInstanceValid(node))
            {
                continue;
            }

            foreach (var container in node.GetChildren().OfType<Control>().Where(control => control.IsVisibleInTree()))
            {
                var panels = container.GetChildren().OfType<Control>().Where(control => control.IsVisibleInTree()).ToList();
                foreach (var part in panels.Count > 0 ? panels : new List<Control> { container })
                {
                    if (BoxOf(part) is { } box)
                    {
                        boxes.Add(ToBox(box));
                    }
                }
            }
        }

        return boxes;
    }

    private sealed class Reminder
    {
        private readonly NPlayerHand _hand;
        private readonly Action _step;
        // The shown cloud, and the two unseen ones measured while the hovered card settles.
        private Cloud? _cloud;
        private Cloud? _tailLeft;
        private Cloud? _tailRight;
        private bool _prepared;
        // Set while the shown cloud waits for the card to settle, to check its spot once.
        private bool _checkAfterSettling;
        private NHandCardHolder? _holder;
        private CombatState? _combat;
        private Phase _phase;
        private CloudSpot? _spot;
        private int _frames;
        private double _pendedAt;
        private double _nextTick;
        private bool _stepDone;
        private SceneTree? _tree;

        internal Reminder(NPlayerHand hand)
        {
            _hand = hand;
            // Made once, so the per-frame check allocates nothing.
            _step = () =>
            {
                Step();
                _stepDone = true;
            };
            hand.TreeExiting += Clear;
        }

        internal HandReminderPolicy<Player, (ICombatState, ReactionTurn)> Reactions { get; } = new();

        internal void Pend(NHandCardHolder holder, CombatState combat)
        {
            FreeClouds();
            _holder = holder;
            _combat = combat;
            _phase = Phase.Settling;
            _pendedAt = Bubbles.Now;
            _prepared = false;
            Follow(true);
        }

        internal void Clear()
        {
            FreeClouds();
            _phase = Phase.None;
            _spot = null;
            _holder = null;
            _combat = null;
            Follow(false);
        }

        // Every frame while clouds are pending or shown, so they go as soon as the hover ends. A
        // failure takes them down and stops the checks, so it is logged once.
        private void OnProcessFrame()
        {
            _stepDone = false;
            HookGuard.Run("Card reminder", "the clouds go", _step);
            if (!_stepDone)
            {
                Clear();
            }
        }

        private void Step()
        {
            if (!GodotObject.IsInstanceValid(_hand) || _holder is null || !GodotObject.IsInstanceValid(_holder)
                || !ReferenceEquals(_hand.FocusedHolder, _holder) || _combat is not { } combat)
            {
                Clear();
                return;
            }

            var now = Bubbles.Now;
            if (_phase == Phase.Settling)
            {
                // The rule is checked once, on the first frame of the hover, when the card's tips exist.
                if (!_prepared)
                {
                    _prepared = true;
                    if (!IsNeeded(combat))
                    {
                        _phase = Phase.Watching;
                        _nextTick = now + TickSeconds;
                        return;
                    }

                    PrepareClouds();
                }

                // The cloud's size never changes, so it is measured on the first hover only (or after the screen
                // size changes) and shown at once from then on.
                if (!CloudsKnown())
                {
                    if (!MeasureClouds() && _frames < MeasureFrames)
                    {
                        return;
                    }

                    KeepMeasuredBoxes();
                }

                Show(now);
                return;
            }

            if (_phase == Phase.Shown)
            {
                // Like the game's own tips, the cloud follows the card: it keeps its spot beside where the card
                // settles and moves with the slide still to come, so it never stands ahead of the card.
                FollowCard();
                if (_checkAfterSettling && CloudPlacement.IsSettled((_holder.Position - _holder.TargetPosition).Length(), now - _pendedAt))
                {
                    _checkAfterSettling = false;
                    MoveToSettledCard();
                }
            }

            if (now < _nextTick)
            {
                return;
            }

            _nextTick = now + TickSeconds;
            var needed = IsNeeded(combat);
            if (_phase == Phase.Shown && !needed)
            {
                // Hidden, not cleared: the hovered card stays watched and the cloud comes back when needed again.
                FreeClouds();
                _spot = null;
                _phase = Phase.Watching;
            }
            else if (_phase == Phase.Watching && needed)
            {
                _phase = Phase.Settling;
                _pendedAt = now;
                _prepared = false;
            }
        }

        // The whole rule at the hovered card now: the card must still be playable by the local player.
        private bool IsNeeded(CombatState combat)
        {
            return _holder?.CardNode?.Model is { } card
                && HandReminderPolicy<Player, int>.ShowsCloud(true, false, card.CanPlay(), CanAct(_hand, combat) && !CombatOver(combat), OwnRowEligible(),
                    Bubbles.EnemyNeedsVulnerable(combat), MultiplayerPlayerStateIndicators.ReminderRows(live: false));
        }

        // One cloud for each side the tail can point to, because a cloud left of the card has its tail on
        // the right. Both are added after the hand, so the shown one draws over the resting cards.
        private void PrepareClouds()
        {
            FreeClouds();
            if (_hand.GetParent() is not { } parent)
            {
                return;
            }

            _tailLeft = Cloud.Create(parent, DialogueSide.Left);
            _tailRight = Cloud.Create(parent, DialogueSide.Right);
            _frames = 0;
            if (_tailLeft is not null && _tailRight is not null && _knownBoxes is { } known && known.Viewport == _hand.GetViewportRect().Size)
            {
                _tailLeft.Box = known.Left;
                _tailRight.Box = known.Right;
            }
        }

        private bool CloudsKnown()
        {
            return _tailLeft is { Size: { X: > 0f, Y: > 0f } } && _tailRight is { Size: { X: > 0f, Y: > 0f } }
                && _knownBoxes is { } known && known.Viewport == _hand.GetViewportRect().Size;
        }

        /// <summary>Measures both unseen clouds once; true when both sizes have held since the last frame.</summary>
        private bool MeasureClouds()
        {
            if (_tailLeft is null || _tailRight is null)
            {
                return false;
            }

            _frames++;
            var left = _tailLeft.Measure();
            var right = _tailRight.Measure();
            return left && right;
        }

        private void KeepMeasuredBoxes()
        {
            if (_tailLeft is { Size: { X: > 0f, Y: > 0f } } left && _tailRight is { Size: { X: > 0f, Y: > 0f } } right)
            {
                _knownBoxes = (left.Box, right.Box, _hand.GetViewportRect().Size);
            }
        }

        // Shows the cloud at the spot the hovered card slides to. The tips follow the card, so they move by
        // the same slide; once the card has settled, the spot is checked once against where everything is.
        private void Show(double now)
        {
            if (_tailLeft is null || _tailRight is null || _holder is null)
            {
                Clear();
                return;
            }

            if (_tailLeft.Size.X <= 0f || _tailLeft.Size.Y <= 0f)
            {
                Log.Info($"[{ModEntry.ModId}] Card reminder: the cloud had no size after {_frames} frames; its parts: {_tailLeft.DescribeParts()}.");
                Clear();
                return;
            }

            var slide = SlideStillToCome(_holder);
            if (PlaceAt(slide) is not { } placed)
            {
                Clear();
                return;
            }

            var (spot, tipCount) = placed;
            _spot = spot;
            _cloud = spot.Side == CloudSide.Left ? _tailRight : _tailLeft;
            FollowCard();
            _cloud.Reveal();
            _phase = Phase.Shown;
            _checkAfterSettling = slide != Vector2.Zero;
            _nextTick = now + TickSeconds;
            Log.Info($"[{ModEntry.ModId}] Card reminder: cloud {Where(spot.Side)} the hovered card ({tipCount} tip boxes), {_cloud.Size} from {_cloud.PartCount} parts, after {_frames} frames.");
        }

        // The card has settled: the cloud moves only when the spot it should have now differs.
        private void MoveToSettledCard()
        {
            if (_tailLeft is null || _tailRight is null || _spot is not { } shown || PlaceAt(Vector2.Zero) is not { } placed)
            {
                return;
            }

            var spot = placed.Spot;
            if (spot.Side == shown.Side && Mathf.Abs(spot.Cloud.X - shown.Cloud.X) < 2f && Mathf.Abs(spot.Cloud.Y - shown.Cloud.Y) < 2f)
            {
                return;
            }

            var next = spot.Side == CloudSide.Left ? _tailRight : _tailLeft;
            if (!ReferenceEquals(next, _cloud))
            {
                _cloud?.Hide();
            }

            _spot = spot;
            _cloud = next;
            FollowCard();
            _cloud.Reveal();
            Log.Info($"[{ModEntry.ModId}] Card reminder: cloud moved {Where(spot.Side)} the settled card.");
        }

        // The card's box and its tips' boxes, moved by the slide still to come, placed with the cloud's size.
        private (CloudSpot Spot, int TipCount)? PlaceAt(Vector2 slide)
        {
            if (_tailLeft is null || _holder is null || CardBox(_holder) is not { } card)
            {
                return null;
            }

            var viewport = _hand.GetViewportRect();
            var tips = TipBoxes();
            for (var i = 0; i < tips.Count; i++)
            {
                tips[i] = tips[i] with { X = tips[i].X + slide.X, Y = tips[i].Y + slide.Y };
            }

            var cardBox = ToBox(new Rect2(card.Position + slide, card.Size));
            if (CloudPlacement.Place(cardBox, tips, ToBox(viewport), _tailLeft.Size.X, _tailLeft.Size.Y, viewport.Size.Y * GapRatio) is not { } spot)
            {
                // The numbers the placement used, so a log shows which of them leaves no room.
                Log.Info($"[{ModEntry.ModId}] Card reminder: no free spot at the hovered card ({tips.Count} tip boxes); card {cardBox}, "
                    + $"cloud {_tailLeft.Size} from {_tailLeft.PartCount} parts, screen {viewport}, gap {viewport.Size.Y * GapRatio}.");
                return null;
            }

            return (spot, tips.Count);
        }

        // The spot is beside where the card settles; the cloud stands back by the slide still to come. Every
        // frame while shown, with no allocation: Box and Vector2 are values.
        private void FollowCard()
        {
            if (_cloud is null || _spot is not { } spot || _holder is null || !GodotObject.IsInstanceValid(_holder))
            {
                return;
            }

            var slide = SlideStillToCome(_holder);
            _cloud.MoveTo(spot.Cloud with { X = spot.Cloud.X - slide.X, Y = spot.Cloud.Y - slide.Y });
        }

        // Position and TargetPosition share the hand's units; the parent's transform turns the offset into screen units.
        private static Vector2 SlideStillToCome(NHandCardHolder holder)
        {
            var slide = holder.TargetPosition - holder.Position;
            return holder.GetParent() is CanvasItem parent ? parent.GetGlobalTransform().BasisXform(slide) : slide;
        }

        private static string Where(CloudSide side) => side switch
        {
            CloudSide.Left => "left of",
            CloudSide.Right => "right of",
            _ => "above"
        };

        private void FreeClouds()
        {
            _tailLeft?.Free();
            _tailRight?.Free();
            _cloud = null;
            _tailLeft = null;
            _tailRight = null;
            _checkAfterSettling = false;
        }

        private void Follow(bool follow)
        {
            if (follow)
            {
                if (_tree is null && GodotObject.IsInstanceValid(_hand) && _hand.GetTree() is { } tree)
                {
                    tree.ProcessFrame += OnProcessFrame;
                    _tree = tree;
                }

                return;
            }

            if (_tree is not null && GodotObject.IsInstanceValid(_tree))
            {
                _tree.ProcessFrame -= OnProcessFrame;
            }

            _tree = null;
        }
    }

    // One card cloud: the game's thought cloud inside a node of the mod's own, which carries the size,
    // because the cloud's pop-in animation sets the cloud's own scale from 75% to 100%. It starts unseen
    // and is measured as drawn: its container has no height of its own, and the parts that draw it get
    // their sizes only when Godot lays them out.
    private sealed class Cloud
    {
        private const int MaxDescribedParts = 12;
        private readonly Control _root;
        private readonly Control _bubble;
        // The cloud's box once fully grown, in screen units, relative to the root's global position.
        private Rect2 _box;

        private Cloud(Control root, Control bubble)
        {
            _root = root;
            _bubble = bubble;
        }

        internal Vector2 Size => _box.Size;

        /// <summary>The measured box, kept so a later cloud of the same side can show without measuring.</summary>
        internal Rect2 Box
        {
            get => _box;
            set => _box = value;
        }

        internal int PartCount { get; private set; }

        internal static Cloud? Create(Node parent, DialogueSide side)
        {
            if (VulnerableIcon.Texture is not { } icon || NThoughtBubbleVfx.Create(icon, side, null) is not { } bubble)
            {
                return null;
            }

            var root = new Control
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Scale = new Vector2(Bubbles.CloudScale, Bubbles.CloudScale),
                Modulate = new Color(1f, 1f, 1f, 0f)
            };
            parent.AddChild(root);
            root.AddChild(bubble);
            IgnoreMouse(bubble);
            return new Cloud(root, bubble);
        }

        /// <summary>Measures the cloud as drawn now; true once it has a size that held since the last call.</summary>
        internal bool Measure()
        {
            if (!GodotObject.IsInstanceValid(_root) || !GodotObject.IsInstanceValid(_bubble))
            {
                return false;
            }

            // Through the cloud's own transform, so the animation's current scale does not count; the
            // cloud sits at the root's origin and grows to scale 1 there. The cloud's root node is left
            // out, because it is a frame for its parts, not part of the drawing.
            Rect2? local = null;
            var parts = 0;
            var toCloud = _bubble.GetGlobalTransform().AffineInverse();
            foreach (var child in _bubble.GetChildren())
            {
                Collect(child, toCloud, ref local, ref parts);
            }

            var onScreen = _root.GetGlobalTransform() * (local ?? default);
            var box = new Rect2(onScreen.Position - _root.GlobalPosition, onScreen.Size);
            var held = box.Size.X > 0f && box.Size.Y > 0f && box.IsEqualApprox(_box);
            _box = box;
            PartCount = parts;
            return held;
        }

        internal void Reveal()
        {
            if (GodotObject.IsInstanceValid(_root))
            {
                _root.Modulate = Colors.White;
            }
        }

        internal void Hide()
        {
            if (GodotObject.IsInstanceValid(_root))
            {
                _root.Modulate = new Color(1f, 1f, 1f, 0f);
            }
        }

        // For the log when the cloud never gets a size: its parts with their type and size.
        internal string DescribeParts()
        {
            var parts = new List<string>();
            if (GodotObject.IsInstanceValid(_bubble))
            {
                Describe(_bubble, parts);
            }

            return string.Join("; ", parts);
        }

        private static void Collect(Node node, Transform2D toCloud, ref Rect2? local, ref int parts)
        {
            if (node is not CanvasItem item || !item.IsVisibleInTree())
            {
                return;
            }

            Rect2? own = node switch
            {
                Control control when control.Size.X > 0f && control.Size.Y > 0f => new Rect2(Vector2.Zero, control.Size),
                Sprite2D sprite when sprite.Texture is not null => sprite.GetRect(),
                _ => null
            };
            if (own is { } rect)
            {
                var placed = toCloud * item.GetGlobalTransform() * rect;
                local = local is { } union ? union.Merge(placed) : placed;
                parts++;
            }

            foreach (var child in node.GetChildren())
            {
                Collect(child, toCloud, ref local, ref parts);
            }
        }

        private static void Describe(Node node, List<string> parts)
        {
            if (parts.Count >= MaxDescribedParts)
            {
                return;
            }

            var size = node switch
            {
                Control control => control.Size.ToString(),
                Sprite2D sprite => sprite.GetRect().Size.ToString(),
                _ => "-"
            };
            var visible = node is CanvasItem item ? item.IsVisibleInTree() : false;
            parts.Add($"{node.Name} {node.GetType().Name} {size}{(visible ? string.Empty : " hidden")}");
            foreach (var child in node.GetChildren())
            {
                Describe(child, parts);
            }
        }

        internal void MoveTo(Box spot)
        {
            if (GodotObject.IsInstanceValid(_root))
            {
                _root.GlobalPosition = new Vector2(spot.X, spot.Y) - _box.Position;
            }
        }

        internal void Free()
        {
            if (GodotObject.IsInstanceValid(_root))
            {
                _root.QueueFree();
            }
        }

        // The cloud must never take the pointer from the cards under it.
        private static void IgnoreMouse(Node node)
        {
            if (node is Control control)
            {
                control.MouseFilter = Control.MouseFilterEnum.Ignore;
            }

            foreach (var child in node.GetChildren())
            {
                IgnoreMouse(child);
            }
        }
    }
}
