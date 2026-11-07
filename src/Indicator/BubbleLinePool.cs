namespace WaitIHaveVuln.Indicator;

/// <summary>
/// Picks bubble lines from one list at random: no line again until the others were used, never the
/// same line twice in a row across a refill, and never a line already on screen. A pool can open
/// each combat with a signature line.
/// </summary>
internal sealed class BubbleLinePool
{
    private readonly IReadOnlyList<string> _lines;
    private readonly string? _signature;
    private readonly Random _random;
    private readonly List<int> _bag = new();
    private int _last = -1;
    // Held weakly, so the pool does not keep a finished combat, and the whole combat state with it, alive
    // until the next combat's first bubble; null for a null combat.
    private WeakReference<object>? _combat;

    internal BubbleLinePool(IReadOnlyList<string> lines, Random random, string? signature = null)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(random);
        _lines = lines;
        _random = random;
        _signature = signature;
    }

    /// <summary>
    /// The next line for <paramref name="combat"/>, or null when every line is on screen or needs a
    /// missing name. The first pick in a new combat is the signature, unless it is on screen.
    /// </summary>
    internal BubbleLine? Next(object? combat, Func<BubbleLine, bool> isOnScreen, string? you, string? attacker = null)
    {
        ArgumentNullException.ThrowIfNull(isOnScreen);
        if (_signature is not null && !IsCurrentCombat(combat))
        {
            _combat = combat is null ? null : new WeakReference<object>(combat);
            var signature = new BubbleLine(_signature);
            if (!isOnScreen(signature))
            {
                // Taken from a full bag, so it counts as this pass's use of the signature.
                if (_bag.Count == 0)
                {
                    Refill();
                }

                _last = IndexOf(_signature);
                _bag.Remove(_last);
                return signature;
            }
        }

        if (Take(isOnScreen, you, attacker) is { } line)
        {
            return line;
        }

        Refill();
        return Take(isOnScreen, you, attacker);
    }

    private bool IsCurrentCombat(object? combat)
    {
        return _combat is null ? combat is null : _combat.TryGetTarget(out var current) && Equals(current, combat);
    }

    private BubbleLine? Take(Func<BubbleLine, bool> isOnScreen, string? you, string? attacker)
    {
        foreach (var index in _bag)
        {
            if (index == _last || BubbleLines.Fill(_lines[index], you, attacker) is not { } text || isOnScreen(new BubbleLine(text)))
            {
                continue;
            }

            _bag.Remove(index);
            _last = index;
            return new BubbleLine(text);
        }

        return null;
    }

    private void Refill()
    {
        _bag.Clear();
        for (var index = 0; index < _lines.Count; index++)
        {
            _bag.Insert(_random.Next(_bag.Count + 1), index);
        }
    }

    private int IndexOf(string line)
    {
        for (var index = 0; index < _lines.Count; index++)
        {
            if (string.Equals(_lines[index], line, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
