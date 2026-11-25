using Oracle.Services;

internal static class GcdReadinessChecks
{
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    public static void Run()
    {
        Check(Math.Abs(ReplayGcdReadiness.Remaining(2.5f,1.01f)-1.49f)<0.0001f,"Read native total/elapsed without a fixed raw recast slot");
        Check(ReplayGcdReadiness.Remaining(1.5f,1.5f)==0 && ReplayGcdReadiness.Remaining(2.5f,3)==0,
            "Completed and shortened recasts are ready");
        Check(float.IsNaN(ReplayGcdReadiness.Remaining(float.NaN,0))
            && float.IsNaN(ReplayGcdReadiness.Remaining(2.5f,-1)),"Invalid native timers do not silently become ready");
        var stale = new ReplayNativeWindow(1.49f,0,0,false,LastUsedSequence:31);
        Check(!stale.CanQueueGcd,"Reproduce the reported 1.49-second snapshot blocking Heated Slug Shot");
        var live = ReplayGcdReadiness.Reconcile(stale,0);
        Check(live.CanQueueGcd && live.GcdRemaining==0,"A full native ready check releases that exact waiting action");
        foreach(var status in new uint?[]{null,572,579,580,581,582,9999})
            Check(ReplayGcdReadiness.Reconcile(stale,status)==stale,"Unknown/blocked/cooling actions never receive a false ready proof: "+status);
        Check(ReplayGcdReadiness.Reconcile(stale with { CastRemaining=2 },0).CastRemaining==2
            && !ReplayGcdReadiness.Reconcile(stale with { CastRemaining=2 },0).CanQueueGcd,"Do not interrupt or overlap a real cast");
        Check(!ReplayGcdReadiness.Reconcile(stale with { AnimationLock=0.6f },0).CanQueueGcd,"Actual animation lock remains authoritative");
        Check(!ReplayGcdReadiness.Reconcile(stale with { CastRemaining=float.NaN },0).CanQueueGcd,
            "Uncertain cast timing is never overridden");
        var queueWindow=stale with { GcdRemaining=0.2f };
        Check(ReplayGcdReadiness.Reconcile(queueWindow,582)==queueWindow && queueWindow.CanQueueGcd,
            "Retain the native 0.4-second input queue window while recast is still active");

        // Exercise a real cursor at the reported 31/381 position. Native readiness
        // repairs scheduling only: it must neither skip nor confirm the source cue.
        foreach(var actionId in new uint[]{7412,54,16495,15989,141})
        {
            var queue=new ReplayAdaptiveQueue(Enumerable.Range(0,381).Select(i=>($"step-{i}",actionId,true)));
            for(var i=0;i<31;i++)
            {
                Check(queue.Dispatch(new(0,0,0,false),i,s=>new(true,s.ActionId)),"Build confirmed history before the stall");
                Check(queue.Confirm(actionId)?.CueId==$"step-{i}","Each history step has a real confirmation");
            }
            var feedback=new ReplayFeedbackLog();
            Check(!queue.Dispatch(stale,32000,_=>throw new Exception("Old snapshot should block")) && queue.CompletedCount==31,
                "Reproduce the frozen cursor without a target change");
            Check(queue.Dispatch(live,32001,s=>{
                feedback.Requested(s.CueId,s.ActionId,"Same selected boss",29.9f,29.9f,32001);
                return new(true,s.ActionId);
            }),"Native readiness retries the current source action without OFF/ON");
            Check(queue.PendingGcd?.Step.CueId=="step-31" && queue.CompletedCount==31
                && feedback.LastConfirmed==null && queue.CooldownSkippedCount==0 && queue.ConditionSkippedCount==0,
                "No skipped record, fake confirmation or opening restart is introduced");
            Check(!queue.Dispatch(live,32002,_=>throw new Exception("Duplicate pending GCD")),"Wait for the real use confirmation after the repaired request");
            var confirmed=queue.Confirm(actionId)!;
            feedback.Confirmed(confirmed.CueId,actionId,30.0f,32100);
            Check(confirmed.CueId=="step-31" && queue.CompletedCount==32 && queue.NextGcd?.CueId=="step-32",
                "One actual use advances exactly one source cue for every job");
        }

        var owned=new ReplayQueuedRequest("same-cue",7412,101,31,2600);
        var queued=stale with { ActionQueued=true,QueuedActionId=7412,QueuedActionType=1,QueuedTargetId=101 };
        var recovered=ReplayGcdReadiness.Reconcile(queued,0);
        Check(recovered.ActionQueued && !recovered.CanQueueGcd,"A readiness proof preserves an existing input queue");
        Check(ReplayQueuedTargetRecovery.Decide(owned,recovered,101,true,2700)==ReplayQueueRecovery.CancelOwnedQueue,
            "A proven-ready game does not leave an expired owned input pinned by a stale timer");
        Check(ReplayQueuedTargetRecovery.Decide(owned,recovered with { LastUsedSequence=32 },101,true,2700)==ReplayQueueRecovery.ForgetSent,
            "An already sent request is not cancelled or resent by the readiness repair");
        var hold=new ReplayTimelineHold();
        var origin=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);
        hold.SetHeld(true,origin.AddSeconds(29.9));
        var decision=ReplayClockHoldPolicy.Decide(true,29.9f,false,true,true,true,!live.CanQueueGcd,false);
        Check(!decision.Hold,"The same live readiness decision also releases the attack clock");
        var paused=hold.SetHeld(decision.Hold,origin.AddSeconds(34.9));
        Check(Math.Abs(paused.TotalSeconds-5)<0.0001,"Clock resumes at the same source position without jumping or restarting");
        Check(ReplayClockHoldPolicy.Decide(true,29.9f,false,true,true,false,false,false).BlockMainActions,
            "Targetless downtime still keeps the source attack waiting");
        Console.WriteLine("Reported 31/381 GCD stall, native readiness reconciliation, real cooldown/cast/queue waits and same-cue confirmation checks passed.");
    }
}
