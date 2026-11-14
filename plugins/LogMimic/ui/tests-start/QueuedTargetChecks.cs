using Oracle.Services;

internal static class QueuedTargetChecks
{
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    public static void Run()
    {
        var request = new ReplayQueuedRequest("first",7411,101,7,2600);
        var queued = new ReplayNativeWindow(0.2f,0,0,true,7411,0,7,101,1);
        Check(ReplayQueuedTargetRecovery.Decide(request,queued,202,true,100)==ReplayQueueRecovery.Retarget,
            "MCH boss A still alive to add B: retarget the replay's unsent native input");
        var moved = request with { TargetId=202 };
        Check(ReplayQueuedTargetRecovery.Decide(moved,queued with { QueuedTargetId=202 },202,true,110)==ReplayQueueRecovery.None,
            "A queue already bound to B must not create a second input");
        Check(ReplayQueuedTargetRecovery.Decide(moved,queued with { QueuedTargetId=202 },303,true,120)==ReplayQueueRecovery.Retarget,
            "Successive manual target changes follow the newest selected enemy");
        Check(ReplayQueuedTargetRecovery.Decide(request,queued with { QueuedTargetId=202 },202,true,100)==ReplayQueueRecovery.AdoptNativeTarget,
            "An input already retargeted by the game keeps the same pending cue without a duplicate request");
        Check(ReplayQueuedTargetRecovery.Decide(request,queued with { QueuedTargetId=303 },202,true,100)==ReplayQueueRecovery.None,
            "An early same-action input with a different target is not blindly overwritten");
        Check(ReplayQueuedTargetRecovery.Decide(request,queued with { QueuedTargetId=303,GcdRemaining=0 },202,true,2700)==ReplayQueueRecovery.CancelOwnedQueue,
            "A same-action target change cannot disable the deadline watchdog indefinitely");
        Check(ReplayQueuedTargetRecovery.Decide(request,queued,null,false,100)==ReplayQueueRecovery.CancelOwnedQueue,
            "Clearing the target cancels only the unfired input and leaves the source cue waiting");
        Check(ReplayQueuedTargetRecovery.Decide(request,queued,202,false,100)==ReplayQueueRecovery.CancelOwnedQueue,
            "An unreachable or invalid new target must not leave an old boss input stuck");
        Check(ReplayQueuedTargetRecovery.Decide(request,queued,101,false,100)==ReplayQueueRecovery.None,
            "Temporary same-target native locks must not churn a normal input queue");
        Check(ReplayQueuedTargetRecovery.Decide(request,queued with { LastUsedSequence=8 },202,true,100)==ReplayQueueRecovery.ForgetSent,
            "An action already sent to A must not be resent or redirected to B");
        Check(ReplayQueuedTargetRecovery.Decide(request,queued with { CastingActionId=7411,CastRemaining=2 },202,true,100)==ReplayQueueRecovery.ForgetSent,
            "A live cast retains its committed target even if native queue flags are stale");
        Check(ReplayQueuedTargetRecovery.Decide(request,queued with { ActionQueued=false,QueuedActionId=0,QueuedTargetId=0 },202,true,100)==ReplayQueueRecovery.RetryUnsent,
            "A native cancellation with no outgoing sequence resumes the same cue immediately");
        Check(ReplayQueuedTargetRecovery.Decide(request,queued with { QueuedActionId=999,QueuedTargetId=303 },202,true,100)==ReplayQueueRecovery.RetryUnsent,
            "A player's replacement input is preserved while only the old replay pending marker is released");
        Check(ReplayQueuedTargetRecovery.Decide(request,queued with { QueuedActionType=2 },202,true,100)==ReplayQueueRecovery.RetryUnsent,
            "An item or another input type must never be rewritten as a replay attack");
        Check(ReplayQueuedTargetRecovery.Decide(request,queued with { GcdRemaining=0 },101,true,2700)==ReplayQueueRecovery.CancelOwnedQueue,
            "An idle native queue past its deadline cannot pin replay forever");
        Check(ReplayQueuedTargetRecovery.Decide(request,queued with { GcdRemaining=float.NaN },202,true,100)==ReplayQueueRecovery.None,
            "Uncertain native timing data must not mutate a queued input");

        var queue = new ReplayAdaptiveQueue(new[] { ("first",7411u,true),("second",7412u,true) });
        var feedback = new ReplayFeedbackLog();
        var ready = new ReplayNativeWindow(0,0,0,false);
        queue.Dispatch(ready,0,s=> {
            feedback.Requested(s.CueId,s.ActionId,"Boss A",0,0,0);
            return new(true,s.ActionId,NativeQueued:true,BeforeUseSequence:7);
        });
        // Native target changes do not issue UseAction, cancel a cast or move the timeline cursor.
        feedback.RetargetPending("first","Add B");
        Check(queue.CompletedCount==0 && queue.PendingGcd?.Step.CueId=="first"
            && feedback.LastRequest?.Target=="Add B" && feedback.LastConfirmed==null,
            "Retarget keeps one pending cue and reports B without faking a use");
        Check(!queue.Dispatch(ready,100,_=>throw new Exception("Duplicate retarget input")),
            "An accepted retargeted request must still wait for its real confirmation");
        var actual = queue.Confirm(7411)!; feedback.Confirmed(actual.CueId,7411,0.3f,300);
        Check(actual.CueId=="first" && feedback.LastConfirmed?.Target=="Add B" && queue.NextGcd?.CueId=="second",
            "The genuine B confirmation advances exactly one source record");

        var cancelled = new ReplayAdaptiveQueue(new[] { ("retry",7411u,true) });
        cancelled.Dispatch(ready,0,s=>new(true,s.ActionId,NativeQueued:true,BeforeUseSequence:7));
        feedback.Requested("retry",7411,"Boss A",0,0,0);
        Check(cancelled.CancelPending("retry"), "Release only the cancelled request marker");
        feedback.CancelPending("retry");
        Check(cancelled.CompletedCount==0 && cancelled.NextGcd?.CueId=="retry"
            && !feedback.MatchesRequest("retry",7411) && feedback.LastRequest==null,
            "A cancelled unsent input stays at the same cue with no false confirmation");
        Check(cancelled.Dispatch(ready,1,s=>new(true,s.ActionId)), "Retry the original waiting action on the selected B");
        Check(!cancelled.CancelPending("unrelated"), "Unrelated input state cannot cancel the active replay cue");

        var stuck = new ReplayAdaptiveQueue(new[]{("stuck",7411u,true)});
        stuck.Dispatch(ready,0,s=>new(true,s.ActionId,NativeQueued:true,BeforeUseSequence:7));
        stuck.Expire(1000,queued);
        Check(stuck.PendingGcd!=null, "A legitimate queued input retains its normal confirmation window");
        stuck.Expire(2700,queued);
        Check(stuck.PendingGcd!=null, "A running shared cooldown must finish before treating native input as stuck");
        stuck.Expire(2701,queued with { GcdRemaining=0 });
        Check(stuck.PendingGcd==null && stuck.CompletedCount==0,
            "The old unbounded same-action native queue wait is now bounded without consuming a cue");
        Check(stuck.Dispatch(ready,2702,s=>new(true,s.ActionId)), "After recovery the same skill can be requested for B");
        Console.WriteLine("Live boss-to-add target switches, owned queue retargeting, cancellation retries, committed casts and bounded waits checks passed.");
    }
}
