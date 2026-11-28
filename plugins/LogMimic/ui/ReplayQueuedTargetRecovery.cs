namespace Oracle.Services;

internal sealed record ReplayQueuedRequest(string CueId, uint ActionId, ulong TargetId,
    uint BeforeSequence, long Deadline, bool FollowSelection = true);
internal enum ReplayQueueRecovery { None, Retarget, AdoptNativeTarget, CancelOwnedQueue, RetryUnsent, ForgetSent }

internal static class ReplayQueuedTargetRecovery
{
    public const int HardQueueGraceMs = 1000;

    public static ReplayQueuedRequest? Capture(string cueId, uint actionId, ulong targetId,
        ReplayActionTraits traits, ReplayNativeWindow before, ReplayNativeWindow after, long deadline)
    {
        if (before.ActionQueued || !after.ActionQueued || after.QueuedActionType != 1
            || after.QueuedActionId != actionId || after.QueuedTargetId != targetId
            || after.LastUsedSequence != before.LastUsedSequence) return null;
        return new(cueId, actionId, targetId, before.LastUsedSequence, deadline,
            FollowSelection: traits.Hostile && !traits.Area);
    }

    public static ReplayQueueRecovery Decide(ReplayQueuedRequest request, ReplayNativeWindow window,
        ulong? selectedId, bool selectedUsable, long now, bool cuePending = true)
    {
        // A changed send sequence or an active cast can mean the action has already
        // left the queue. Never retry or redirect an action already being executed.
        var casting = window.CastingActionId == request.ActionId && window.CastRemaining > 0;
        if (window.LastUsedSequence != request.BeforeSequence || casting) return ReplayQueueRecovery.ForgetSent;
        var sameActionQueue = window.ActionQueued && window.QueuedActionType == 1
            && window.QueuedActionId == request.ActionId;
        if (!sameActionQueue) return ReplayQueueRecovery.RetryUnsent;
        if (!float.IsFinite(window.GcdRemaining) || !float.IsFinite(window.CastRemaining)
            || !float.IsFinite(window.AnimationLock)) return ReplayQueueRecovery.None;
        var validSelection = selectedId is > 0 && selectedId != 0xE0000000UL;
        var stalled = now > request.Deadline && window.GcdRemaining <= 0.001f
            && window.CastRemaining <= 0.001f && window.AnimationLock <= 0.001f;
        if (!request.FollowSelection)
        {
            // Self/ally input retains its committed target. It must never be
            // redirected to the enemy selected for the next damage action.
            if (window.QueuedTargetId != request.TargetId) return ReplayQueueRecovery.RetryUnsent;
            if (!cuePending || !validSelection || selectedId != request.TargetId)
                return ReplayQueueRecovery.CancelOwnedQueue;
        }
        if (window.QueuedTargetId != request.TargetId)
        {
            // The game may already have followed the new hard target. Count that
            // same unexecuted action once instead of dropping its pending marker.
            if (validSelection && window.QueuedTargetId == selectedId) return ReplayQueueRecovery.AdoptNativeTarget;
            return stalled ? ReplayQueueRecovery.CancelOwnedQueue : ReplayQueueRecovery.None;
        }
        if (!validSelection || selectedId != request.TargetId)
            return validSelection && selectedUsable ? ReplayQueueRecovery.Retarget : ReplayQueueRecovery.CancelOwnedQueue;
        // A queue created inside the native input window cannot legitimately
        // wait forever. Release only this exact unsent input, even if its GCD
        // counter is stuck; keep its source record for a real retry/confirmation.
        if (!cuePending || (now > request.Deadline + HardQueueGraceMs && window.CastRemaining <= 0.001f))
            return ReplayQueueRecovery.CancelOwnedQueue;
        // Same-target native queues must not pin a replay forever after their input window.
        if (stalled) return ReplayQueueRecovery.CancelOwnedQueue;
        return ReplayQueueRecovery.None;
    }
}
