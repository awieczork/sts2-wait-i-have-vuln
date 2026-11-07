namespace WaitIHaveVuln.HandPreview;

/// <summary>
/// Engine-independent rules for the teammate hand preview. The row's own hitbox lies over the card-count
/// icon and reports mouse hover and controller focus alike, so the preview tests the mouse position
/// against the icon itself and stays closed while a controller is in use. The hitbox's hover still
/// counts, because the GUI gives it no hover while another screen covers the row. The preview also stays
/// closed while the game suppresses the row's own highlight (targeting, a card play) or blocks every hover tip.
/// </summary>
internal static class HandPreviewRule
{
    /// <summary>Whether a row's preview watches the mouse this combat: with the setting on, and only on a teammate's row, because your own row shows no card count.</summary>
    internal static bool Watches(bool setting, bool isOwnRow)
    {
        return setting && !isOwnRow;
    }

    internal static bool IsOpen(
        bool counterShown,
        bool usingController,
        bool rowHovered,
        bool mouseOverCounter,
        bool targeting,
        bool inCardPlay,
        bool hoverTipsBlocked)
    {
        return counterShown && !usingController && rowHovered && mouseOverCounter && !targeting && !inCardPlay && !hoverTipsBlocked;
    }
}
