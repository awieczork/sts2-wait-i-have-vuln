using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
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
    // A few checks a second take down the clouds of teammates who are no longer needed.
    private const double TickSeconds = 0.2;
    // The gap kept around each cloud, as a share of the viewport height.
    private const float GapRatio = 0.01f;
    // The clouds go with the hover; this only bounds one that is never taken down.
    private const double CloudSeconds = 600;
    private const double ReactionSeconds = 2.5;
    // How many frames the unseen cloud may take to reach a size that holds.
    private const int MeasureFrames = 10;

    private enum Phase
    {
        None,
        Settling,
        Measuring,
        Shown
    }

    private static readonly ConditionalWeakTable<NPlayerHand, Reminder> Reminders = new();
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

            if (HandReminderPolicy<Player, int>.ShowsCloud(card.Type == CardType.Attack, VulnerableCardPredicate.IsSupported(card.Id.Entry),
                CanAct(hand) && !CombatOver(combatState), OwnRowEligible(), Bubbles.EnemyNeedsVulnerable(combatState), MultiplayerPlayerStateIndicators.ReminderRows()))
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

    internal static void OnAttackPlayed(Player attacker, ICombatState? combat)
    {
        HookGuard.Run(nameof(OnAttackPlayed), "no teammate reacts to the Attack", () =>
        {
            if (!SettingsService.Current.ShowBubble || combat is null || NPlayerHand.Instance is not { } hand)
            {
                return;
            }

            var reminder = Reminders.GetValue(hand, _ => new Reminder(hand));
            foreach (var (player, count) in reminder.Reactions.AttackPlayed((combat, combat.RoundNumber), attacker, OwnRowEligible(),
                Bubbles.EnemyNeedsVulnerable(combat), MultiplayerPlayerStateIndicators.ReminderRows()))
            {
                if (Bubbles.Say(player, () => Bubbles.NextAngry(combat, player, attacker, count), ReactionSeconds) is not null)
                {
                    Log.Info($"[{ModEntry.ModId}] Reaction: tier {BubbleVoice.AngryTier(count) + 1}, Attack {count} this turn.");
                }
            }
        });
    }

    // In solo mode your own row stands for a teammate; with "Show icon on my own row" off it shows
    // nothing at all, as its icon and row bubble do.
    private static bool OwnRowEligible() => SoloMode.Enabled && RunManager.Instance.RunLobby is null && SettingsService.Current.ShowOnOwnRow;

    // A won combat empties the enemies and discards the hand.
    private static bool CombatOver(CombatState combat) => !CombatManager.Instance.IsInProgress || combat.HittableEnemies.Count == 0;

    // The game's own gate for card actions (private), resolved once; when a game update renames
    // it, the card reminder stays off with one warning.
    private static bool CanAct(NPlayerHand hand)
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
        private Cloud? _cloud;
        private NHandCardHolder? _holder;
        private CombatState? _combat;
        private Phase _phase;
        private CloudSpot? _spot;
        private int _tipCount;
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

        internal HandReminderPolicy<Player, (ICombatState, int)> Reactions { get; } = new();

        internal void Pend(NHandCardHolder holder, CombatState combat)
        {
            _holder = holder;
            _combat = combat;
            _phase = Phase.Settling;
            _pendedAt = Bubbles.Now;
            Follow(true);
        }

        internal void Clear()
        {
            _cloud?.Free();
            _cloud = null;
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
                // Position and TargetPosition share the hand's units, so the offset is the slide still to come.
                if (!CloudPlacement.IsSettled((_holder.Position - _holder.TargetPosition).Length(), now - _pendedAt))
                {
                    return;
                }

                // Added unseen, because its size is known only once Godot has laid it out.
                _cloud = _hand.GetParent() is { } parent ? Cloud.Create(parent, DialogueSide.Left) : null;
                _phase = Phase.Measuring;
                _frames = 0;
                if (_cloud is null)
                {
                    Clear();
                }

                return;
            }

            if (_phase == Phase.Measuring)
            {
                Measure(now);
                return;
            }

            if (now < _nextTick)
            {
                return;
            }

            _nextTick = now + TickSeconds;
            if (!HandReminderPolicy<Player, int>.ShowsCloud(true, false, CanAct(_hand) && !CombatOver(combat), OwnRowEligible(),
                Bubbles.EnemyNeedsVulnerable(combat), MultiplayerPlayerStateIndicators.ReminderRows()))
            {
                Clear();
            }
        }

        // Measures the unseen cloud each frame until its size holds, then places and shows it. The cloud
        // is added after the hand, so it draws over the resting cards.
        private void Measure(double now)
        {
            if (_cloud is null || _holder is null || _hand.GetParent() is not { } parent)
            {
                Clear();
                return;
            }

            _frames++;
            if (!_cloud.Measure() && _frames < MeasureFrames)
            {
                return;
            }

            if (_cloud.Size.X <= 0f || _cloud.Size.Y <= 0f)
            {
                Log.Info($"[{ModEntry.ModId}] Card reminder: the cloud had no size after {_frames} frames; its parts: {_cloud.DescribeParts()}.");
                Clear();
                return;
            }

            if (_spot is null)
            {
                var viewport = _hand.GetViewportRect();
                var tips = TipBoxes();
                if (CardBox(_holder) is not { } card
                    || CloudPlacement.Place(ToBox(card), tips, ToBox(viewport), _cloud.Size.X, _cloud.Size.Y, viewport.Size.Y * GapRatio) is not { } spot)
                {
                    // The numbers the placement used, so a log shows which of them leaves no room.
                    Log.Info($"[{ModEntry.ModId}] Card reminder: no free spot at the hovered card ({tips.Count} tip boxes); card {CardBox(_holder)}, "
                        + $"cloud {_cloud.Size} from {_cloud.PartCount} parts, screen {viewport}, gap {viewport.Size.Y * GapRatio}.");
                    Clear();
                    return;
                }

                _spot = spot;
                _tipCount = tips.Count;
                // The tail points back at the card: a cloud left of the card opens to the left of its spot,
                // so it is made again with that side and measured again.
                if (spot.Side == CloudSide.Left)
                {
                    _cloud.Free();
                    _cloud = Cloud.Create(parent, DialogueSide.Right);
                    _frames = 0;
                    if (_cloud is null)
                    {
                        Clear();
                    }

                    return;
                }
            }

            var placed = _spot.Value;
            _cloud.MoveTo(placed.Cloud);
            _cloud.Reveal();
            _phase = Phase.Shown;
            _nextTick = now + TickSeconds;
            var where = placed.Side switch
            {
                CloudSide.Left => "left of",
                CloudSide.Right => "right of",
                _ => "above"
            };
            Log.Info($"[{ModEntry.ModId}] Card reminder: cloud {where} the hovered card ({_tipCount} tip boxes), {_cloud.Size} from {_cloud.PartCount} parts.");
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

        internal int PartCount { get; private set; }

        internal static Cloud? Create(Node parent, DialogueSide side)
        {
            if (VulnerableIcon.Texture is not { } icon || NThoughtBubbleVfx.Create(icon, side, CloudSeconds) is not { } bubble)
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
