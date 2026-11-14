using Oracle.Services;

internal static class ProcPairChecks
{
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    public static void Run()
    {
        uint HighLevel(uint id) => id switch { 97=>16495, 98=>7409, _=>id };
        foreach (var pair in ReplayProcPairs.Pairs)
        {
            var basic = HighLevel(pair.BaseRoot);
            var proc = HighLevel(pair.ProcRoot);
            foreach (var logged in new[] { basic, proc })
            {
                var none = ReplayProcPairs.Select(pair.JobId,logged,logged,HighLevel,_=>false,_=>true);
                Check(none==basic, $"{logged}: absent live proc must use the paired basic action");
                foreach (var status in pair.ReadyStatuses)
                {
                    var ready = ReplayProcPairs.Select(pair.JobId,logged,logged,HighLevel,id=>id==status,_=>true);
                    Check(ready==proc, $"{logged}: live status {status} must select the proc regardless of the source log");
                }
                var locked = ReplayProcPairs.Select(pair.JobId,logged,logged,HighLevel,_=>true,_=>false);
                Check(locked==basic, "An unlearned proc must fall back to its basic action");
            }
        }
        Check(ReplayProcPairs.Pairs.Count==5, "Limit substitutions to the five requested pairs");
        Check(ReplayProcPairs.Select(38,15989,15989,HighLevel,id=>id==2694,_=>true)==15989,
            "A Flow proc must not enable Reverse Cascade");
        Check(ReplayProcPairs.Select(38,15990,15990,HighLevel,id=>id==2693,_=>true)==15990,
            "A Symmetry proc must not enable Fountainfall");
        Check(ReplayProcPairs.Select(38,15993,15993,HighLevel,id=>id==1814,_=>true)==15993,
            "Older client single-target proc buffs must not enable a different old AoE proc");
        Check(ReplayProcPairs.Select(23,16495,16495,HighLevel,id=>id==128,_=>true)==7409,
            "Barrage is a valid native Refulgent Arrow condition");
        Check(ReplayProcPairs.Select(23,7409,7409,id=>id,_=>false,_=>true)==97,
            "A lower-level synced Bard can fall back through the native base-action root");
        Check(ReplayProcPairs.Select(23,16495,16495,id=>id,id=>id==122,_=>true)==98,
            "A lower-level Bard proc uses Straight Shot rather than an unlearned upgrade");
        Check(ReplayProcPairs.Select(23,97,16495,HighLevel,id=>id==3861,_=>true)==7409,
            "Native level upgrades and older source-log IDs stay in the same family");
        Check(ReplayProcPairs.Select(20,15989,15989,_=>throw new Exception("Wrong-job native query"),_=>true,_=>true)==15989,
            "Other jobs must not enter proc substitution");
        Check(ReplayProcPairs.Select(38,15997,16003,_=>throw new Exception("Dance query"),_=>true,_=>true)==16003,
            "Standard Step and other unrelated native actions remain unchanged");
        Check(ReplayProcPairs.Select(38,15989,15999,_=>throw new Exception("Dancing remap"),_=>true,_=>true)==15999,
            "Do not overwrite a native dance-step transformation");
        Check(ReplayProcPairs.Select(38,15991,15991,id=>id==15989 ? 15999u : id,_=>false,_=>true)==15991,
            "A proc fallback must not silently press an unrelated dance step");
        Check(ReplayProcPairs.Select(38,15989,15989,HighLevel,_=>true,_=>true,new uint[]{15991})==15989,
            "Explicitly excluded proc variants must not be introduced by substitution");
        Check(ReplayProcPairs.Select(38,15991,15991,HighLevel,_=>false,_=>true,new uint[]{15989})==15991,
            "Explicitly excluded basic variants keep their existing exclusion behavior");
        Check(!ReplayProcPairs.IsLiveStatus(2693,0) && !ReplayProcPairs.IsLiveStatus(2693,-1)
            && !ReplayProcPairs.IsLiveStatus(2693,float.NaN) && ReplayProcPairs.IsLiveStatus(2693,0.1f),
            "Expired or invalid proc statuses must not enable a proc action");

        var readyWindow = new ReplayNativeWindow(0,0,0,false);
        var queue = new ReplayAdaptiveQueue(new[] { ("base-record",15989u,true),("proc-record",15991u,true) });
        var feedback = new ReplayFeedbackLog();
        var liveProc = true;
        ReplayAttemptOutcome Attempt(ReplayAdaptiveStep step)
        {
            var selected = ReplayProcPairs.Select(38,step.ActionId,step.ActionId,HighLevel,id=>liveProc && id==2693,_=>true);
            feedback.Requested(step.CueId,selected,"Selected target",0,0,0);
            return new(true,selected);
        }
        Check(!queue.Dispatch(readyWindow,0,_=>throw new Exception("Targetless proc request"),targetAvailable:false),
            "No target still pauses the exact waiting cue");
        Check(queue.Dispatch(readyWindow,1,Attempt) && queue.PendingGcd?.ActualActionId==15991,
            "A logged basic records the actual substituted proc request");
        Check(queue.Confirm(15989)==null && feedback.LastConfirmed==null,
            "The recorded base ID must not fake a proc-use confirmation");
        var first = queue.Confirm(15991)!;
        feedback.Confirmed(first.CueId,15991,1,1);
        Check(first.CueId=="base-record" && queue.NextGcd?.CueId=="proc-record"
            && feedback.LastConfirmed?.ActionId==15991, "One proc confirms exactly the original cue and leaves the next source cue untouched");
        liveProc = false;
        Check(queue.Dispatch(readyWindow,2,Attempt) && queue.PendingGcd?.ActualActionId==15989,
            "The next logged proc falls back to a basic when the live proc is gone");
        Check(feedback.MatchesRequest("proc-record",15989) && !feedback.MatchesRequest("proc-record",15991),
            "Independent/legacy confirmation matching also uses the actual substituted ID");
        var second = queue.Confirm(15989)!; feedback.Confirmed(second.CueId,15989,2,2);
        Check(second.CueId=="proc-record" && queue.Finished && feedback.History.Count==2,
            "Substitution consumes one source record per real use without adding timeline steps");

        var cancelled = new ReplayAdaptiveQueue(new[]{("retry",15989u,true)});
        liveProc = true;
        cancelled.Dispatch(readyWindow,0,_=>new(true,15991,NativeQueued:true,BeforeUseSequence:10));
        cancelled.Expire(900,new(0,0,0,false,LastUsedSequence:10)); liveProc=false;
        Check(cancelled.PendingGcd==null && cancelled.Dispatch(readyWindow,901,Attempt)
            && cancelled.PendingGcd?.ActualActionId==15989,
            "A cancelled input rechecks current procs before retrying the same source record");
        Console.WriteLine("Five DNC/BRD proc pairs, both log directions, status expiry, level upgrades, dance/exclusion guards and actual-ID confirmations checks passed.");
    }
}
