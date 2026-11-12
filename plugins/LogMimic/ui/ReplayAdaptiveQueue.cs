using System.Collections.Concurrent;

namespace Oracle.Services;

internal readonly record struct ReplayActionTraits(int CooldownGroup, int AdditionalCooldownGroup,
    bool Self, bool Hostile, bool Party, bool Alliance, bool Area)
{
    public bool IsGcd => CooldownGroup == 58 || AdditionalCooldownGroup == 58;
    public bool UsesSelf(int loggedTarget) => !Area && Self && !Hostile
        && (loggedTarget <= 0 || (!Party && !Alliance));
}

internal readonly record struct ReplayNativeWindow(float GcdRemaining, float CastRemaining,
    float AnimationLock, bool ActionQueued, uint QueuedActionId = 0, uint CastingActionId = 0, uint LastUsedSequence = 0)
{
    private bool Valid => float.IsFinite(GcdRemaining) && float.IsFinite(CastRemaining)
        && float.IsFinite(AnimationLock);
    public bool CanQueueGcd => Valid && !ActionQueued && AnimationLock <= 0.001f
        && GcdRemaining <= 0.4f && CastRemaining <= 0.4f;
    public bool CanWeave(bool allowIdle = false, float lockSeconds = 0.6f) => Valid && !ActionQueued
        && AnimationLock <= 0.001f && CastRemaining <= 0.001f
        && (GcdRemaining >= Math.Max(0, lockSeconds) + 0.1f || (allowIdle && GcdRemaining <= 0.001f));
}

internal sealed record ReplayAdaptiveStep(string CueId, uint ActionId, bool IsGcd, int GcdBefore, int Segment = 0);
internal sealed record ReplayAdaptivePending(ReplayAdaptiveStep Step, uint ActualActionId, long Deadline,
    long RequestedAt, bool NativeQueued, uint BeforeUseSequence, long ExpectedCastEnd = 0);
internal readonly record struct ReplayAttemptOutcome(bool Accepted, uint ActionId = 0,
    int TimeoutMs = 2600, bool TargetBlocked = false, bool NativeQueued = false, uint BeforeUseSequence = 0);

// GCD order stays strict; usable abilities are tried in recorded priority. Abilities are released
// only after the GCD that preceded them in the log, rather than all firing at time zero.
internal sealed class ReplayAdaptiveQueue
{
    private readonly List<ReplayAdaptiveStep> _gcd = [];
    private readonly List<ReplayAdaptiveStep> _ogcd = [];
    private int _gcdIndex;
    private readonly HashSet<string> _ogcdDone = [];
    private int _phase;
    private int _discardBeforePhase;
    public ReplayAdaptivePending? PendingGcd { get; private set; }
    public ReplayAdaptivePending? PendingOgcd { get; private set; }
    public int CompletedCount => _gcdIndex + _ogcdDone.Count;
    public bool Finished => _gcdIndex == _gcd.Count && _ogcdDone.Count == _ogcd.Count;
    public ReplayAdaptiveStep? NextGcd => _gcdIndex < _gcd.Count && _gcd[_gcdIndex].Segment <= _phase ? _gcd[_gcdIndex] : null;
    private IEnumerable<ReplayAdaptiveStep> ReleasedOgcds => _ogcd.Where(s =>
        s.GcdBefore <= _gcdIndex && s.Segment <= _phase && !_ogcdDone.Contains(s.CueId));
    public ReplayAdaptiveStep? NextOgcd => ReleasedOgcds.FirstOrDefault();

    public ReplayAdaptiveQueue(IEnumerable<(string Id, uint ActionId, bool IsGcd)> actions)
        : this(actions.Select(a => (a.Id, a.ActionId, a.IsGcd, 0))) { }

    public ReplayAdaptiveQueue(IEnumerable<(string Id, uint ActionId, bool IsGcd, int Segment)> actions)
    {
        var precedingGcds = 0;
        foreach (var (id, actionId, isGcd, segment) in actions)
        {
            var step = new ReplayAdaptiveStep(id, actionId, isGcd, precedingGcds, segment);
            if (isGcd) { _gcd.Add(step); precedingGcds++; }
            else _ogcd.Add(step);
        }
    }

    public bool Dispatch(ReplayNativeWindow window, long now,
        Func<ReplayAdaptiveStep, ReplayAttemptOutcome> attempt,
        Func<ReplayAdaptiveStep, float>? expectedLock = null, bool targetAvailable = true)
    {
        if (!targetAvailable) return false;
        var gcd = PendingGcd == null && window.CanQueueGcd ? NextGcd : null;
        var ogcds = PendingOgcd == null ? ReleasedOgcds.Where(s =>
            window.CanWeave(true, expectedLock?.Invoke(s) ?? 0.6f)).ToList() : [];
        var leading = _gcdIndex == 0 ? ogcds.Where(s => s.GcdBefore == 0).ToList() : [];
        var candidates = leading.Cast<ReplayAdaptiveStep?>().Concat(new[] { gcd })
            .Concat(ogcds.Where(s => !leading.Contains(s)));
        var gcdTargetBlocked = false;
        foreach (var step in candidates)
        {
            if (step == null) continue;
            if (!step.IsGcd && window.GcdRemaining <= 0.001f && gcdTargetBlocked) continue;
            var result = attempt(step);
            if (!result.Accepted)
            {
                if (step.IsGcd) gcdTargetBlocked = result.TargetBlocked;
                continue;
            }
            var pending = new ReplayAdaptivePending(step, result.ActionId,
                now + Math.Max(2600, result.TimeoutMs), now, result.NativeQueued, result.BeforeUseSequence);
            if (step.IsGcd) PendingGcd = pending;
            else PendingOgcd = pending;
            return true;
        }
        return false;
    }

    public ReplayAdaptiveStep? Confirm(uint actualActionId)
    {
        if (PendingGcd is { } gcd && gcd.ActualActionId == actualActionId)
        {
            PendingGcd = null; _gcdIndex++; DiscardOldPhase(); return gcd.Step;
        }
        if (PendingOgcd is { } ogcd && ogcd.ActualActionId == actualActionId)
        {
            PendingOgcd = null; _ogcdDone.Add(ogcd.Step.CueId); DiscardOldPhase(); return ogcd.Step;
        }
        return null;
    }

    public void Expire(long now, ReplayNativeWindow window)
    {
        ReplayAdaptivePending? ObserveCast(ReplayAdaptivePending? pending) => pending != null
            && pending.ActualActionId == window.CastingActionId && window.CastRemaining > 0
            ? pending with { ExpectedCastEnd = now + (long)Math.Ceiling(window.CastRemaining * 1000) } : pending;
        PendingGcd = ObserveCast(PendingGcd);
        PendingOgcd = ObserveCast(PendingOgcd);
        bool Expired(ReplayAdaptivePending pending)
        {
            if (pending.ActualActionId == window.QueuedActionId || pending.ActualActionId == window.CastingActionId) return false;
            if (pending.ExpectedCastEnd > now + 100) return true;
            // A disappeared native queue with no new outgoing action sequence was cancelled,
            // not used. Retry the same cue without the full server-confirmation timeout.
            if (pending.NativeQueued && now - pending.RequestedAt > 800
                && window.LastUsedSequence == pending.BeforeUseSequence) return true;
            return now > pending.Deadline;
        }
        if (PendingGcd is { } gcd && Expired(gcd)) PendingGcd = null;
        if (PendingOgcd is { } ogcd && Expired(ogcd)) PendingOgcd = null;
        DiscardOldPhase();
    }

    public void ReleasePhase(int phase, bool discardEarlier = false)
    {
        _phase = Math.Max(_phase, phase);
        if (discardEarlier) _discardBeforePhase = Math.Max(_discardBeforePhase, phase);
        DiscardOldPhase();
    }

    private void DiscardOldPhase()
    {
        if (PendingGcd == null)
            while (_gcdIndex < _gcd.Count && _gcd[_gcdIndex].Segment < _discardBeforePhase) _gcdIndex++;
        foreach (var step in _ogcd)
            if (step.Segment < _discardBeforePhase && step.CueId != PendingOgcd?.Step.CueId) _ogcdDone.Add(step.CueId);
    }

    public bool Skip()
    {
        if (PendingGcd != null || PendingOgcd != null) return false;
        if (NextGcd != null) _gcdIndex++;
        else if (NextOgcd is { } ogcd) _ogcdDone.Add(ogcd.CueId);
        DiscardOldPhase();
        return true;
    }
}

// An ActionEffect can be repeated for several targets. Its source sequence identifies
// one actual use; duplicate packets must not consume the next identical logged skill.
internal sealed class ReplayConfirmationQueue
{
    private readonly ConcurrentQueue<(uint ActionId, uint Sequence)> _queue = new();
    private readonly HashSet<(uint Sequence, uint ActionId)> _seen = [];
    private readonly Queue<(uint Sequence, uint ActionId)> _recent = new();
    public void Enqueue(uint actionId, uint sequence)
    {
        if (actionId != 0 && sequence != 0) _queue.Enqueue((actionId, sequence));
    }
    public bool TryDequeue(out uint actionId)
    {
        while (_queue.TryDequeue(out var item))
        {
            var key = (item.Sequence, item.ActionId);
            if (!_seen.Add(key)) continue;
            _recent.Enqueue(key);
            if (_recent.Count > 256) _seen.Remove(_recent.Dequeue());
            actionId = item.ActionId; return true;
        }
        actionId = 0; return false;
    }
    public void Clear() { _queue.Clear(); _seen.Clear(); _recent.Clear(); }
}
