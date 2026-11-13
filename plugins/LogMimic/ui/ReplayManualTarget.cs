namespace Oracle.Services;

internal readonly record struct ReplayManualObject(ulong Id, bool IsDead, bool IsTargetable);
internal readonly record struct ReplayManualSelection(ulong? ObjectId, bool Self = false);

internal static class ReplayManualTarget
{
    public static bool IsSelfOnly(ReplayActionTraits action) => action.Self && !action.Hostile
        && !action.Party && !action.Alliance && !action.Area;

    // No log actor, name lookup, binding or previous target participates in selection.
    public static ReplayManualSelection Choose(ReplayActionTraits action, ulong? playerId, ReplayManualObject? selected)
    {
        if (IsSelfOnly(action)) return ValidId(playerId) ? new(playerId, true) : new(null);
        if (selected is { IsDead: false, IsTargetable: true } target && ValidId(target.Id))
            return new(target.Id);
        return new(null);
    }

    private static bool ValidId(ulong? id) => id is > 0 && id != 0xE0000000UL;
}
