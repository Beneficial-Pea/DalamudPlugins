namespace Oracle.Services;

internal readonly record struct ReplayCastMark(int Index, uint ActionId, int TargetId, double Timestamp, bool IsBegin);
internal readonly record struct ReplayCastWindow(int Index, double StartTimestamp);

internal static class ReplayCastTiming
{
    public static IReadOnlyList<ReplayCastWindow> Resolve(IEnumerable<ReplayCastMark> events)
    {
        var starts = new Dictionary<uint, List<ReplayCastMark>>();
        var result = new List<ReplayCastWindow>();
        foreach (var item in events.OrderBy(e => e.Timestamp).ThenBy(e => e.IsBegin ? 0 : 1))
        {
            if (!starts.TryGetValue(item.ActionId, out var pending)) starts[item.ActionId] = pending = [];
            if (item.IsBegin) { pending.Add(item); continue; }
            var begin = pending.LastOrDefault(b => b.Timestamp <= item.Timestamp && item.Timestamp - b.Timestamp <= 30000
                && (b.TargetId == 0 || item.TargetId == 0 || b.TargetId == item.TargetId));
            var found = begin.ActionId != 0;
            result.Add(new(item.Index, found ? begin.Timestamp : item.Timestamp));
            if (found) pending.RemoveAll(b => b.Timestamp <= begin.Timestamp);
            pending.RemoveAll(b => item.Timestamp - b.Timestamp > 30000);
        }
        return result;
    }
}

internal static class ReplayPacing
{
    public static int ConfirmationTimeoutMs(float castSeconds) =>
        (int)Math.Max(2600, Math.Ceiling(Math.Max(0, castSeconds) * 1000) + 1500);
    public static float Scheduled(float completion, float? recordedStart) => Math.Max(0, recordedStart ?? completion);
    public static float AlignmentOffset(bool align, IEnumerable<float> scheduled) => align ? scheduled.DefaultIfEmpty(0).Min() : 0;
}
