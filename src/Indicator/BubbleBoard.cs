namespace WaitIHaveVuln.Indicator;

/// <summary>The bubbles on screen now, with the time each one goes away, so no two show the same line.</summary>
internal sealed class BubbleBoard
{
    private readonly List<(BubbleLine Line, double Until)> _shown = new();

    internal void Add(BubbleLine line, double until)
    {
        _shown.Add((line, until));
    }

    /// <summary>A bubble taken down before its time.</summary>
    internal void Remove(BubbleLine line)
    {
        var index = _shown.FindIndex(entry => entry.Line == line);
        if (index >= 0)
        {
            _shown.RemoveAt(index);
        }
    }

    internal bool Contains(BubbleLine line, double now)
    {
        _shown.RemoveAll(entry => entry.Until <= now);
        return _shown.Exists(entry => entry.Line == line);
    }
}
