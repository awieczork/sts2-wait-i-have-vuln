using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Runs;

namespace WaitIHaveVuln.Indicator;

internal static class MultiplayerPlayerStateIndicators
{
    private static readonly ConditionalWeakTable<NMultiplayerPlayerState, RowIndicator> Indicators = new();

    internal static void OnCombatSetUp(NMultiplayerPlayerState row, HBoxContainer topContainer)
    {
        var indicator = Indicators.GetValue(row, state => new RowIndicator(state, topContainer));
        indicator.BeginCombat(!IsConnected(row));
    }

    internal static void OnConnectionStateChanged(NMultiplayerPlayerState row)
    {
        if (Indicators.TryGetValue(row, out var indicator))
        {
            indicator.UpdateConnectionState(!IsConnected(row));
        }
    }

    private static bool IsConnected(NMultiplayerPlayerState row)
    {
        var lobby = RunManager.Instance.RunLobby;
        return lobby is not null && lobby.ConnectedPlayerIds.Contains(row.Player.NetId);
    }

    internal static void OnCombatEnded(NMultiplayerPlayerState row)
    {
        if (Indicators.TryGetValue(row, out var indicator))
        {
            indicator.EndCombat();
        }
    }

    internal static void OnExitTree(NMultiplayerPlayerState row)
    {
        if (!Indicators.TryGetValue(row, out var indicator))
        {
            return;
        }

        indicator.Dispose();
        Indicators.Remove(row);
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
        private const float IconSize = 24f;
        private const float Gap = 6f;
        private const string VulnerableIconPath = "res://images/atlases/power_atlas.sprites/vulnerable_power.tres";
        private static bool _textureLoadAttempted;
        private static Texture2D? _vulnerableTexture;
        private readonly NMultiplayerPlayerState _row;
        private readonly HBoxContainer _topContainer;
        private readonly Viewport _viewport;
        private readonly List<CanvasItem> _layoutItems = new();
        private readonly HandIndicatorState<CardModel> _state;
        private TextureRect? _icon;
        private bool _combatActive;

        internal RowIndicator(NMultiplayerPlayerState row, HBoxContainer topContainer)
        {
            _row = row;
            _topContainer = topContainer;
            _viewport = row.GetViewport();
            _state = new HandIndicatorState<CardModel>(card => card.Id.Entry, SetVisible);
            for (Node? ancestor = _topContainer; ancestor is not null; ancestor = ancestor.GetParent())
            {
                if (ancestor is CanvasItem canvasItem)
                {
                    canvasItem.ItemRectChanged += UpdateIconPosition;
                    _layoutItems.Add(canvasItem);
                }

                if (ReferenceEquals(ancestor, _row))
                {
                    break;
                }
            }

            _viewport.SizeChanged += UpdateIconPosition;
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
            foreach (var layoutItem in _layoutItems)
            {
                if (GodotObject.IsInstanceValid(layoutItem))
                {
                    layoutItem.ItemRectChanged -= UpdateIconPosition;
                }
            }

            _layoutItems.Clear();

            if (GodotObject.IsInstanceValid(_viewport))
            {
                _viewport.SizeChanged -= UpdateIconPosition;
            }
        }

        private void SetVisible(bool visible)
        {
            if (visible)
            {
                if (_icon is not null)
                {
                    return;
                }

                var texture = LoadVulnerableTexture();
                if (texture is null)
                {
                    return;
                }

                var icon = new TextureRect
                {
                    Texture = texture,
                    CustomMinimumSize = new Vector2(IconSize, IconSize),
                    Size = new Vector2(IconSize, IconSize),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                _row.AddChild(icon);
                _icon = icon;
                UpdateIconPosition();
                return;
            }

            if (_icon is null)
            {
                return;
            }

            if (GodotObject.IsInstanceValid(_icon))
            {
                _icon.Visible = false;
                _icon.QueueFree();
            }

            _icon = null;
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

            var topRect = _topContainer.GetGlobalRect();
            var rowTransform = _row.GetGlobalTransform().AffineInverse();
            var topRight = rowTransform * topRect.End;
            var topCenter = rowTransform * (topRect.Position + topRect.Size * 0.5f);
            icon.Position = new Vector2(topRight.X + Gap, topCenter.Y - IconSize * 0.5f);
        }

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
