namespace Oracle.Services;

internal sealed record ReplayProcPair(uint JobId, uint BaseRoot, uint ProcRoot,
    uint[] BaseIds, uint[] ProcIds, uint[] ReadyStatuses)
{
    public bool Contains(uint id) => BaseIds.Contains(id) || ProcIds.Contains(id);
}

internal static class ReplayProcPairs
{
    // RSR DancerRotation/BardRotation action checks; legacy ready buffs keep older
    // Korean client data compatible. Native adjustment handles Bard's level upgrades.
    public static IReadOnlyList<ReplayProcPair> Pairs { get; } = new ReplayProcPair[] {
        new(38,15989,15991,[15989],[15991],[2693,3017,1814]),
        new(38,15990,15992,[15990],[15992],[2694,3018,1815]),
        new(38,15993,15995,[15993],[15995],[2693,3017,1816]),
        new(38,15994,15996,[15994],[15996],[2694,3018,1817]),
        new(23,97,98,[97,16495],[98,7409],[3861,122,128]),
    };

    public static uint Select(uint jobId, uint recordedId, uint adjustedRecorded,
        Func<uint,uint> adjust, Func<uint,bool> readyStatus, Func<uint,bool> learned,
        IReadOnlyCollection<uint>? excluded = null)
    {
        var pair = Pairs.FirstOrDefault(p => p.JobId == jobId && p.Contains(recordedId));
        if (pair == null || !pair.Contains(adjustedRecorded)) return adjustedRecorded;
        // Keep explicit exclusions and native transformations (such as DNC steps).
        if (excluded?.Any(pair.Contains) == true) return adjustedRecorded;
        uint Adjust(uint id) { var actual = adjust(id); return actual == 0 ? id : actual; }
        var basic = Adjust(pair.BaseRoot);
        var proc = Adjust(pair.ProcRoot);
        if (!pair.BaseIds.Contains(basic) || !pair.ProcIds.Contains(proc)) return adjustedRecorded;
        return pair.ReadyStatuses.Any(readyStatus) && learned(proc) ? proc : basic;
    }

    public static bool IsLiveStatus(uint statusId, float remaining) => statusId != 0
        && float.IsFinite(remaining) && remaining > 0;
}
