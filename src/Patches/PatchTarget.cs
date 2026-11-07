using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;

namespace WaitIHaveVuln.Patches;

// Each patch class checks its target and the private fields it injects in Prepare, which Harmony calls
// first with no target (original is null) before it binds anything, so a game update that renames one
// skips only that class, with one line that names the missing member and the feature that stays off,
// instead of a bind exception.
internal static class PatchTarget
{
    internal static bool Exists(string patchClass, string feature, Type type, string method, Type[]? arguments = null, params string[] fields)
    {
        // Matched by name and, when given, argument types, so an overloaded target is never ambiguous.
        var found = AccessTools.FirstMethod(type, candidate => candidate.Name == method
            && (arguments is null || candidate.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(arguments)));
        var missing = found is null
            ? $"{type.Name}.{method}"
            : fields.Where(field => AccessTools.Field(type, field) is null).Select(field => $"{type.Name}.{field}").FirstOrDefault();
        if (missing is null)
        {
            return true;
        }

        Log.Warn($"[{ModEntry.ModId}] Skipped {patchClass}: {missing} not found in this game version; {feature} stays off.");
        return false;
    }
}
