using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;
using WaitIHaveVuln.Settings;

namespace WaitIHaveVuln.Indicator;

internal static class MultiplayerPlayerStateIndicators
{
    private static readonly ConditionalWeakTable<NMultiplayerPlayerState, RowIndicator> Indicators = new();

    internal static void OnCombatSetUp(NMultiplayerPlayerState row, HBoxContainer topContainer, NHealthBar healthBar, TextureRect portrait)
    {
        Guard(nameof(OnCombatSetUp), () =>
        {
            var indicator = Indicators.GetValue(row, state => new RowIndicator(state, topContainer, healthBar, portrait));
            indicator.BeginCombat(!IsConnected(row));
        });
    }

    internal static void OnConnectionStateChanged(NMultiplayerPlayerState row)
    {
        Guard(nameof(OnConnectionStateChanged), () =>
        {
            if (Indicators.TryGetValue(row, out var indicator))
            {
                indicator.UpdateConnectionState(!IsConnected(row));
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
        });
    }

    internal static void OnExitTree(NMultiplayerPlayerState row)
    {
        Guard(nameof(OnExitTree), () =>
        {
            if (!Indicators.TryGetValue(row, out var indicator))
            {
                return;
            }

            indicator.Dispose();
            Indicators.Remove(row);
        });
    }

    private static void Guard(string hook, Action action)
    {
        HookGuard.Run(hook, "the indicator keeps its current state", action);
    }

    private static bool IsConnected(NMultiplayerPlayerState row)
    {
        var lobby = RunManager.Instance.RunLobby;
        // Single-player runs have no lobby; only solo mode creates a row there, and it counts as connected.
        if (lobby is null)
        {
            return SoloMode.Enabled;
        }

        return lobby.ConnectedPlayerIds.Contains(row.Player.NetId);
    }

    private sealed class CardPileHand : ICardHand<CardModel>
    {
        private readonly CardPile _pile;

        internal CardPileHand(CardPile pile)
        {
            _pile = pile;
        }

        public IReadOnlyList<CardModel> Cards => _pile.Cards;

        public event Action<CardModel> CardAdded
        {
            add => _pile.CardAdded += value;
            remove => _pile.CardAdded -= value;
        }

        public event Action<CardModel> CardRemoved
        {
            add => _pile.CardRemoved += value;
            remove => _pile.CardRemoved -= value;
        }
    }

    private sealed class RowIndicator : IDisposable
    {
        private const float IconSize = 36f;
        private const float Gap = 12f;
        private const string VulnerableIconPath = "res://images/atlases/power_atlas.sprites/vulnerable_power.tres";
        private const string SpeechText = "Wait! I have vuln!";
        private const double SpeechSeconds = 4.0;
        private static bool _textureLoadAttempted;
        private static Texture2D? _vulnerableTexture;
        private readonly NMultiplayerPlayerState _row;
        private readonly HBoxContainer _topContainer;
        private readonly Control? _hpBar;
        private readonly Control _portrait;
        private readonly HandIndicatorState<CardModel> _state;
        private TextureRect? _icon;
        private SceneTree? _frameTree;
        private bool _combatActive;

        internal RowIndicator(NMultiplayerPlayerState row, HBoxContainer topContainer, NHealthBar healthBar, TextureRect portrait)
        {
            _row = row;
            _topContainer = topContainer;
            // The visible bar, not the whole health bar node, which also holds the block badge.
            _hpBar = healthBar.HpBarContainer;
            _portrait = portrait;
            _state = new HandIndicatorState<CardModel>(card => card.Id.Entry, OnHandChanged);
        }

        internal void BeginCombat(bool disconnected)
        {
            _combatActive = true;
            UpdateConnectionState(disconnected);
        }

        internal void UpdateConnectionState(bool disconnected)
        {
            if (!_combatActive)
            {
                return;
            }

            if (disconnected)
            {
                _state.Suspend();
                return;
            }

            var hand = _row.Player.PlayerCombatState?.Hand;
            if (hand is null)
            {
                _state.Suspend();
                return;
            }

            _state.Bind(new CardPileHand(hand));
        }

        internal void EndCombat()
        {
            _combatActive = false;
            _state.EndCombat();
        }

        public void Dispose()
        {
            _combatActive = false;
            _state.Dispose();
            FollowLayout(false);
        }

        // The settings are read here and never change while rows exist, because the game
        // greys out Mod Settings during a run.
        private void OnHandChanged(bool hasCard)
        {
            var settings = SettingsService.Current;
            var visible = settings.ShowsIndicator(hasCard, LocalContext.IsMe(_row.Player));
            if (SetVisible(visible) && settings.ShowBubble)
            {
                ShowSpeechBubble();
            }
        }

        // Returns true when the icon was just created.
        private bool SetVisible(bool visible)
        {
            if (visible)
            {
                if (_icon is not null)
                {
                    return false;
                }

                // The icon is the primary signal; without it the bubble would announce nothing.
                var texture = LoadVulnerableTexture();
                if (texture is null)
                {
                    return false;
                }

                // ExpandMode comes before any size: under the default KeepSize, Godot clamps Size up to
                // the texture's own size, and switching to IgnoreSize afterwards does not shrink it back.
                var size = IconSize * SettingsService.Current.IconScale;
                var icon = new TextureRect
                {
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
                return true;
            }

            if (_icon is null)
            {
                return false;
            }

            FollowLayout(false);
            if (GodotObject.IsInstanceValid(_icon))
            {
                _icon.Visible = false;
                _icon.QueueFree();
            }

            _icon = null;
            return false;
        }

        private void ShowSpeechBubble()
        {
            var creature = _row.Player.Creature;
            if (creature is null || creature.IsDead)
            {
                return;
            }

            var bubble = NSpeechBubbleVfx.Create(SpeechText, creature, SpeechSeconds, VfxColor.White);
            if (bubble is null)
            {
                return;
            }

            creature.GetVfxContainer()?.AddChildSafely(bubble);
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
            foreach (var part in new[] { _hpBar, _portrait })
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

        // Loaded once per process. A failure is logged once and leaves the icon off until restart.
        private static Texture2D? LoadVulnerableTexture()
        {
            if (_textureLoadAttempted)
            {
                return _vulnerableTexture;
            }

            _textureLoadAttempted = true;
            try
            {
                _vulnerableTexture = ResourceLoader.Load<Texture2D>(VulnerableIconPath);
            }
            catch (Exception exception)
            {
                Log.Warn($"[{ModEntry.ModId}] Could not load the Vulnerable icon at '{VulnerableIconPath}': {exception.Message}");
                return null;
            }

            if (_vulnerableTexture is null)
            {
                Log.Warn($"[{ModEntry.ModId}] Could not load the Vulnerable icon at '{VulnerableIconPath}'.");
            }

            return _vulnerableTexture;
        }
    }
}
