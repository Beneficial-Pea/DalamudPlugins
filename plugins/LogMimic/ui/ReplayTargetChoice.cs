namespace Oracle.Services;

internal readonly record struct SelectedReplayTarget(ulong ObjectId, bool IsEnemy, bool IsDead, bool IsTargetable);
internal readonly record struct ReplayTargetSelection(bool OverrideRecorded, ulong? ObjectId);

internal static class ReplayTargetChoice
{
    public static ReplayTargetSelection Choose(bool followSelected, string recordedKind, SelectedReplayTarget? selected)
    {
        if (!followSelected || recordedKind != "enemy") return new(false, null);
        if (selected is { IsEnemy: true, IsDead: false, IsTargetable: true } target
            && target.ObjectId != 0 && target.ObjectId != 0xE0000000UL)
            return new(true, target.ObjectId);
        // Following the user's selection must not fall back to a different recorded enemy.
        return new(true, null);
    }
}
