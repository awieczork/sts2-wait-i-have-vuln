namespace WaitIHaveVuln.HandPreview;

/// <summary>
/// The cards the preview shows, in order, compared by reference with the live hand every frame. The hand
/// changes silently too (silent adds and removes, reorders raise no event), so the preview compares
/// instead of listening. Holds at most <paramref name="capacity"/> entries in one array and allocates
/// nothing per comparison.
/// </summary>
internal sealed class ShownCards<T>(int capacity) where T : class
{
    private readonly T?[] _cards = new T?[capacity];

    internal int Count { get; private set; }

    internal T? this[int index] => _cards[index];

    /// <summary>Whether <paramref name="hand"/> is the same cards in the same order as the shown ones.</summary>
    internal bool Matches(IReadOnlyList<T> hand)
    {
        if (hand.Count != Count)
        {
            return false;
        }

        for (var i = 0; i < Count; i++)
        {
            if (!ReferenceEquals(hand[i], _cards[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Records <paramref name="hand"/> as shown, up to the capacity.</summary>
    internal void Set(IReadOnlyList<T> hand)
    {
        var count = Math.Min(hand.Count, _cards.Length);
        for (var i = 0; i < count; i++)
        {
            _cards[i] = hand[i];
        }

        Clear(count);
    }

    internal void Clear() => Clear(0);

    private void Clear(int from)
    {
        for (var i = from; i < _cards.Length; i++)
        {
            _cards[i] = null;
        }

        Count = from;
    }
}
