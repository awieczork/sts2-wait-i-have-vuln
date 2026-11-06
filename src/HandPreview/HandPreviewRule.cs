namespace WaitIHaveVuln.HandPreview;

/// <summary>
/// Engine-independent rule for the teammate hand preview. The row's own hitbox lies over the card-count
/// icon and reports mouse hover and controller focus alike, so the preview tests the mouse position
/// against the icon itself and stays closed while a controller is in use. It also stays closed while the
/// game suppresses the row's own highlight (targeting, a card play) or blocks every hover tip.
/// </summary>
internal static class HandPreviewRule
{
    internal static bool IsOpen(
        bool enabled,
        bool counterShown,
        bool usingController,
        bool mouseOverCounter,
        bool targeting,
        bool inCardPlay,
        bool hoverTipsBlocked)
    {
        return enabled && counterShown && !usingController && mouseOverCounter && !targeting && !inCardPlay && !hoverTipsBlocked;
    }
}
