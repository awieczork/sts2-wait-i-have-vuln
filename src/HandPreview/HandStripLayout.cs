namespace WaitIHaveVuln.HandPreview;

/// <summary>
/// Engine-independent layout of the preview strip: one line of cards, left to right in hand order, with
/// its top-left corner at (<c>left</c>, <c>top</c>) and every card inside <c>bounds</c>. Cards keep
/// <see cref="BaseScale"/> while they fit; a larger hand shrinks them down to <see cref="MinScale"/>, and
/// past that the cards overlap so the strip still ends inside the bounds. A strip that would reach below
/// the bounds moves up, but never above their top; one that starts too far right for a single card at
/// <see cref="MinScale"/> moves left until that card fits.
/// </summary>
internal static class HandStripLayout
{
    // At this scale, the owner's choice after the first in-game check, a full hand of 10 cards (the
    // game's limit, 300 px wide each) fits between the row's content and the right edge of the
    // 1920-wide canvas; with the Vulnerable icon showing it shrinks a little.
    internal const float BaseScale = 0.5f;
    internal const float MinScale = 0.3f;
    internal const float Gap = 8f;

    internal static HandStrip Place(int count, float left, float top, Box bounds, float cardWidth, float cardHeight)
    {
        left = Math.Min(left, bounds.Right - cardWidth * MinScale);
        var available = bounds.Right - left;
        var scale = BaseScale;
        if (count > 0)
        {
            var fit = (available - (count - 1) * Gap) / (count * cardWidth);
            scale = Math.Clamp(fit, MinScale, BaseScale);
        }

        var width = cardWidth * scale;
        var height = cardHeight * scale;
        var step = width + Gap;
        if (count > 1 && (count - 1) * step + width > available)
        {
            step = Math.Max(0f, (available - width) / (count - 1));
        }

        var y = Math.Max(Math.Min(top, bounds.Bottom - height), bounds.Y);
        return new HandStrip(scale, left + width / 2f, y + height / 2f, step);
    }
}
