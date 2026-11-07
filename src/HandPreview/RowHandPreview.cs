using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using WaitIHaveVuln.Settings;

namespace WaitIHaveVuln.HandPreview;

/// <summary>
/// The hand preview of one teammate's row: while the mouse is on the row's card-count icon, that
/// teammate's hand shows as small real cards in a line to the right of the row. It only reads the
/// hand; the card nodes come from the game's own card pool and go back to it when the preview closes.
/// </summary>
internal sealed class RowHandPreview
{
    // Space between the row's drawn content and the first card, clear of the selection frame the game
    // draws 6 units past that content.
    private const float RowGap = 16f;
    // Space kept free at the edges of the screen.
    private const float ScreenMargin = 16f;

    private readonly NMultiplayerPlayerState _row;
    private readonly Control _counter;
    private readonly HBoxContainer _topLine;
    private readonly NHealthBar _healthBar;
    private readonly List<NCard> _cards = new();
    private readonly ShownCards<CardModel> _shown = new(CardPile.MaxCardsInHand);
    private readonly Action<CombatState> _onCombatStateChanged;
    private readonly Dictionary<ulong, bool> _modNodes = new();
    private SceneTree? _frameTree;
    private CombatStateTracker? _tracker;
    private Control? _holder;
    private bool _visualsDue;
    // Where the row's content ended when the strip was last laid out.
    private float _laidOutRight;

    internal RowHandPreview(NMultiplayerPlayerState row, Control counter, HBoxContainer topLine, NHealthBar healthBar)
    {
        _row = row;
        _counter = counter;
        _topLine = topLine;
        _healthBar = healthBar;
        _onCombatStateChanged = _ => _visualsDue = true;
    }

    /// <summary>Reads the setting and starts watching the mouse for this combat; your own row has no counter.</summary>
    internal void BeginCombat()
    {
        Stop();
        if (!HandPreviewRule.Watches(SettingsService.Current.ShowHandPreview, LocalContext.IsMe(_row.Player)))
        {
            return;
        }

        if (_row.GetTree() is { } tree)
        {
            tree.ProcessFrame += OnProcessFrame;
            _frameTree = tree;
        }
    }

    /// <summary>Closes the preview, returns its cards to the game's pool, and stops watching, at combat end or when the row leaves.</summary>
    internal void Stop()
    {
        if (_frameTree is not null && GodotObject.IsInstanceValid(_frameTree))
        {
            _frameTree.ProcessFrame -= OnProcessFrame;
        }

        _frameTree = null;
        _modNodes.Clear();
        Close();
        if (_holder is not null && GodotObject.IsInstanceValid(_holder))
        {
            _holder.QueueFreeSafely();
        }

        _holder = null;
    }

    // A failure stops the per-frame updates, so it is logged once instead of every frame.
    private void OnProcessFrame()
    {
        try
        {
            Update();
        }
        catch (Exception exception)
        {
            Stop();
            Log.Warn($"[{ModEntry.ModId}] Hand preview failed; it stays off for this row until the next combat. {exception}");
        }
    }

    private void Update()
    {
        if (!GodotObject.IsInstanceValid(_row) || !GodotObject.IsInstanceValid(_counter))
        {
            Stop();
            return;
        }

        var hand = _row.Player.PlayerCombatState?.Hand.Cards;
        var open = hand is not null && HandPreviewRule.IsOpen(
            _counter.IsVisibleInTree(),
            NControllerManager.Instance?.IsUsingController ?? false,
            IsRowHovered(),
            _counter.GetGlobalRect().HasPoint(_counter.GetGlobalMousePosition()),
            // NTargetManager.Instance is reached through NRun.Instance and throws without a run.
            NRun.Instance is not null && NTargetManager.Instance.IsInSelection,
            NPlayerHand.Instance?.InCardPlay ?? false,
            NHoverTipSet.shouldBlockHoverTips);
        if (!open)
        {
            Close();
            return;
        }

        Open();
        if (!_shown.Matches(hand!))
        {
            Rebuild(hand!);
        }
        else if (_holder is { } holder && ContentRight(holder) != _laidOutRight)
        {
            // The row's content changed while the hand did not, such as the Vulnerable icon appearing.
            Layout(holder);
        }

        if (_visualsDue)
        {
            RefreshVisuals();
        }
    }

    // Godot's own hovered control: the card counter stops the mouse, so it is the row's, while a screen
    // drawn over the player list (pile view, map, pause menu) takes the hover and keeps the preview closed.
    // The row hitbox's hover is not used, because it starts narrower than a row with a long name.
    private bool IsRowHovered() =>
        _row.GetViewport()?.GuiGetHoveredControl() is { } hovered && (hovered == _row || _row.IsAncestorOf(hovered));

    private void Open()
    {
        if (_holder is null || !GodotObject.IsInstanceValid(_holder))
        {
            // A plain Control, not a container, so nothing re-lays the cards out; a child of the row,
            // so the strip moves with the row when the game slides the player list away.
            _holder = new Control { Name = $"{ModEntry.ModId}HandPreview", MouseFilter = Control.MouseFilterEnum.Ignore };
            _row.AddChildSafely(_holder);
        }

        if (_tracker is null)
        {
            _tracker = CombatManager.Instance.StateTracker;
            _tracker.CombatStateChanged += _onCombatStateChanged;
        }
    }

    private void Close()
    {
        if (_tracker is not null)
        {
            _tracker.CombatStateChanged -= _onCombatStateChanged;
            _tracker = null;
        }

        foreach (var card in _cards)
        {
            card.QueueFreeSafely();
        }

        _cards.Clear();
        _shown.Clear();
        _visualsDue = false;
    }

    // Keeps the card nodes already shown and changes only their model, so a draw or a play does not
    // return and take back every node.
    private void Rebuild(IReadOnlyList<CardModel> hand)
    {
        var holder = _holder!;
        var count = Math.Min(hand.Count, CardPile.MaxCardsInHand);
        for (var i = 0; i < count; i++)
        {
            if (i < _cards.Count)
            {
                _cards[i].Model = hand[i];
                continue;
            }

            // Null only in the game's test mode, which a player never runs.
            if (NCard.Create(hand[i]) is not { } card)
            {
                break;
            }

            holder.AddChildSafely(card);
            _cards.Add(card);
        }

        for (var i = _cards.Count - 1; i >= count; i--)
        {
            _cards[i].QueueFreeSafely();
            _cards.RemoveAt(i);
        }

        _shown.Set(hand);
        Layout(holder);
        _visualsDue = true;
    }

    private void Layout(Control holder)
    {
        var row = _row.GetGlobalRect();
        var screen = _row.GetViewport().GetVisibleRect();
        var bounds = new Box(
            screen.Position.X + ScreenMargin,
            screen.Position.Y + ScreenMargin,
            screen.Size.X - 2f * ScreenMargin,
            screen.Size.Y - 2f * ScreenMargin);
        _laidOutRight = ContentRight(holder);
        var strip = HandStripLayout.Place(_cards.Count, _laidOutRight + RowGap, row.Position.Y, bounds, NCard.defaultSize.X, NCard.defaultSize.Y);
        var toHolder = holder.GetGlobalTransform().AffineInverse();
        for (var i = 0; i < _cards.Count; i++)
        {
            _cards[i].Position = toHolder * new Vector2(strip.CenterX(i), strip.CenterY);
            _cards[i].Scale = new Vector2(strip.Scale, strip.Scale);
        }
    }

    // Global X where the row's drawn content ends, measured as the game sizes the row's selection frame
    // (NMultiplayerPlayerState.UpdateSelectionReticleWidth): the last visible item of the name line and
    // the health bar. The row's own rect is the whole player list's width, far past the content. Nodes
    // the mod adds to the row, such as the Vulnerable icon, carry the mod id in their name and count too.
    // Runs every frame while the preview is open, so it walks the children by index, which allocates no
    // Godot array.
    private float ContentRight(Control holder)
    {
        var right = _row.GlobalPosition.X;
        if (GodotObject.IsInstanceValid(_topLine))
        {
            var count = _topLine.GetChildCount();
            for (var i = 0; i < count; i++)
            {
                if (_topLine.GetChild(i) is Control { Visible: true } item && item.Size.X > 0f)
                {
                    right = Mathf.Max(right, item.GetGlobalRect().End.X);
                }
            }
        }

        if (GodotObject.IsInstanceValid(_healthBar) && _healthBar.HpBarContainer is { } bar)
        {
            right = Mathf.Max(right, bar.GetGlobalRect().End.X);
        }

        var rowChildren = _row.GetChildCount();
        for (var i = 0; i < rowChildren; i++)
        {
            if (_row.GetChild(i) is Control { Visible: true } added && !ReferenceEquals(added, holder) && IsModNode(added))
            {
                right = Mathf.Max(right, added.GetGlobalRect().End.X);
            }
        }

        return right;
    }

    // Remembered per node, because reading a node's name makes a new string each time.
    private bool IsModNode(Node node)
    {
        var id = node.GetInstanceId();
        if (!_modNodes.TryGetValue(id, out var isModNode))
        {
            isModNode = node.Name.ToString().StartsWith(ModEntry.ModId, StringComparison.Ordinal);
            _modNodes[id] = isModNode;
        }

        return isModNode;
    }

    // A card's text follows its owner's powers and energy, so it is redrawn after each combat state
    // change; a card whose node is not ready yet is redrawn on a later frame.
    private void RefreshVisuals()
    {
        var allReady = true;
        foreach (var card in _cards)
        {
            if (!GodotObject.IsInstanceValid(card) || card.Model is null)
            {
                continue;
            }

            if (!card.IsNodeReady())
            {
                allReady = false;
                continue;
            }

            card.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
        }

        _visualsDue = !allReady;
    }
}
