using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;

namespace WaitIHaveVuln.Settings;

/// <summary>
/// The settings controls on the modding screen, in the mod image slot between the title and the
/// description, which this mod leaves empty. Built from plain Godot controls rather than the game's
/// settings scenes, so a game UI change cannot break them; the font, colours, and tick art are read
/// from the screen itself, so the panel matches it.
/// </summary>
internal static class SettingsPanel
{
    // The game's own gold, for the headings and the hover text.
    private static readonly Color Gold = StsColors.gold;
    private static readonly ConditionalWeakTable<NModInfoContainer, Controls> Panels = new();

    internal static void OnModInfoFilled(NModInfoContainer container, Mod mod, TextureRect image, RichTextLabel description)
    {
        HookGuard.Run(nameof(OnModInfoFilled), "the mod details show without the settings", () =>
        {
            var isThisMod = mod.manifest?.id == ModEntry.ModId;
            if (!Panels.TryGetValue(container, out var controls) || !GodotObject.IsInstanceValid(controls.Root))
            {
                if (!isThisMod)
                {
                    return;
                }

                controls = new Controls(Style.From(container, description), container);
                container.AddChild(controls.Root);
                Panels.AddOrUpdate(container, controls);
            }

            controls.Root.Visible = isThisMod;
            if (isThisMod)
            {
                // The container places its children by anchors, not as a layout container,
                // so the panel takes the image slot's anchors and follows it on resize.
                controls.Root.AnchorLeft = image.AnchorLeft;
                controls.Root.AnchorTop = image.AnchorTop;
                controls.Root.AnchorRight = image.AnchorRight;
                controls.Root.AnchorBottom = image.AnchorBottom;
                controls.Root.OffsetLeft = image.OffsetLeft;
                controls.Root.OffsetTop = image.OffsetTop;
                controls.Root.OffsetRight = image.OffsetRight;
                controls.Root.OffsetBottom = image.OffsetBottom;
                controls.Sync();
                // After layout, when the sizes it measures are current.
                Callable.From(() => HookGuard.Run("Settings fit", "the settings keep their size", () => controls.FitTo(image))).CallDeferred();
            }
        });
    }

    // Read once per panel from the description label and the mod list's tickbox. A missing
    // part keeps Godot's default for it, so a game UI change degrades the look, not the panel.
    private sealed record Style(Font? Font, int FontSize, Color TextColor, Texture2D? Ticked, Texture2D? NotTicked)
    {
        internal static Style From(NModInfoContainer container, RichTextLabel description)
        {
            var (ticked, notTicked) = FindTickTextures(container);
            return new Style(
                description.GetThemeFont("normal_font"),
                description.GetThemeFontSize("normal_font_size"),
                description.GetThemeColor("default_color"),
                ticked,
                notTicked);
        }

        private static (Texture2D?, Texture2D?) FindTickTextures(Node container)
        {
            Node? screen = container;
            while (screen is not null and not NModdingScreen)
            {
                screen = screen.GetParent();
            }

            if (screen is null || NodeSearch.FindDescendant<NTickbox>(screen) is not { } tickbox)
            {
                return (null, null);
            }

            return (TextureOf(tickbox, "_tickedImage"), TextureOf(tickbox, "_notTickedImage"));
        }

        // Looked up per call, not cached in a static field, so a renamed field gives null inside the guard.
        private static Texture2D? TextureOf(NTickbox tickbox, string field)
        {
            return AccessTools.Field(typeof(NTickbox), field)?.GetValue(tickbox) switch
            {
                TextureRect rect => rect.Texture,
                Control control => NodeSearch.FindDescendant<TextureRect>(control)?.Texture,
                _ => null
            };
        }
    }

    private sealed class Controls
    {
        private readonly Style _style;
        private readonly BaseButton _showBubble;
        private readonly BaseButton _variedBubbleLines;
        private readonly BaseButton _showOnOwnRow;
        private readonly HSlider _iconScale;
        private readonly Label _iconScaleValue;
        private readonly BaseButton _showHandReminder;
        private readonly BaseButton _showHandPreview;

        internal Controls(Style style, Node onScreen)
        {
            _style = style;
            Root = new VBoxContainer { Name = $"{ModEntry.ModId}Settings", Alignment = BoxContainer.AlignmentMode.Center };
            Root.AddThemeConstantOverride("separation", style.FontSize / 3);

            _showBubble = CreateToggle("Show bubbles");
            _showBubble.Toggled += on => Change(nameof(_showBubble), settings => settings with { ShowBubble = on });

            _variedBubbleLines = CreateToggle("Varied bubble lines");
            _variedBubbleLines.Toggled += on => Change(nameof(_variedBubbleLines), settings => settings with { VariedBubbleLines = on });

            _showOnOwnRow = CreateToggle("Show icon on my own row");
            _showOnOwnRow.Toggled += on => Change(nameof(_showOnOwnRow), settings => settings with { ShowOnOwnRow = on });

            _iconScaleValue = CreateLabel(string.Empty, style.TextColor);
            _iconScaleValue.CustomMinimumSize = new Vector2(style.FontSize * 3f, 0f);
            _iconScaleValue.HorizontalAlignment = HorizontalAlignment.Right;
            _iconScale = CreateSlider(onScreen, out var overhang);
            // A drag saves once when it ends; a keyboard step or a click outside a drag saves at once.
            var dragging = false;
            const string scaleFailure = "the icon size may keep its current value";
            _iconScale.DragStarted += () => HookGuard.Run(nameof(_iconScale), scaleFailure, () => dragging = true);
            _iconScale.DragEnded += _ => HookGuard.Run(nameof(_iconScale), scaleFailure, () =>
            {
                dragging = false;
                SaveScale();
            });
            _iconScale.ValueChanged += value => HookGuard.Run(nameof(_iconScale), scaleFailure, () =>
            {
                ShowScale((int)Math.Round(value));
                if (!dragging)
                {
                    SaveScale();
                }
            });
            var iconScaleRow = new HBoxContainer();
            iconScaleRow.AddThemeConstantOverride("separation", style.FontSize / 2);
            iconScaleRow.AddChild(CreateLabel("Icon size", style.TextColor));
            // Room for the part of the handle that reaches past the slider's ends, so at 50% and
            // 150% it stays clear of the label and the value, as the game pads its own sliders.
            var sliderBox = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var margin = (int)Math.Ceiling(overhang);
            sliderBox.AddThemeConstantOverride("margin_left", margin);
            sliderBox.AddThemeConstantOverride("margin_right", margin);
            sliderBox.AddChild(_iconScale);
            iconScaleRow.AddChild(sliderBox);
            iconScaleRow.AddChild(_iconScaleValue);

            _showHandReminder = CreateToggle("Card reminder");
            _showHandReminder.Toggled += on => Change(nameof(_showHandReminder), settings => settings with { ShowHandReminder = on });

            _showHandPreview = CreateToggle("Teammate hand preview");
            _showHandPreview.Toggled += on => Change(nameof(_showHandPreview), settings => settings with { ShowHandPreview = on });

            // The row icon and the hand preview, then the bubbles on top of the icon, each under its heading.
            Root.AddChild(CreateLabel("Player list", Gold));
            Root.AddChild(_showOnOwnRow);
            Root.AddChild(iconScaleRow);
            Root.AddChild(_showHandPreview);
            Root.AddChild(CreateLabel("Bubbles", Gold));
            Root.AddChild(_showBubble);
            Root.AddChild(_showHandReminder);
            Root.AddChild(_variedBubbleLines);
        }

        internal VBoxContainer Root { get; }

        // The image slot has a fixed height in the game's scene. When the options need more, the panel
        // scales down to fit it and widens by the same factor, so it never runs into the author and
        // version below. Called deferred, after layout. No control in the panel wraps its text, so the
        // width does not change the height it needs, and one measurement is enough.
        internal void FitTo(Control slot)
        {
            if (!GodotObject.IsInstanceValid(Root) || !GodotObject.IsInstanceValid(slot) || slot.Size.X <= 0f || slot.Size.Y <= 0f)
            {
                return;
            }

            var needed = Root.GetCombinedMinimumSize().Y;
            var scale = needed > slot.Size.Y ? slot.Size.Y / needed : 1f;
            Root.Scale = new Vector2(scale, scale);
            Root.OffsetRight = slot.OffsetRight + slot.Size.X * (1f / scale - 1f);
        }

        // Reads the current values without firing the change handlers.
        internal void Sync()
        {
            var settings = SettingsService.Current;
            SetToggle(_showBubble, settings.ShowBubble);
            SetToggle(_variedBubbleLines, settings.VariedBubbleLines);
            SetToggle(_showOnOwnRow, settings.ShowOnOwnRow);
            _iconScale.SetValueNoSignal(settings.IconScalePercent);
            ShowScale(settings.IconScalePercent);
            SetToggle(_showHandReminder, settings.ShowHandReminder);
            SetToggle(_showHandPreview, settings.ShowHandPreview);
        }

        // With the game's tick art, a flat toggle button that swaps its icon like the mod list's tickbox;
        // without it, Godot's own checkbox.
        private BaseButton CreateToggle(string text)
        {
            if (_style.Ticked is null || _style.NotTicked is null)
            {
                var checkBox = new CheckBox { Text = text };
                ApplyText(checkBox, _style.TextColor);
                return checkBox;
            }

            var button = new Button
            {
                Text = text,
                ToggleMode = true,
                Flat = true,
                ExpandIcon = true,
                Alignment = HorizontalAlignment.Left,
                Icon = _style.NotTicked
            };
            ApplyText(button, _style.TextColor);
            button.AddThemeConstantOverride("icon_max_width", (int)(_style.FontSize * 1.5f));
            button.AddThemeConstantOverride("h_separation", _style.FontSize / 2);
            button.AddThemeColorOverride("font_hover_color", Gold);
            button.AddThemeColorOverride("font_hover_pressed_color", Gold);
            button.AddThemeColorOverride("font_focus_color", _style.TextColor);
            button.AddThemeColorOverride("font_pressed_color", _style.TextColor);
            button.AddThemeStyleboxOverride("focus", FocusOutline());
            button.Toggled += on => HookGuard.Run(text, "the tick may not match the setting", () => button.Icon = on ? _style.Ticked : _style.NotTicked);
            return button;
        }

        private void SetToggle(BaseButton toggle, bool on)
        {
            toggle.SetPressedNoSignal(on);
            if (toggle is Button { Flat: true } button)
            {
                button.Icon = on ? _style.Ticked : _style.NotTicked;
            }
        }

        // The game's settings slider look when one is loaded; otherwise the mod's own, whose grabber
        // stays inside the slider, so its overhang is zero.
        private HSlider CreateSlider(Node onScreen, out float overhang)
        {
            var slider = new HSlider
            {
                MinValue = PlayerSettings.MinScalePercent,
                MaxValue = PlayerSettings.MaxScalePercent,
                Step = PlayerSettings.ScaleStepPercent,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
            };
            slider.AddThemeStyleboxOverride("focus", FocusOutline());
            if (GameSliderLook.TryApply(onScreen, slider, out overhang))
            {
                return slider;
            }

            var height = Math.Max(6, _style.FontSize / 4);
            slider.AddThemeStyleboxOverride("slider", Track(new Color(0f, 0f, 0f, 0.45f), height));
            slider.AddThemeStyleboxOverride("grabber_area", Track(Gold with { A = 0.85f }, height));
            slider.AddThemeStyleboxOverride("grabber_area_highlight", Track(Gold, height));
            var grabber = Dot((int)(height * 2.6f), _style.TextColor);
            slider.AddThemeIconOverride("grabber", grabber);
            slider.AddThemeIconOverride("grabber_highlight", Dot((int)(height * 2.6f), Gold));
            return slider;
        }

        private Label CreateLabel(string text, Color color)
        {
            var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
            ApplyText(label, color);
            return label;
        }

        private void ApplyText(Control control, Color color)
        {
            if (_style.Font is not null)
            {
                control.AddThemeFontOverride("font", _style.Font);
            }

            control.AddThemeFontSizeOverride("font_size", _style.FontSize);
            control.AddThemeColorOverride("font_color", color);
        }

        private void ShowScale(int percent)
        {
            _iconScaleValue.Text = $"{percent}%";
        }

        // Unchanged values are skipped by SettingsService.Update, so a drag back to the start writes nothing.
        private void SaveScale()
        {
            var percent = (int)Math.Round(_iconScale.Value);
            Change(nameof(_iconScale), settings => settings with { IconScalePercent = percent });
        }

        private static StyleBoxFlat Track(Color color, int height)
        {
            var box = new StyleBoxFlat { BgColor = color, ContentMarginTop = height / 2f, ContentMarginBottom = height / 2f };
            box.SetCornerRadiusAll(height);
            return box;
        }

        private static StyleBoxFlat FocusOutline()
        {
            var box = new StyleBoxFlat { DrawCenter = false, BorderColor = Gold };
            box.SetBorderWidthAll(2);
            box.SetCornerRadiusAll(6);
            box.SetExpandMarginAll(4f);
            return box;
        }

        // A filled circle with a soft edge, drawn once per panel for the slider grabber.
        private static ImageTexture Dot(int size, Color color)
        {
            var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            var radius = size / 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = new Vector2(x + 0.5f - radius, y + 0.5f - radius).Length();
                    image.SetPixel(x, y, color with { A = Mathf.Clamp(radius - distance, 0f, 1f) });
                }
            }

            return ImageTexture.CreateFromImage(image);
        }

        private static void Change(string control, Func<PlayerSettings, PlayerSettings> change)
        {
            HookGuard.Run(control, "the setting keeps its current value", () => SettingsService.Update(change(SettingsService.Current)));
        }
    }
}
