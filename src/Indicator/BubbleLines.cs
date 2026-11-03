using System.Text;

namespace WaitIHaveVuln.Indicator;

/// <summary>
/// The bubble lines, English only. <c>{you}</c> is the local player's name, <c>{them}</c> the
/// speaking teammate's, and <c>{attacker}</c> that of the player whose Attack the teammate reacts to;
/// a line whose name is missing is skipped.
/// </summary>
internal static class BubbleLines
{
    internal const string Signature = "Wait! I have vuln!";
    internal const string You = "{you}";
    internal const string Them = "{them}";
    internal const string Attacker = "{attacker}";
    // The one angry line with varied lines off.
    internal const string FixedAngry = "Nooo!";
    internal const int MaxNameLength = 16;

    // First person, over the teammate's character, when a supported card arrives in their hand.
    internal static readonly IReadOnlyList<string> Row = new[]
    {
        Signature,
        "I have vuln!",
        "Got vuln!",
        "Vuln in hand!",
        "Vuln first!",
        "Hold your attacks!",
        "Let me go first!",
        "{you}, wait!",
        "{you}, I have vuln!"
    };

    // The teammate's reaction when another player plays an Attack while they still hold Vulnerable,
    // angrier with each such Attack in the turn: the first, the second, and the third and later.
    internal static readonly IReadOnlyList<IReadOnlyList<string>> Angry = new[]
    {
        new[] { FixedAngry, "Wait!", "Why?!", "{attacker}, wait!" },
        new[] { "I HAD VULN!", "Again?!", "Seriously?!", "We talked about this!", "{attacker}, really?!" },
        new[] { "Unbelievable.", "I'm writing this down.", "I'll just hold it forever then.", "Fine. I'll play Defends.", "{attacker}, we need to talk." }
    };

    /// <summary>
    /// A player name fit for a bubble, or null when there is none: the bubble's label reads BBCode,
    /// so square brackets are removed, and a long name is cut to <see cref="MaxNameLength"/> with an ellipsis.
    /// </summary>
    internal static string? CleanName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            if (character is not ('[' or ']') && !char.IsControl(character))
            {
                builder.Append(character);
            }
        }

        var cleaned = builder.ToString().Trim();
        if (cleaned.Length == 0)
        {
            return null;
        }

        if (cleaned.Length <= MaxNameLength)
        {
            return cleaned;
        }

        // Never between the two halves of a character outside the basic plane, such as an emoji.
        var cut = MaxNameLength - 1;
        if (char.IsHighSurrogate(cleaned[cut - 1]))
        {
            cut--;
        }

        return cleaned[..cut].TrimEnd() + "…";
    }

    /// <summary>The line with its names filled in, or null when a name it needs is missing.</summary>
    internal static string? Fill(string line, string? you, string? them, string? attacker = null)
    {
        if ((line.Contains(You, StringComparison.Ordinal) && you is null) || (line.Contains(Them, StringComparison.Ordinal) && them is null)
            || (line.Contains(Attacker, StringComparison.Ordinal) && attacker is null))
        {
            return null;
        }

        return line.Replace(You, you, StringComparison.Ordinal).Replace(Them, them, StringComparison.Ordinal).Replace(Attacker, attacker, StringComparison.Ordinal);
    }
}
