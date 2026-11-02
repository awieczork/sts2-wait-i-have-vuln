using Godot;

namespace WaitIHaveVuln.Settings;

internal static class NodeSearch
{
    // Depth-first, so the first match is the one nearest the top of the tree order.
    internal static T? FindDescendant<T>(Node node) where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    // A copy of a node with a script would run the game's code on our panel.
    internal static bool IsScriptFree(Node node)
    {
        if (node.GetScript().VariantType != Variant.Type.Nil)
        {
            return false;
        }

        foreach (var child in node.GetChildren())
        {
            if (!IsScriptFree(child))
            {
                return false;
            }
        }

        return true;
    }
}
