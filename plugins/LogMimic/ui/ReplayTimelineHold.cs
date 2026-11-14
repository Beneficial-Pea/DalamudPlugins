namespace Oracle.Services;

internal sealed class ReplayTimelineHold
{
    private DateTime? _heldUtc;
    public bool IsHeld => _heldUtc.HasValue;
    public bool HasHeld { get; private set; }
    public DateTime Now(DateTime liveUtc) => _heldUtc ?? liveUtc;

    // Return only the completed pause interval; the engine shifts its playback
    // origin and highlight timestamps by this amount when resuming.
    public TimeSpan SetHeld(bool hold, DateTime liveUtc)
    {
        if (hold)
        {
            if (!_heldUtc.HasValue) { _heldUtc = liveUtc; HasHeld = true; }
            return TimeSpan.Zero;
        }
        if (_heldUtc is not { } start) return TimeSpan.Zero;
        _heldUtc = null;
        return liveUtc > start ? liveUtc - start : TimeSpan.Zero;
    }

    public void Reset() { _heldUtc = null; HasHeld = false; }
}

internal enum ReplayClockHoldReason { None, ManualPause, Target, ActionCondition }
internal readonly record struct ReplayClockHoldDecision(ReplayClockHoldReason Reason)
{
    public bool Hold => Reason != ReplayClockHoldReason.None;
    public bool BlockMainActions => Reason is ReplayClockHoldReason.Target or ReplayClockHoldReason.ManualPause;
}

internal static class ReplayClockHoldPolicy
{
    public static ReplayClockHoldDecision Decide(bool eligible, float elapsed, bool suspended,
        bool hasRemaining, bool targetRequired, bool targetReady, bool normalBusy, bool actionBlocked)
    {
        if (!eligible || !float.IsFinite(elapsed) || elapsed < 0) return new(ReplayClockHoldReason.None);
        if (suspended) return new(ReplayClockHoldReason.ManualPause);
        if (!hasRemaining) return new(ReplayClockHoldReason.None);
        if (targetRequired && !targetReady) return new(ReplayClockHoldReason.Target);
        if (!normalBusy && actionBlocked) return new(ReplayClockHoldReason.ActionCondition);
        return new(ReplayClockHoldReason.None);
    }
}
