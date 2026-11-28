using Oracle.Services;

internal static class AllInputQueueChecks
{
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    public static void Run()
    {
        var before=new ReplayNativeWindow(1.49f,0,0,false,LastUsedSequence:7);
        var self=new ReplayActionTraits(18,73,true,false,false,false,false);
        var shot=new ReplayActionTraits(58,0,false,true,false,false,false);
        var after=before with { ActionQueued=true,QueuedActionId=2876,QueuedActionType=1,QueuedTargetId=999 };
        var buff=ReplayQueuedTargetRecovery.Capture("reassemble",2876,999,self,before,after,2600);
        Check(buff is { FollowSelection:false,TargetId:999 },"Reassemble's self-only input now has ownership; it must not be dropped by a hostile-only filter");
        var attack=ReplayQueuedTargetRecovery.Capture("shot",7412,101,shot,before,
            after with { QueuedActionId=7412,QueuedTargetId=101 },2600);
        Check(attack is { FollowSelection:true },"Attack queues still follow the player's selected enemy");
        var ally=ReplayQueuedTargetRecovery.Capture("ally",100,222,self with { Self=false,Party=true },before,
            after with { QueuedActionId=100,QueuedTargetId=222 },2600);
        Check(ally is { FollowSelection:false,TargetId:222 },"An ally queue retains its committed ally target");
        Check(ReplayQueuedTargetRecovery.Capture("x",2876,999,self,after,after,2600)==null,
            "Never adopt a pre-existing native input as replay-owned");
        Check(ReplayQueuedTargetRecovery.Capture("x",2876,999,self,before,after with { QueuedActionType=2 },2600)==null
            && ReplayQueuedTargetRecovery.Capture("x",2876,999,self,before,after with { QueuedActionId=7412 },2600)==null
            && ReplayQueuedTargetRecovery.Capture("x",2876,999,self,before,after with { QueuedTargetId=101 },2600)==null,
            "Unrelated type, action or target never establishes ownership");
        Check(ReplayQueuedTargetRecovery.Capture("x",2876,999,self,before,after with { LastUsedSequence=8 },2600)==null,
            "A request already sent is never mistaken for an unexecuted queue");
        Check(ReplayQueuedTargetRecovery.Decide(buff!,after,999,true,2500)==ReplayQueueRecovery.None,
            "A normal self queue inside its deadline stays intact");
        Check(ReplayQueuedTargetRecovery.Decide(buff!,after,999,true,3601)==ReplayQueueRecovery.CancelOwnedQueue,
            "A stale 1.49-second GCD snapshot cannot pin an owned self input indefinitely");
        Check(ReplayQueuedTargetRecovery.Decide(buff!,after,101,true,100)==ReplayQueueRecovery.CancelOwnedQueue,
            "A self-only input is never retargeted onto the selected boss");
        Check(ReplayQueuedTargetRecovery.Decide(buff!,after with { QueuedTargetId=222 },222,true,4000)==ReplayQueueRecovery.RetryUnsent,
            "A player's replacement ally input is not cancelled or redirected");
        Check(ReplayQueuedTargetRecovery.Decide(buff!,after,999,true,2700,cuePending:false)==ReplayQueueRecovery.CancelOwnedQueue,
            "Keep queue ownership long enough to clean up an expired pending marker");
        Check(ReplayQueuedTargetRecovery.Decide(buff!,after with { CastingActionId=2876,CastRemaining=2 },999,true,4000)==ReplayQueueRecovery.ForgetSent,
            "Never interrupt a cast that has already started");
        Check(ReplayQueuedTargetRecovery.Decide(buff!,after with { CastingActionId=123,CastRemaining=2 },999,true,4000)==ReplayQueueRecovery.None,
            "Do not clear a normal queue while another live cast is finishing");
        Check(ReplayQueuedTargetRecovery.Decide(buff!,after with { LastUsedSequence=8 },999,true,4000)==ReplayQueueRecovery.ForgetSent,
            "Sent requests retain their normal confirmation path");

        // Full MCH queue handoff: first shot confirms, Reassemble queues on self,
        // the native queue sticks with a 1.49s timer, then recovery retries that
        // exact buff and continues to Heated Slug Shot without manual input.
        var queue=new ReplayAdaptiveQueue(new[]{("split",7411u,true),("reassemble",2876u,false),("slug",7412u,true)});
        var feedback=new ReplayFeedbackLog();
        queue.Dispatch(new(0,0,0,false),0,s=>new(true,s.ActionId));
        Check(queue.Confirm(7411)?.CueId=="split","The first shot is genuinely confirmed");
        Check(queue.Dispatch(before,1000,s=>{
            feedback.Requested(s.CueId,s.ActionId,"Self",1,1,1000);
            return new(true,s.ActionId,NativeQueued:true,BeforeUseSequence:7);
        }) && queue.PendingOgcd?.Step.CueId=="reassemble","Capture the self input that blocks the next GCD");
        var owned=ReplayQueuedTargetRecovery.Capture("reassemble",2876,999,self,before,after,3600)!;
        Check(!queue.Dispatch(after,3000,_=>throw new Exception("Duplicate queued buff")),"A genuine pending input is not spammed");
        Check(ReplayQueuedTargetRecovery.Decide(owned,after,999,true,4601)==ReplayQueueRecovery.CancelOwnedQueue,
            "Recover the same-target self queue even when its GCD value does not decrease");
        Check(queue.CancelPending(owned.CueId),"Only the unsent buff's pending marker is released");
        feedback.CancelPending(owned.CueId);
        Check(queue.CompletedCount==1 && queue.NextGcd?.CueId=="slug" && feedback.LastConfirmed==null,
            "Recovery neither restarts the opener nor fabricates a use");
        Check(queue.Dispatch(before,4602,s=>new(true,s.ActionId)) && queue.PendingOgcd?.Step.CueId=="reassemble",
            "Retry the original buff once the owned native queue is released");
        Check(queue.Confirm(2876)?.CueId=="reassemble","The retried buff needs a real server confirmation");
        Check(queue.Dispatch(new(0,0,0,false),4700,s=>new(true,s.ActionId)) && queue.PendingGcd?.Step.CueId=="slug",
            "Heated Slug Shot continues automatically after the self input recovers");
        Check(queue.Confirm(7412)?.CueId=="slug" && queue.Finished
            && queue.CooldownSkippedCount==0 && queue.ConditionSkippedCount==0,"The full sequence completes without skipped source actions");

        var expire=new ReplayAdaptiveQueue(new[]{("buff",2876u,false)});
        expire.Dispatch(new(0,0,0,false),0,s=>new(true,s.ActionId,NativeQueued:true,BeforeUseSequence:7));
        expire.Expire(3601,after);
        Check(expire.PendingOgcd==null && expire.CompletedCount==0,"Pending expiry is bounded even with a stuck native cooldown value");
        var history=new ReplayStallHistory();
        string? report=null;
        for(var i=0;i<=16;i++) report=history.Observe("slug",i*250,true,"queue=2876 target=self gcd=1.49");
        Check(report!=null && history.LastSummary.Contains("2876"),"Capture a stall automatically without a mid-combat command");
        var retained=history.LastSummary;
        history.Observe("next",5000,true,"ready");
        history.Observe("",5100,false,"");
        Check(history.LastSummary==retained,"Manual recovery, a new cue and combat end do not erase the last report");
        var restored=new ReplayStallHistory();
        restored.RestoreSummary(retained); restored.ResetActive();
        Check(restored.LastSummary==retained,"A saved report remains available after a plugin restart");
        for(var i=0;i<100;i++) history.Observe("bounded",6000+i*250,true,new string('x',10000));
        var bounded=history.Capture("test",32000,"last");
        Check(bounded.Length<31000 && bounded.Split('\n').Length<=27,"Diagnostic history is sampled, bounded and retained locally");
        Console.WriteLine("MCH self-input queue recovery, fixed ally targets, stale timers, real confirmations and retained stall evidence checks passed.");
    }
}
