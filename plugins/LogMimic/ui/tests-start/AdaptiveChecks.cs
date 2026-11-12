using Oracle.Services;

internal static class AdaptiveChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        var tactician = new ReplayActionTraits(22,0,true,false,false,false,false);
        Check(!tactician.IsGcd && tactician.UsesSelf(-1), "Tactician with FFLogs target -1 must resolve to the player");
        Check(tactician.UsesSelf(999), "A self-only raid mitigation must not look for a different log actor");
        var heatedSlug = new ReplayActionTraits(58,0,false,true,false,false,false);
        Check(heatedSlug.IsGcd && !heatedSlug.UsesSelf(-1), "Heated Slug Shot must remain a hostile GCD");
        Check(new ReplayActionTraits(10,58,false,true,false,false,false).IsGcd, "A separate cooldown does not remove a skill from the shared GCD lane");
        var heal = new ReplayActionTraits(58,0,true,false,true,true,false);
        Check(!heal.UsesSelf(42) && heal.UsesSelf(-1), "Retain an explicit ally target; use self only for absent heal target data");
        Check(!new ReplayActionTraits(22,0,true,false,false,false,true).UsesSelf(-1), "Do not turn ground placement into a self-only target");

        var ready = new ReplayNativeWindow(0,0,0,false);
        Check(new ReplayNativeWindow(0.39f,0,0,false).CanQueueGcd, "Queue the next GCD before the current recast ends");
        Check(!new ReplayNativeWindow(0.5f,0,0,false).CanQueueGcd, "Do not queue a GCD outside the native input window");
        Check(new ReplayNativeWindow(0.2f,0.3f,0,false).CanQueueGcd, "The final part of a cast can use the native GCD input queue");
        Check(!new ReplayNativeWindow(0,0,0,true).CanQueueGcd, "Never overwrite an existing queued input");
        Check(!new ReplayNativeWindow(float.NaN,0,0,false).CanQueueGcd, "Invalid timer data must not dispatch actions");
        Check(new ReplayNativeWindow(0.9f,0,0,false).CanWeave(), "A fast 1.5s GCD still has a single-weave window after its lock ends");
        Check(!new ReplayNativeWindow(0.5f,0,0,false).CanWeave(), "Do not force a second weave that would delay the GCD");
        Check(!new ReplayNativeWindow(1,0,0,false).CanWeave(false,1.1f), "Observed long animation locks need a larger weave window");
        Check(!new ReplayNativeWindow(2,0.1f,0,false).CanWeave(), "Do not weave while casting");
        Check(!new ReplayNativeWindow(2,0,0.1f,false).CanWeave(), "Respect the native animation lock");
        Check(ready.CanWeave(true) && !ready.CanWeave(), "Allow a leading buff without making ordinary weaves delay a ready GCD");

        var queue = new ReplayAdaptiveQueue(new (string,uint,bool)[] {
            ("split",7411,true), ("tactician",16889,false), ("slug",7412,true),
            ("doublecheck",36979,false), ("clean",7413,true)
        });
        Check(queue.NextOgcd == null, "Do not release a mitigation before its preceding GCD");
        Check(queue.Confirm(7411) == null, "An unsolicited event must not consume an unrequested GCD");
        Check(queue.Dispatch(ready,0, s=>new(true,s.ActionId)), "First GCD must dispatch without waiting for logged timestamps");
        Check(queue.PendingGcd?.Step.CueId == "split" && queue.NextOgcd == null, "An accepted request must still wait for actual confirmation");
        Check(queue.Confirm(7411)?.CueId == "split", "Confirm the requested opening shot");
        Check(queue.NextOgcd?.CueId == "tactician", "Release the raid mitigation after its preceding GCD");

        var blocked = new List<string>();
        Check(!queue.Dispatch(new(0.9f,0,0,false),600,s=>{blocked.Add(s.CueId);return new(false,TargetBlocked:true);}), "Unavailable mitigation must remain pending rather than pretending it fired");
        Check(blocked.SequenceEqual(new[]{"tactician"}), "Future abilities must not be pulled forward across their GCD barrier");
        // The source log can have a 2.5s gap. The local recast reaches its queue window sooner.
        Check(queue.Dispatch(new(0.39f,0,0,false),1610,s=>new(true,s.ActionId)), "A blocked Tactician must not stop Heated Slug Shot on a faster local GCD");
        Check(queue.PendingGcd?.Step.CueId == "slug", "Keep GCD order while bypassing an unavailable ability");
        Check(queue.Confirm(7412)?.CueId == "slug", "The second combo shot must advance on its real use");
        var attempted = new List<string>();
        Check(queue.Dispatch(new(0.9f,0,0,false),2600,s=>{
            attempted.Add(s.CueId);return s.CueId=="tactician" ? new(false) : new(true,s.ActionId);
        }), "An unavailable mitigation must not block another released damage ability");
        Check(attempted.SequenceEqual(new[]{"tactician","doublecheck"}), "Try released abilities in recorded priority order");
        Check(queue.Dispatch(new(0.39f,0,0,false),3610,s=>new(true,s.ActionId)), "A late ability confirmation must not stall the next GCD");
        Check(queue.PendingGcd?.Step.CueId=="clean" && queue.PendingOgcd?.Step.CueId=="doublecheck", "Track GCD and ability acknowledgements independently");
        queue.Confirm(36979);queue.Confirm(7413);
        Check(!queue.Finished && queue.NextOgcd?.CueId=="tactician", "Keep the deferred mitigation available for a later legal window");
        Check(queue.Dispatch(ready,5000,s=>new(true,s.ActionId)), "Retry a deferred ability when it becomes usable");
        queue.Confirm(16889);Check(queue.Finished && queue.CompletedCount==5, "Every confirmed action must be counted once");

        var adjusted = new ReplayAdaptiveQueue(new[]{("upgrade",2868u,true)});
        adjusted.Dispatch(ready,0,_=>new(true,7412));
        Check(adjusted.Confirm(2868)==null && adjusted.Confirm(7412)?.CueId=="upgrade", "Match the ID captured before use, not an ID adjusted again after a combo changes");
        var longCast = new ReplayAdaptiveQueue(new[]{("cast",100u,true)});
        longCast.Dispatch(ready,0,_=>new(true,100,7000));
        longCast.Expire(8000,new(0,1,0,false,CastingActionId:100));
        Check(longCast.PendingGcd!=null, "Do not retry a native cast still in progress");
        longCast.Expire(8001,ready);
        Check(longCast.PendingGcd==null && longCast.CompletedCount==0, "Expiry permits a retry without falsely completing the cast");
        var missingTarget = new ReplayAdaptiveQueue(new[]{("first",99u,true),("buff",200u,false),("gcd",100u,true)});
        missingTarget.Dispatch(ready,0,s=>new(true,s.ActionId));missingTarget.Confirm(99);
        var seen = new List<uint>();
        missingTarget.Dispatch(ready,0,s=>{seen.Add(s.ActionId);return new(false,TargetBlocked:true);});
        Check(seen.SequenceEqual(new[]{100u}), "Do not burn idle self buffs while the next enemy-target GCD has no valid target");

        var inbox = new ReplayConfirmationQueue();
        inbox.Enqueue(7412,10); inbox.Enqueue(7412,10); inbox.Enqueue(7412,11); inbox.Enqueue(16889,0);
        Check(inbox.TryDequeue(out var first) && first==7412 && inbox.TryDequeue(out var second) && second==7412
            && !inbox.TryDequeue(out _), "Two real uses of the same action count twice; repeated packets and server-only effects do not");
        inbox.Enqueue(9999,12);inbox.Enqueue(7412,12);
        Check(inbox.TryDequeue(out var secondary) && secondary==9999 && inbox.TryDequeue(out var primary) && primary==7412,
            "A secondary effect with the same sequence must not hide the requested primary action");
        var feedback = new ReplayFeedbackLog();
        feedback.Requested("snapshot",7412,"Boss",0,0,0);
        Check(feedback.MatchesRequest("snapshot",7412) && !feedback.MatchesRequest("snapshot",7413), "Legacy and independent lanes also use the captured requested ID");
        var phases = new ReplayPhasePlan(new[] {
            new ReplayPhaseMark("p1",0,2.5f), new ReplayPhaseMark("p1b",2.5f,2.5f),
            new ReplayPhaseMark("p2buff",20,0), new ReplayPhaseMark("p2",20.6f,2.5f)
        },new[]{new ReplayPhaseAnchor("phase-signal",18)});
        Check(phases.Boundaries.Count==1 && phases.Boundaries[0].At==20
            && phases.Boundaries[0].SyncCueId=="phase-signal", "Retain a long phase gap and its configured encounter sync");
        Check(phases.Segments["p1b"]==0 && phases.Segments["p2buff"]==1, "Phase-two preparation must not be pulled into phase one");
        Check(!phases.Boundaries[0].CanRelease(100,_=>false), "Passing the logged time must not substitute for a configured phase signal");
        Check(!phases.Boundaries[0].CanRelease(18,_=>true) && phases.Boundaries[0].CanRelease(20,_=>true), "Retain the delay between a phase signal and its recorded follow-up action");
        Check(new ReplayPhaseBoundary(1,20,null).CanRelease(20,_=>false), "Without a sync cue, retain the original timeline gate");
        var phasedQueue = new ReplayAdaptiveQueue(new (string,uint,bool,int)[] {
            ("old",100,true,0),("prep",200,false,1),("next",300,true,1)
        });
        phasedQueue.Dispatch(ready,0,s=>new(true,s.ActionId));phasedQueue.Confirm(100);
        Check(!phasedQueue.Dispatch(ready,1000,_=>throw new Exception("Phase-two action executed early")),
            "A faster local GCD must not erase the phase transition wait");
        phasedQueue.ReleasePhase(1);
        Check(phasedQueue.NextGcd?.CueId=="next" && phasedQueue.NextOgcd?.CueId=="prep", "Release phase-two actions only when the phase gate opens");
        var resync = new ReplayAdaptiveQueue(new (string,uint,bool,int)[] {("late",1,true,0),("new",2,true,1)});
        resync.ReleasePhase(1,true);
        Check(resync.NextGcd?.CueId=="new" && resync.CompletedCount==1, "An observed phase sync can discard unrequested actions from the old phase");
        var longSpell = new ReplayPhasePlan(new[]{new ReplayPhaseMark("long",0,8),new ReplayPhaseMark("after",8,2.5f)},Array.Empty<ReplayPhaseAnchor>());
        Check(longSpell.Boundaries.Count==0, "A long spell's normal cast time is not a phase transition");
        Check(!phasedQueue.Dispatch(new(1.8f,0,0,false),0,_=>throw new Exception("Buff used with missing target"),targetAvailable:false),
            "Untargetable downtime must also block self buffs between GCDs");
        // Monk has short trait-adjusted recasts and both charged buffs and gauge abilities.
        var balance = new ReplayActionTraits(14,71,true,false,false,false,false);
        Check(!balance.IsGcd && balance.UsesSelf(-1), "Perfect Balance is a self-targeted ability, not a GCD");
        var monk = new ReplayAdaptiveQueue(new (string,uint,bool)[] {
            ("opo",53,true),("chakra",3547,false),("balance",69,false),("raptor",54,true),("coeurl",56,true)
        });
        monk.Dispatch(ready,0,_=>new(true,36945));monk.Confirm(36945);
        var monkAttempts = new List<string>();
        Check(monk.Dispatch(new(1.3f,0,0,false),600,s=>{
            monkAttempts.Add(s.CueId);return s.CueId=="chakra" ? new(false) : new(true,s.ActionId);
        }), "Insufficient chakra must not prevent a usable Monk buff from weaving");
        Check(monkAttempts.SequenceEqual(new[]{"chakra","balance"}), "Monk abilities retain priority without being blocked by gauge requirements");
        monk.Confirm(69);
        Check(monk.Dispatch(new(0.35f,0,0,false),1590,_=>new(true,36946)), "Queue the next Monk combo on its local short GCD");
        Check(monk.Confirm(36946)?.CueId=="raptor" && monk.NextGcd?.CueId=="coeurl", "An upgraded Monk action must advance the intended combo step");
        Check(new ReplayActionTraits(7,58,true,false,false,false,false).IsGcd, "Dancer Standard Step uses the additional global recast group");
        Check(new ReplayActionTraits(5,58,false,true,false,false,false).IsGcd, "Machinist Drill remains a GCD despite having its own cooldown");
        var cancelled = new ReplayAdaptiveQueue(new[]{("queued",100u,true)});
        cancelled.Dispatch(ready,0,_=>new(true,100,NativeQueued:true,BeforeUseSequence:50));
        cancelled.Expire(900,new(0,0,0,false,LastUsedSequence:50));
        Check(cancelled.PendingGcd==null && cancelled.CompletedCount==0, "A cancelled native queue must retry, not stall for a confirmation that cannot arrive");
        var fired = new ReplayAdaptiveQueue(new[]{("queued",100u,true)});
        fired.Dispatch(ready,0,_=>new(true,100,NativeQueued:true,BeforeUseSequence:50));
        fired.Expire(900,new(1.2f,0,0,false,LastUsedSequence:51));
        Check(fired.PendingGcd!=null, "A queue that actually fired must still await its real server confirmation");
        var interrupted = new ReplayAdaptiveQueue(new[]{("spell",100u,true)});
        interrupted.Dispatch(ready,0,_=>new(true,100,6000));
        interrupted.Expire(1000,new(0,3,0,false,CastingActionId:100));
        interrupted.Expire(2000,ready);
        Check(interrupted.PendingGcd==null && interrupted.CompletedCount==0, "An interrupted cast must retry instead of waiting for a completion event that cannot arrive");
        var completedCast = new ReplayAdaptiveQueue(new[]{("spell",100u,true)});
        completedCast.Dispatch(ready,0,_=>new(true,100,6000));
        completedCast.Expire(1000,new(0,0.5f,0,false,CastingActionId:100));
        completedCast.Expire(1600,ready);
        Check(completedCast.PendingGcd!=null, "A naturally completed cast must still wait for its real acknowledgement");
        Console.WriteLine("Adaptive MCH combo, target -1, deferred mitigation, faster GCD, native queue/weave and confirmation regressions passed.");
        Console.WriteLine("Phase gaps, encounter sync gates, phase-specific abilities and untargetable downtime checks passed.");
        Console.WriteLine("Monk short-GCD combo, charged buff, chakra wait, action upgrades and native queue cancellation checks passed.");
    }
}
