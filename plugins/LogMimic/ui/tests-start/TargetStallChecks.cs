using Oracle.Services;

internal static class TargetStallChecks
{
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    public static void Run()
    {
        var ready = new ReplayNativeWindow(0,0,0,false,LastUsedSequence:7);
        var conditions = new ReplayActionCondition();
        Check(!conditions.ShouldSkip("heat",16497,101,572,0),"Give a just-used Hypercharge/proc time to arrive");
        Check(!conditions.ShouldSkip("heat",16497,101,572,599),"Do not skip during the preparation grace interval");
        Check(!conditions.ShouldSkip("heat",16497,101,0,600),"An arriving buff clears the wait and uses the recorded action normally");
        Check(!conditions.ShouldSkip("heat",16497,101,572,1000),"A newly lost requirement starts a fresh grace interval");
        Check(conditions.ShouldSkip("heat",16497,101,572,1600),"A lost gauge/proc cannot pin the attack cursor forever");
        foreach(var status in new uint[]{0,567,568,573,574,579,580,581,582,9999})
        {
            conditions.ShouldSkip("wait",7411,101,status,2000);
            Check(!conditions.ShouldSkip("wait",7411,101,status,3000),"Target restrictions, silence, cooldowns, MP and unknown errors are not missing-proc skips: "+status);
        }
        conditions.ShouldSkip("switch",16497,101,572,4000);
        Check(!conditions.ShouldSkip("switch",16497,202,572,4600),"A new selected target gets its own live requirement observation");
        conditions.Reset();
        Check(!conditions.ShouldSkip("switch",16497,202,572,5200),"Target loss, normal busy windows, pause and fresh sessions clear stale waits");
        Check(!conditions.ShouldSkip("switch",16497,202,572,8000),"A long gap between observations cannot discard an action immediately");
        conditions.ShouldSkip("main",16497,101,572,9000);
        conditions.Forget("unrelated-ability");
        Check(conditions.ShouldSkip("main",16497,101,572,9600),"An unrelated unavailable ability cannot reset the main attack's recovery indefinitely");

        // Both role assignments use the player's current selection. The source
        // record can name the add or boss; it never participates in dispatch.
        foreach(var selectedId in new ulong[]{101,202})
        {
            var chosen=ReplayManualTarget.Choose(new(58,0,false,true,false,false,false),999,
                new ReplayManualObject(selectedId,false,true));
            Check(chosen.ObjectId==selectedId,"Opposite log assignment keeps the manually selected boss/add");
            var queue=new ReplayAdaptiveQueue(new[]{("expired-burst",16497u,true),("normal-shot",7411u,true)});
            var log=new ReplayFeedbackLog();
            var wait=new ReplayActionCondition();
            ReplayAttemptOutcome Attempt(ReplayAdaptiveStep step,long now)
            {
                var status=step.ActionId==16497 ? 572u : 0;
                if(wait.ShouldSkip(step.CueId,step.ActionId,selectedId,status,now)) return new(false,SkipCondition:true);
                if(status!=0) return new(false);
                log.Requested(step.CueId,step.ActionId,selectedId.ToString(),0,0,now);
                return new(true,step.ActionId);
            }
            Check(!queue.Dispatch(ready,0,s=>Attempt(s,0)) && queue.CompletedCount==0,"Missing burst requirement initially waits on the current target");
            Check(queue.Dispatch(ready,600,s=>Attempt(s,600)) && queue.PendingGcd?.Step.CueId=="normal-shot",
                "After the grace period, the next usable shot runs on either role target without a frozen-timeline timeout");
            Check(queue.ConditionSkippedCount==1 && queue.CooldownSkippedCount==0 && log.LastConfirmed==null,
                "A missing-condition skip is distinct from cooldowns and never claims that the skill fired");
            Check(queue.Confirm(16497)==null && queue.Confirm(7411)?.CueId=="normal-shot" && queue.Finished,
                "Only the actual next shot advances its source cue");
        }

        var nativeQueued=ready with { ActionQueued=true,QueuedActionId=7411,QueuedActionType=1,QueuedTargetId=101 };
        Check(ReplayNativeRequest.Accepted(false,ready,nativeQueued,7411,101),
            "A proven newly created native queue must be tracked even without immediate execution success");
        Check(!ReplayNativeRequest.Accepted(false,nativeQueued,nativeQueued,7411,101),"A pre-existing input does not establish replay ownership");
        Check(!ReplayNativeRequest.Accepted(false,ready,nativeQueued with { QueuedActionId=7412 },7411,101),"A different queued action is not adopted");
        Check(!ReplayNativeRequest.Accepted(false,ready,nativeQueued with { QueuedTargetId=202 },7411,101),"An unrelated target is not adopted");
        Check(!ReplayNativeRequest.Accepted(false,ready,nativeQueued with { QueuedActionType=2 },7411,101),"An item queue is not an attack request");
        Check(!ReplayNativeRequest.Accepted(false,ready,nativeQueued with { LastUsedSequence=8 },7411,101),"Uncertain outgoing ownership is not guessed");
        Check(ReplayNativeRequest.Accepted(true,ready,ready with { LastUsedSequence=8 },7411,101),"An immediately executed native request retains its normal confirmation path");
        var pending=new ReplayAdaptiveQueue(new[]{("role-change",7411u,true)});
        Check(pending.Dispatch(ready,0,s=>new(ReplayNativeRequest.Accepted(false,ready,nativeQueued,s.ActionId,101),s.ActionId,
            NativeQueued:true,BeforeUseSequence:7)),"Register exactly one pending record for the newly queued input");
        var owned=new ReplayQueuedRequest("role-change",7411,101,7,2600);
        Check(ReplayQueuedTargetRecovery.Decide(owned,nativeQueued,202,true,100)==ReplayQueueRecovery.Retarget,
            "Role assignment change can now recover the tracked unsent attack onto the selected boss/add");
        Check(!pending.Dispatch(nativeQueued,100,_=>throw new Exception("Duplicate native request")),"Retargeting does not create duplicate requests");
        Check(pending.Confirm(7411)?.CueId=="role-change" && pending.Finished,"A real confirmation completes the recovered request once");

        var phased=new ReplayAdaptiveQueue(new[]{("old",16497u,true,0),("next-phase",7411u,true,1)});
        Check(!phased.Dispatch(ready,0,_=>new(false,SkipCondition:true)) && phased.NextGcd==null,
            "Condition skips cannot bypass an explicit phase signal");
        var targetless=new ReplayAdaptiveQueue(new[]{("wait-target",16497u,true)});
        Check(!targetless.Dispatch(ready,0,_=>throw new Exception("Targetless condition skip"),targetAvailable:false)
            && targetless.CompletedCount==0,"Missing targets preserve the waiting attack");
        Console.WriteLine("Opposite boss/add assignments, expired-requirement recovery, transient buffs, exact native queue acceptance and real-confirmation checks passed.");
    }
}
