using System;
using System.Collections.Generic;

namespace WaitIHaveVuln.Indicator;

internal static class VulnerableCardPredicate
{
    private static readonly HashSet<string> SupportedModelIds = new(StringComparer.Ordinal)
    {
        "ASSASSINATE",
        "BASH",
        "BEAM_CELL",
        "BREAK",
        "COMET",
        "DOMINATE",
        "EXPOSE",
        "FALLING_STAR",
        "FEAR",
        "GAMMA_BLAST",
        "HIGH_FIVE",
        "KNOW_THY_PLACE",
        "METEOR_SHOWER",
        "PUTREFY",
        "SHOCKWAVE",
        "SQUASH",
        "TAUNT",
        "THUNDERCLAP",
        "TREMBLE",
        "UPPERCUT"
    };

    internal static IReadOnlySet<string> SupportedIds => SupportedModelIds;

    internal static bool IsSupported(string modelId) => SupportedModelIds.Contains(modelId);
}


internal interface ICardHand<TCard>
{
    IReadOnlyList<TCard> Cards { get; }
    event Action<TCard> CardAdded;
    event Action<TCard> CardRemoved;
}

internal sealed class HandIndicatorState<TCard> : IDisposable
{
    private readonly Func<TCard, string> _getModelId;
    private readonly Action<bool> _setVisible;
    private ICardHand<TCard>? _hand;
    private bool _isVisible;
    private bool _disposed;

    internal HandIndicatorState(Func<TCard, string> getModelId, Action<bool> setVisible)
    {
        ArgumentNullException.ThrowIfNull(getModelId);
        ArgumentNullException.ThrowIfNull(setVisible);
        _getModelId = getModelId;
        _setVisible = setVisible;
    }

    internal bool IsVisible => _isVisible;

    internal void Bind(ICardHand<TCard> hand)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(hand);

        if (ReferenceEquals(_hand, hand))
        {
            Refresh();
            return;
        }

        Unsubscribe();
        _hand = hand;
        hand.CardAdded += OnCardChanged;
        hand.CardRemoved += OnCardChanged;
        Refresh();
    }

    internal void Suspend()
    {
        Unsubscribe();
    }

    internal void EndCombat()
    {
        Unsubscribe();
        SetVisible(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        EndCombat();
    }

    private void OnCardChanged(TCard _)
    {
        Refresh();
    }

    private void Refresh()
    {
        var hand = _hand ?? throw new InvalidOperationException("Cannot refresh an unbound hand indicator.");
        foreach (var card in hand.Cards)
        {
            if (VulnerableCardPredicate.IsSupported(_getModelId(card)))
            {
                SetVisible(true);
                return;
            }
        }

        SetVisible(false);
    }

    private void Unsubscribe()
    {
        if (_hand is null)
        {
            return;
        }

        _hand.CardAdded -= OnCardChanged;
        _hand.CardRemoved -= OnCardChanged;
        _hand = null;
    }

    private void SetVisible(bool visible)
    {
        if (_isVisible == visible)
        {
            return;
        }

        _setVisible(visible);
        _isVisible = visible;
    }
}
