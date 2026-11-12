namespace Oracle.Services;

internal readonly record struct ReplayTargetSelection(ulong? ObjectId, bool UsedCurrentSelection);

internal static class ReplayTargetChoice
{
    public static ReplayTargetSelection Choose(IReadOnlyList<ulong> matchingObjects, ulong? currentSelection, bool allowAmbiguousSelection)
    {
        var matches = matchingObjects.Where(id => id != 0 && id != 0xE0000000UL).Distinct().ToArray();
        if (matches.Length == 1) return new(matches[0], false);
        if (matches.Length > 1 && allowAmbiguousSelection && currentSelection is { } selected && matches.Contains(selected))
            return new(selected, true);
        return new(null, false);
    }
}
