namespace WaitIHaveVuln;

/// <summary>An axis-aligned rectangle in screen units, without Godot, for the card cloud and hand preview placement rules.</summary>
internal readonly record struct Box(float X, float Y, float Width, float Height)
{
    internal float Right => X + Width;

    internal float Bottom => Y + Height;

    internal float CenterX => X + Width / 2f;

    /// <summary>Whether the two overlap; boxes that only touch do not.</summary>
    internal bool Intersects(Box other)
    {
        return X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
    }

    internal bool IsInside(Box outer)
    {
        return X >= outer.X && Y >= outer.Y && Right <= outer.Right && Bottom <= outer.Bottom;
    }

    /// <summary>The box grown by <paramref name="by"/> on every side, or shrunk when it is negative.</summary>
    internal Box Grow(float by)
    {
        return new Box(X - by, Y - by, Width + 2f * by, Height + 2f * by);
    }
}
