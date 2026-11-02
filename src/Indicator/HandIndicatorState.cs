namespace WaitIHaveVuln.Indicator;

/// <summary>
/// Engine-independent indicator state for one player's row: follows a bound hand and reports
/// visibility changes only when the first qualifying card arrives or the last one leaves.
/// Suspending keeps the last observed result until a hand is bound again.
/// </summary>
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
