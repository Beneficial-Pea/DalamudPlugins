using Oracle.Models;

namespace Oracle.Services;

internal sealed class ReplayPhaseSession
{
    public ReplayPhasePlan Plan { get; }
    public ReplayPhaseMode Mode { get; }
    public int CurrentSegment { get; private set; }
    public float ReadyAt { get; private set; }

    public ReplayPhaseSession(ReplayPhaseMode mode, IEnumerable<ReplayPhaseMark> marks,
        IEnumerable<ReplayPhaseAnchor> anchors)
    {
        Mode = mode;
        var actions = marks.ToArray();
        Plan = mode == ReplayPhaseMode.AlignToPhase
            ? new ReplayPhasePlan(actions, anchors, requireSync: true)
            : ReplayPhasePlan.Continuous(actions);
    }

    public ReplayPhaseBoundary? Observe(Func<string, bool> observed)
    {
        // Clock time and changes to the player's target never imply a new phase.
        var boundary = Plan.Boundaries.LastOrDefault(b => b.Segment > CurrentSegment
            && b.SyncCueId != null && observed(b.SyncCueId));
        if (boundary == null) return null;
        CurrentSegment = boundary.Segment;
        ReadyAt = boundary.At;
        return boundary;
    }

    // The encounter clock resyncs to the observed cue. Preserve any recorded delay
    // between that signal and the opening action, even when the phase starts early/late.
    public bool CanDispatch(float elapsed) => float.IsFinite(elapsed) && elapsed >= ReadyAt;
}
