using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using WaitIHaveVuln.Settings;

namespace WaitIHaveVuln.Indicator;

internal static class MultiplayerPlayerStateIndicators
{
    private static readonly ConditionalWeakTable<NMultiplayerPlayerState, RowIndicator> Indicators = new();
    // Subscribed while any row is in a combat. The tracker belongs to the one CombatManager and the executor to
    // the run, so each is kept to unsubscribe from the same instance.
    private static CombatStateTracker? _tracker;
    private static ActionExecutor? _executor;
    // Set while a one-frame re-check is due for a row whose change waited for a running action.
    private static SceneTree? _recheckTree;

    internal static void OnCombatSetUp(NMultiplayerPlayerState row, HBoxContainer topContainer, NHealthBar healthBar, TextureRect portrait)
    {
        Guard(nameof(OnCombatSetUp), () =>
        {
            var indicator = Indicators.GetValue(row, state => new RowIndicator(state, topContainer, healthBar, portrait));
            indicator.BeginCombat(IsConnected(row));
            Subscribe();
            indicator.Refresh(CanCommit());
        });
    }

    internal static void OnConnectionStateChanged(NMultiplayerPlayerState row)
    {
        Guard(nameof(OnConnectionStateChanged), () =>
        {
            if (Indicators.TryGetValue(row, out var indicator))
            {
                indicator.UpdateConnectionState(IsConnected(row));
                indicator.Refresh(CanCommit());
            }
        });
    }

    internal static void OnCombatEnded(NMultiplayerPlayerState row)
    {
        Guard(nameof(OnCombatEnded), () =>
        {
            if (Indicators.TryGetValue(row, out var indicator))
            {
                indicator.EndCombat();
            }

            UnsubscribeWhenIdle();
        });
    }

    internal static void OnExitTree(NMultiplayerPlayerState row)
    {
        Guard(nameof(OnExitTree), () =>
        {
            if (Indicators.TryGetValue(row, out var indicator))
            {
                indicator.Dispose();
                Indicators.Remove(row);
            }

            UnsubscribeWhenIdle();
        });
    }

    /// <summary>
    /// The rows in their on-screen order, from the indicator state rather than the icon, so the icon
    /// options do not change who gets a reminder. With <paramref name="live"/>, teammates' rows are read
    /// from the game now, as for a reaction while an Attack is being played; the own row, which no
    /// reminder counts, keeps its committed level.
    /// </summary>
    internal static List<HandReminderRow<Player>> ReminderRows(bool live)
    {
        var combat = CombatManager.Instance;
        var rows = new List<(int Index, HandReminderRow<Player> Row)>();
        foreach (var (row, indicator) in Indicators)
        {
            if (!GodotObject.IsInstanceValid(row))
            {
                continue;
            }

            var player = row.Player;
            var isMe = LocalContext.IsMe(player);
            var isFull = live && !isMe ? indicator.LiveLevel() == IndicatorLevel.Full : indicator.IsFull;
            rows.Add((row.GetIndex(), new HandReminderRow<Player>(player, isMe, isFull, combat.IsPlayerReadyToEndTurn(player))));
        }

        return rows.OrderBy(entry => entry.Index).Select(entry => entry.Row).ToList();
    }

    /// <summary>The game's bubble over <paramref name="player"/> ended while their arrival bubble waited: it shows now if the row is still full.</summary>
    internal static void RetryArrival(Player player)
    {
        foreach (var (row, indicator) in Indicators)
        {
            if (GodotObject.IsInstanceValid(row) && ReferenceEquals(row.Player, player))
            {
                indicator.RetryArrival();
                return;
            }
        }
    }

    private static void Subscribe()
    {
        if (_tracker is null)
        {
            _tracker = CombatManager.Instance.StateTracker;
            _tracker.CombatStateChanged += OnCombatStateChanged;
        }

        if (_executor is null && RunManager.Instance.ActionExecutor is { } executor)
        {
            _executor = executor;
            _executor.AfterActionExecuted += OnActionExecuted;
        }
    }

    private static void UnsubscribeWhenIdle()
    {
        foreach (var (_, indicator) in Indicators)
        {
            if (indicator.InCombat)
            {
                return;
            }
        }

        if (_tracker is not null)
        {
            _tracker.CombatStateChanged -= OnCombatStateChanged;
            _tracker = null;
        }

        if (_executor is not null)
        {
            _executor.AfterActionExecuted -= OnActionExecuted;
            _executor = null;
        }

        CancelRecheck();
    }

    // Merged by the game to at most one call per frame, a frame after the change; it covers energy,
    // stars, turn phase, piles, card costs, powers, deaths, and the combat history.
    private static void OnCombatStateChanged(CombatState _)
    {
        HookGuard.Run("Combat state change", "the row icons keep their state", RefreshAll);
    }

    // The settled point after an action, which a change part-way through it waited for.
    private static void OnActionExecuted(GameAction _)
    {
        HookGuard.Run("Action executed", "the row icons keep their state", RefreshAll);
    }

    private static void RefreshAll()
    {
        var canCommit = CanCommit();
        var pending = false;
        foreach (var (row, indicator) in Indicators)
        {
            if (GodotObject.IsInstanceValid(row))
            {
                indicator.Refresh(canCommit);
                pending |= indicator.IsPending;
            }
        }

        if (pending)
        {
            ScheduleRecheck();
        }
    }

    // A change that waited for a running action is checked again on the next frame, and each frame after
    // while it still waits, because an action that pauses for a player's choice raises no event; the checks
    // stop as soon as no row waits.
    private static void ScheduleRecheck()
    {
        if (_recheckTree is null && Engine.GetMainLoop() is SceneTree tree)
        {
            tree.ProcessFrame += OnRecheckFrame;
            _recheckTree = tree;
        }
    }

    private static void CancelRecheck()
    {
        if (_recheckTree is not null && GodotObject.IsInstanceValid(_recheckTree))
        {
            _recheckTree.ProcessFrame -= OnRecheckFrame;
        }

        _recheckTree = null;
    }

    private static void OnRecheckFrame()
    {
        CancelRecheck();
        HookGuard.Run("Pending row check", "the row icons keep their state", RefreshAll);
    }

    // An action paused for a player's choice stays the executor's running action until another one
    // starts, so its state, not only whether one is set, tells whether one is running.
    private static bool CanCommit()
    {
        var phase = RunManager.Instance.ActionExecutor?.CurrentlyRunningAction?.State switch
        {
            GameActionState.WaitingForExecution or GameActionState.Executing or GameActionState.ReadyToResumeExecuting => ActionPhase.Running,
            GameActionState.GatheringPlayerChoice => ActionPhase.WaitingForChoice,
            _ => ActionPhase.None
        };
        return IndicatorRule.CanCommit(phase);
    }

    private static void Guard(string hook, Action action)
    {
        HookGuard.Run(hook, "the indicator keeps its current state", action);
    }

    private static bool IsConnected(NMultiplayerPlayerState row)
    {
        var lobby = RunManager.Instance.RunLobby;
        // Single-player runs have no lobby, and the game creates no rows there.
        if (lobby is null)
        {
            return false;
        }

        return lobby.ConnectedPlayerIds.Contains(row.Player.NetId);
    }

    private sealed class RowIndicator : IDisposable
    {
        private const float IconSize = 36f;
        private const float Gap = 12f;
        private const double SpeechSeconds = 4.0;
        // The dimmed icon: darker and half transparent, a starting point for the owner's judgement in game.
        private static readonly Color DimmedModulate = new(0.6f, 0.6f, 0.6f, 0.55f);
        private readonly NMultiplayerPlayerState _row;
        private readonly HBoxContainer _topContainer;
        private readonly Control? _hpBar;
        private readonly Control _portrait;
        // The health bar and the portrait, kept in one array, because the placement reads them every frame.
        private readonly Control?[] _layoutParts;
        private readonly RowIndicatorState _state = new();
        private TextureRect? _icon;
        private SceneTree? _frameTree;
        private bool _combatActive;
        private bool _connected;
        private bool _failed;

        internal RowIndicator(NMultiplayerPlayerState row, HBoxContainer topContainer, NHealthBar healthBar, TextureRect portrait)
        {
            _row = row;
            _topContainer = topContainer;
            // The visible bar, not the whole health bar node, which also holds the block badge.
            _hpBar = healthBar.HpBarContainer;
            _portrait = portrait;
            _layoutParts = new Control?[] { _hpBar, portrait };
        }

        internal bool InCombat => _combatActive;

        internal bool IsFull => _combatActive && _state.Committed == IndicatorLevel.Full;

        internal bool IsPending => _combatActive && !_failed && _state.IsPending;

        internal void BeginCombat(bool connected)
        {
            _combatActive = true;
            _connected = connected;
            _failed = false;
        }

        internal void UpdateConnectionState(bool connected)
        {
            _connected = connected;
        }

        internal void EndCombat()
        {
            _combatActive = false;
            Apply(_state.Reset());
        }

        public void Dispose()
        {
            _combatActive = false;
            Apply(_state.Reset());
            SetLook(IndicatorLevel.Off);
        }

        // Runs inside the game's state and action events, up to once a frame, so a failure must not reach
        // them, and one that would repeat is logged once and stops this row's refreshes until the next combat.
        internal void Refresh(bool canCommit)
        {
            if (!_combatActive || _failed)
            {
                return;
            }

            try
            {
                if (_row.Player.Creature is { IsDead: true })
                {
                    Bubbles.TakeDown(_row.Player);
                }

                Apply(_state.Observe(LiveLevel(), canCommit));
            }
            catch (Exception exception)
            {
                _failed = true;
                Log.Warn($"[{ModEntry.ModId}] Row refresh failed; the row keeps its icon as it was until the next combat. {exception}");
            }
        }

        /// <summary>The level the game state gives now; the game's play check runs only for a player who could act.</summary>
        internal IndicatorLevel LiveLevel()
        {
            var combat = CombatManager.Instance;
            var player = _row.Player;
            var playerState = player.PlayerCombatState;
            var combatLive = _combatActive && combat.IsInProgress && !combat.IsOverOrEnding;
            var isAlive = player.Creature is { IsAlive: true };
            var isInPlayPhase = playerState?.Phase == PlayerTurnPhase.Play;
            if (!combatLive || !_connected || !isAlive || !isInPlayPhase || playerState is null)
            {
                return IndicatorLevel.Off;
            }

            var holds = false;
            var canPlay = false;
            foreach (var card in playerState.Hand.Cards)
            {
                if (!SupportedCards.IsSupported(card))
                {
                    continue;
                }

                holds = true;
                if (card.CanPlay())
                {
                    canPlay = true;
                    break;
                }
            }

            return IndicatorRule.Level(combatLive, _connected, isAlive, isInPlayPhase, holds, canPlay);
        }

        internal void RetryArrival()
        {
            if (IsFull && SettingsService.Current is { ShowBubble: true } settings
                && settings.ShownLevel(IndicatorLevel.Full, LocalContext.IsMe(_row.Player)) == IndicatorLevel.Full)
            {
                ShowSpeechBubble(afterGameBubble: true);
            }
        }

        // The settings are read here and never change while rows exist, because the game
        // greys out Mod Settings during a run.
        private void Apply(IndicatorChange? change)
        {
            if (change is not { } committed)
            {
                return;
            }

            var settings = SettingsService.Current;
            var shown = settings.ShownLevel(committed.To, LocalContext.IsMe(_row.Player));
            SetLook(shown);
            if (committed.LeftFull)
            {
                Bubbles.LeftFull(_row.Player);
            }

            if (committed.BecameFull && shown == IndicatorLevel.Full && settings.ShowBubble)
            {
                ShowSpeechBubble(afterGameBubble: false);
            }
        }

        private void SetLook(IndicatorLevel shown)
        {
            if (shown == IndicatorLevel.Off)
            {
                RemoveIcon();
                return;
            }

            if (_icon is null || !GodotObject.IsInstanceValid(_icon))
            {
                // The icon is the primary signal; without its texture the row shows nothing.
                if (VulnerableIcon.Texture is not { } texture)
                {
                    return;
                }

                // ExpandMode comes before any size: under the default KeepSize, Godot clamps Size up to
                // the texture's own size, and switching to IgnoreSize afterwards does not shrink it back.
                var size = IconSize * SettingsService.Current.IconScale;
                // Named with the mod id, as every node the mod adds to a row, so the hand preview can
                // start after it.
                var icon = new TextureRect
                {
                    Name = $"{ModEntry.ModId}VulnerableIcon",
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    Texture = texture,
                    CustomMinimumSize = new Vector2(size, size),
                    Size = new Vector2(size, size),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                _row.AddChild(icon);
                _icon = icon;
                UpdateIconPosition();
                FollowLayout(true);
            }

            _icon.Modulate = shown == IndicatorLevel.Dimmed ? DimmedModulate : Colors.White;
        }

        private void RemoveIcon()
        {
            if (_icon is null)
            {
                return;
            }

            FollowLayout(false);
            if (GodotObject.IsInstanceValid(_icon))
            {
                _icon.Visible = false;
                _icon.QueueFree();
            }

            _icon = null;
        }

        // Only while the card can still help a teammate: the row is a teammate's,
        // its player has not ended the turn, and an enemy needs Vulnerable. Each outcome is logged, so a
        // player's godot.log shows what the bubbles did, as it does for reactions.
        private void ShowSpeechBubble(bool afterGameBubble)
        {
            var player = _row.Player;
            var creature = player.Creature;
            if (creature is null || creature.IsDead)
            {
                return;
            }

            var combat = creature.CombatState;
            var isMe = LocalContext.IsMe(player);
            var row = new HandReminderRow<Player>(player, isMe, true, CombatManager.Instance.IsPlayerReadyToEndTurn(player));
            var outcome = HandReminderPolicy<Player, int>.SkipReason(row, Bubbles.EnemyNeedsVulnerable(combat)) is { } reason
                ? $"skipped, {reason}"
                : Bubbles.Arrive(player, () => Bubbles.NextRow(combat, player), SpeechSeconds);
            Log.Info($"[{ModEntry.ModId}] Arrival bubble{(afterGameBubble ? " after the game's bubble" : "")}: {outcome} ({(isMe ? "own" : "teammate")} row).");
        }

        // Powers tween in and the name label resizes its font without a layout signal on the row,
        // so placement is refreshed every frame while the icon shows.
        private void FollowLayout(bool follow)
        {
            if (follow)
            {
                if (_frameTree is null && _row.GetTree() is { } tree)
                {
                    tree.ProcessFrame += OnProcessFrame;
                    _frameTree = tree;
                }

                return;
            }

            if (_frameTree is not null && GodotObject.IsInstanceValid(_frameTree))
            {
                _frameTree.ProcessFrame -= OnProcessFrame;
            }

            _frameTree = null;
        }

        // A failure stops the per-frame updates, so it is logged once instead of every frame.
        private void OnProcessFrame()
        {
            try
            {
                UpdateIconPosition();
            }
            catch (Exception exception)
            {
                FollowLayout(false);
                Log.Warn($"[{ModEntry.ModId}] Icon placement failed; the icon keeps its last position. {exception}");
            }
        }

        private void UpdateIconPosition()
        {
            var icon = _icon;
            if (icon is null
                || !GodotObject.IsInstanceValid(icon)
                || !GodotObject.IsInstanceValid(_row)
                || !GodotObject.IsInstanceValid(_topContainer))
            {
                return;
            }

            // Right after the drawn content of the name line and the health bar,
            // centred on the whole row: portrait, name line, and health bar.
            var topRect = _topContainer.GetGlobalRect();
            var right = ContentRight(_topContainer) ?? topRect.End.X;
            var top = topRect.Position.Y;
            var bottom = topRect.End.Y;
            foreach (var part in _layoutParts)
            {
                if (part is null || !GodotObject.IsInstanceValid(part) || !part.IsVisibleInTree())
                {
                    continue;
                }

                var rect = part.GetGlobalRect();
                top = Mathf.Min(top, rect.Position.Y);
                bottom = Mathf.Max(bottom, rect.End.Y);
                if (ReferenceEquals(part, _hpBar))
                {
                    right = Mathf.Max(right, rect.End.X);
                }
            }

            var rowTransform = _row.GetGlobalTransform().AffineInverse();
            var topRight = rowTransform * new Vector2(right, top);
            var bottomLocal = (rowTransform * new Vector2(right, bottom)).Y;
            icon.Position = new Vector2(topRight.X + Gap, (topRight.Y + bottomLocal - icon.Size.Y) * 0.5f);
        }

        // Global X of the rightmost drawn content under a control. The name label and the power
        // container keep fixed widths, so their rects reach past the text and the power icons.
        private static float? ContentRight(Control control)
        {
            if (!control.Visible)
            {
                return null;
            }

            if (control is Label label)
            {
                return LabelTextRight(label);
            }

            float? right = null;
            var hasChildControls = false;
            foreach (var child in control.GetChildren())
            {
                if (child is not Control childControl)
                {
                    continue;
                }

                hasChildControls = true;
                if (ContentRight(childControl) is { } childRight)
                {
                    right = right is { } current ? Mathf.Max(current, childRight) : childRight;
                }
            }

            if (hasChildControls)
            {
                return right;
            }

            var rect = control.GetGlobalRect();
            return rect.Size.X > 0f ? rect.End.X : null;
        }

        private static float? LabelTextRight(Label label)
        {
            if (string.IsNullOrEmpty(label.Text))
            {
                return null;
            }

            var rect = label.GetGlobalRect();
            if (label.HorizontalAlignment != HorizontalAlignment.Left)
            {
                return rect.End.X;
            }

            var font = label.GetThemeFont("font");
            var width = font.GetStringSize(label.Text, HorizontalAlignment.Left, -1f, label.GetThemeFontSize("font_size")).X;
            return Mathf.Min(rect.End.X, (label.GetGlobalTransform() * new Vector2(width, 0f)).X);
        }
    }
}
