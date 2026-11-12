namespace Oracle.Services;

internal sealed record ReplayPhaseMark(string CueId, float At, float ExpectedDuration);
internal sealed record ReplayPhaseAnchor(string CueId, float At);
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

    public ReplayPhasePlan(IEnumerable<ReplayPhaseMark> marks, IEnumerable<ReplayPhaseAnchor> anchors)
    {
        ReplayPhaseMark? previous = null;
        var segment = 0;
        var syncs = anchors.Where(a => float.IsFinite(a.At) && a.At >= 0).OrderBy(a => a.At).ToArray();
        foreach (var mark in marks.OrderBy(m => m.At))
        {
            if (previous != null && mark.At - previous.At > Math.Max(5, previous.ExpectedDuration + 1))
            {
                segment++;
                var anchor = syncs.LastOrDefault(a => a.At > previous.At && a.At <= mark.At);
                Boundaries.Add(new(segment, mark.At, anchor?.CueId));
            }
            Segments[mark.CueId] = segment;
            previous = mark;
        }
    }
}
