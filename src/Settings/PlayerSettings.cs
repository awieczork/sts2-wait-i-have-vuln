using System.Text;
using System.Text.Json;
using WaitIHaveVuln.Indicator;

namespace WaitIHaveVuln.Settings;

/// <summary>
/// The viewing player's saved options. Every option is local to this player and changes only
/// what they see. Values from a file are read leniently: a missing or wrong-typed field takes its
/// default, the icon scale is clamped and snapped to its step, and unknown fields are ignored.
/// </summary>
internal sealed record PlayerSettings(int IconScalePercent, bool ShowBubble, bool ShowOnOwnRow, bool ShowHandReminder, bool VariedBubbleLines, bool ShowHandPreview)
{
    internal const int MinScalePercent = 50;
    // 150% of the 36-unit icon is about the largest size checked clear of the relic bar above the row.
    internal const int MaxScalePercent = 150;
    internal const int ScaleStepPercent = 10;
    // Written to every file and not read yet: reserved for a future change of the format that needs a migration.
    private const int FormatVersion = 1;

    internal static PlayerSettings Default { get; } = new(100, true, true, true, true, true);

    internal float IconScale => IconScalePercent / 100f;

    // ShowBubble is the switch for every bubble; without it only the row icon shows. The card
    // reminder, the cloud at the hovered card, is one of those bubbles.
    internal bool ShowsCardReminder => ShowBubble && ShowHandReminder;

    // With the own row off, the indicator stays off on that row, icon and bubble alike.
    internal IndicatorLevel ShownLevel(IndicatorLevel level, bool isOwnRow)
    {
        return ShowOnOwnRow || !isOwnRow ? level : IndicatorLevel.Off;
    }

    internal PlayerSettings Normalize()
    {
        return this with { IconScalePercent = SnapScale(IconScalePercent) };
    }

    /// <summary>Returns false when the text is not a JSON object; the settings are then the defaults.</summary>
    internal static bool TryParse(string json, out PlayerSettings settings)
    {
        settings = Default;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            settings = new PlayerSettings(
                ReadScale(root, "iconScalePercent", Default.IconScalePercent),
                ReadBool(root, "showBubble", Default.ShowBubble),
                ReadBool(root, "showOnOwnRow", Default.ShowOnOwnRow),
                ReadBool(root, "showHandReminder", Default.ShowHandReminder),
                ReadBool(root, "variedBubbleLines", Default.VariedBubbleLines),
                ReadBool(root, "showHandPreview", Default.ShowHandPreview));
            return true;
        }
    }

    internal string ToJson()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", FormatVersion);
            writer.WriteNumber("iconScalePercent", IconScalePercent);
            writer.WriteBoolean("showBubble", ShowBubble);
            writer.WriteBoolean("showOnOwnRow", ShowOnOwnRow);
            writer.WriteBoolean("showHandReminder", ShowHandReminder);
            writer.WriteBoolean("variedBubbleLines", VariedBubbleLines);
            writer.WriteBoolean("showHandPreview", ShowHandPreview);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static int ReadScale(JsonElement root, string name, int fallback)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return fallback;
        }

        // A number beyond double's range, such as 1e400, does not parse; it clamps by its sign like any other too-large value.
        if (!value.TryGetDouble(out var number))
        {
            return value.GetRawText().StartsWith('-') ? MinScalePercent : MaxScalePercent;
        }

        return SnapScale(number);
    }

    private static bool ReadBool(JsonElement root, string name, bool fallback)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;
    }

    // Both bounds are multiples of the step, so snapping after clamping stays in range.
    private static int SnapScale(double percent)
    {
        var clamped = Math.Clamp(percent, MinScalePercent, MaxScalePercent);
        return (int)Math.Round(clamped / ScaleStepPercent, MidpointRounding.AwayFromZero) * ScaleStepPercent;
    }
}
