namespace Oracle.Services;

internal sealed record ReplayPhaseMark(string CueId, float At, float ExpectedDuration);
internal sealed record ReplayPhaseAnchor(string CueId, float At, bool StartsPhase = false);
internal sealed record ReplayPhaseBoundary(int Segment, float At, string? SyncCueId)
{
    public bool CanRelease(float elapsed, Func<string, bool> observed) => float.IsFinite(elapsed)
        && elapsed >= At && (SyncCueId == null || observed(SyncCueId));
}

// Retain recorded phase/downtime gaps. A configured sync inside a gap must be
// observed in the real encounter before releasing that section of the replay.
internal sealed class ReplayPhasePlan
{
    public Dictionary<string, int> Segments { get; } = [];
    public List<ReplayPhaseBoundary> Boundaries { get; } = [];

    public static ReplayPhasePlan Continuous(IEnumerable<ReplayPhaseMark> marks)
    {
        var plan = new ReplayPhasePlan([], []);
        foreach (var mark in marks) plan.Segments[mark.CueId] = 0;
        return plan;
    }

    public ReplayPhasePlan(IEnumerable<ReplayPhaseMark> marks, IEnumerable<ReplayPhaseAnchor> anchors, bool requireSync = false)
    {
        ReplayPhaseMark? previous = null;
        var segment = 0;
        var syncs = anchors.Where(a => float.IsFinite(a.At) && a.At >= 0).OrderBy(a => a.At).ToArray();
        foreach (var mark in marks.OrderBy(m => m.At))
        {
            var explicitAnchor = syncs.LastOrDefault(a => a.StartsPhase
                && a.At > (previous?.At ?? float.NegativeInfinity) && a.At <= mark.At);
            var gap = previous != null && mark.At - previous.At > Math.Max(5, previous.ExpectedDuration + 1);
            var anchor = explicitAnchor ?? (gap ? syncs.LastOrDefault(a => a.At > previous!.At && a.At <= mark.At) : null);
            if ((gap || explicitAnchor != null) && (!requireSync || anchor != null))
            {
                segment++;
                Boundaries.Add(new(segment, mark.At, anchor?.CueId));
            }
            Segments[mark.CueId] = segment;
            previous = mark;
        }
    }
}
