using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace WaitIHaveVuln.Settings;

/// <summary>
/// Gives a plain HSlider the look of the game's settings sliders, copied from a live NSlider:
/// the settings screen with the sound sliders stays loaded under the modding screen.
/// Only theme overrides and script-free child nodes are copied, so no game code runs on the copy.
/// NSlider draws its own handle node and moves it in code, so the copied handle is moved here the same way.
/// </summary>
internal static class GameSliderLook
{
    private static readonly string[] StyleNames = { "slider", "grabber_area", "grabber_area_highlight" };
    private static readonly string[] IconNames = { "grabber", "grabber_highlight", "grabber_disabled" };
    private static bool _outcomeLogged;

    /// <summary>
    /// Returns false, leaving the slider unchanged, when no game slider is loaded. <paramref name="overhang"/>
    /// is how far the copied handle reaches past each end of the slider: the game centres its handle on
    /// the track's ends, so half of it sticks out at the minimum and the maximum.
    /// </summary>
    internal static bool TryApply(Node onScreen, HSlider slider, out float overhang)
    {
        overhang = 0f;
        if (onScreen.GetTree()?.Root is not { } root || NodeSearch.FindDescendant<NSlider>(root) is not { } game)
        {
            LogOnce("the game's slider is not loaded; using the mod's own slider look");
            return false;
        }

        var parts = 0;
        foreach (var name in StyleNames)
        {
            if (game.HasThemeStyleboxOverride(name))
            {
                slider.AddThemeStyleboxOverride(name, game.GetThemeStylebox(name));
                parts++;
            }
        }

        foreach (var name in IconNames)
        {
            if (game.HasThemeIconOverride(name))
            {
                slider.AddThemeIconOverride(name, game.GetThemeIcon(name));
                parts++;
            }
        }

        var handle = AccessTools.Field(typeof(NSlider), "_handle")?.GetValue(game) as Control;
        Control? handleCopy = null;
        foreach (var child in game.GetChildren())
        {
            if (child is not Control control || !NodeSearch.IsScriptFree(control))
            {
                continue;
            }

            var copy = (Control)control.Duplicate();
            IgnoreMouse(copy);
            slider.AddChild(copy);
            parts++;
            if (ReferenceEquals(control, handle))
            {
                handleCopy = copy;
            }
        }

        if (parts == 0)
        {
            LogOnce("the game's slider has no copyable parts; using the mod's own slider look");
            return false;
        }

        slider.CustomMinimumSize = new Vector2(slider.CustomMinimumSize.X, Mathf.Max(game.Size.Y, handleCopy?.Size.Y ?? 0f));
        if (handleCopy is not null)
        {
            // The copied handle replaces the built-in grabber, as on the game's slider. The slider
            // redraws on every value and size change, including SetValueNoSignal, so placing the
            // handle on draw keeps it in step.
            var none = EmptyIcon();
            slider.AddThemeIconOverride("grabber", none);
            slider.AddThemeIconOverride("grabber_highlight", none);
            Action place = () => PlaceHandle(slider, handleCopy);
            slider.Draw += () => HookGuard.Run("Settings slider handle", "the handle keeps its last place", place);
            overhang = handleCopy.Size.X * 0.5f;
        }

        LogOnce($"copied the game's slider look ({parts} parts{(handleCopy is null ? ", no handle" : string.Empty)})");
        return true;
    }

    // Same placement as NSlider.UpdateHandlePosition, with the range starting above zero.
    private static void PlaceHandle(HSlider slider, Control handle)
    {
        if (!GodotObject.IsInstanceValid(handle))
        {
            return;
        }

        var span = slider.MaxValue - slider.MinValue;
        var ratio = span > 0 ? (float)((slider.Value - slider.MinValue) / span) : 0f;
        handle.Position = new Vector2(slider.Size.X * ratio - handle.Size.X * 0.5f, (slider.Size.Y - handle.Size.Y) * 0.5f);
    }

    // Clicks must reach the slider underneath, or dragging on the handle would do nothing.
    private static void IgnoreMouse(Control control)
    {
        control.MouseFilter = Control.MouseFilterEnum.Ignore;
        control.FocusMode = Control.FocusModeEnum.None;
        foreach (var child in control.GetChildren())
        {
            if (child is Control nested)
            {
                IgnoreMouse(nested);
            }
        }
    }

    private static ImageTexture EmptyIcon()
    {
        return ImageTexture.CreateFromImage(Image.CreateEmpty(1, 1, false, Image.Format.Rgba8));
    }

    private static void LogOnce(string outcome)
    {
        if (_outcomeLogged)
        {
            return;
        }

        _outcomeLogged = true;
        Log.Info($"[{ModEntry.ModId}] Settings slider: {outcome}.");
    }
}
