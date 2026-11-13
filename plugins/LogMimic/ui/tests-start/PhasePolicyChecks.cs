using Oracle.Models;
using Oracle.Services;

internal static class PhasePolicyChecks
{
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); }

    public static void Run()
    {
        var marks = new[] {
            new ReplayPhaseMark("adds-a",0,2.5f), new ReplayPhaseMark("adds-b",2.5f,2.5f),
            new ReplayPhaseMark("boss",20,2.5f), new ReplayPhaseMark("boss-b",22.5f,2.5f),
        };
        var anchors = new[] { new ReplayPhaseAnchor("phase-signal",18,true) };
        var continuation = new ReplayPhaseSession(ReplayPhaseMode.ContinueSequence, marks, anchors);
        Check(continuation.Plan.Boundaries.Count == 0 && continuation.Plan.Segments.Values.All(s => s == 0),
            "Continuation must preserve action order across long log gaps and phase markers");
        Check(continuation.Observe(_ => true) == null && continuation.CurrentSegment == 0,
            "An observed boss phase must not discard waiting add actions in continuation mode");
        var ready = new ReplayNativeWindow(0,0,0,false);
        var continuousQueue = new ReplayAdaptiveQueue(marks.Select((m,i) => (m.CueId,(uint)i+100,true,continuation.Plan.Segments[m.CueId])));
        continuousQueue.Dispatch(ready,0,s => new(true,s.ActionId)); continuousQueue.Confirm(100);
        Check(!continuousQueue.Dispatch(ready,100000,_ => throw new Exception("No target attack"),targetAvailable:false)
            && continuousQueue.NextGcd?.CueId == "adds-b", "Waiting without a target never advances the continuation cursor");
        Check(continuousQueue.Dispatch(ready,100001,s => new(true,s.ActionId))
            && continuousQueue.PendingGcd?.Step.CueId == "adds-b", "A new selected target resumes the exact waiting add action");

        foreach (var actualSignalTime in new[] { 8f, 80f })
        {
            var align = new ReplayPhaseSession(ReplayPhaseMode.AlignToPhase, marks, anchors);
            Check(align.Observe(_ => false) == null && align.CurrentSegment == 0,
                "Passing a recorded timestamp or changing target never substitutes for a phase signal");
            var queue = new ReplayAdaptiveQueue(marks.Select((m,i) => (m.CueId,(uint)i+100,true,align.Plan.Segments[m.CueId])));
            queue.Dispatch(ready,0,s => new(true,s.ActionId)); queue.Confirm(100);
            var boundary = align.Observe(id => id == "phase-signal")!;
            queue.ReleasePhase(boundary.Segment,discardEarlier:true);
            Check(queue.NextGcd?.CueId == "boss" && queue.CompletedCount == 2,
                "An actual early/late phase signal discards unrequested old add actions");
            // TimelineEngine resyncs its clock to signal.At on the native encounter event.
            float Clock(float actualNow) => 18 + actualNow - actualSignalTime;
            Check(!align.CanDispatch(Clock(actualSignalTime)) && !align.CanDispatch(Clock(actualSignalTime+1.9f))
                && align.CanDispatch(Clock(actualSignalTime+2)), "Fast and slow add clears preserve the same two-second follow-up delay");
            Check(!align.CanDispatch(float.NaN), "An invalid clock cannot release phase actions");
        }
        var noSignal = new ReplayPhaseSession(ReplayPhaseMode.AlignToPhase, marks, []);
        Check(noSignal.Plan.Boundaries.Count == 0 && noSignal.Observe(_ => true) == null,
            "A missing phase signal must not create a time-based reset or an unreleasable gap");
        var shortMarks = new[] { new ReplayPhaseMark("a",0,2.5f),new ReplayPhaseMark("b",2.5f,2.5f),new ReplayPhaseMark("c",5,2.5f) };
        var explicitSignal = new ReplayPhaseSession(ReplayPhaseMode.AlignToPhase,shortMarks,[new("short",4,true)]);
        Check(explicitSignal.Plan.Boundaries.Count == 1 && explicitSignal.Plan.Segments["c"] == 1,
            "An explicitly marked phase signal works even without a five-second log gap");
        var ordinarySignal = new ReplayPhaseSession(ReplayPhaseMode.AlignToPhase,shortMarks,[new("ordinary",4)]);
        Check(ordinarySignal.Plan.Boundaries.Count == 0, "Routine clock syncs are not phase transitions during continuous damage");
        var oldGapSignal = new ReplayPhaseSession(ReplayPhaseMode.AlignToPhase,marks,[new("existing",18)]);
        Check(oldGapSignal.Plan.Boundaries.Single().SyncCueId == "existing", "Existing syncs in recorded downtime remain compatible");
        var firstSignal = new ReplayPhaseSession(ReplayPhaseMode.AlignToPhase,[new("first",20,2.5f)],anchors);
        Check(firstSignal.Plan.Segments["first"] == 1, "An explicit phase signal may gate the first recorded action");

        var pending = new ReplayAdaptiveQueue(new[] { ("accepted",100u,true,0),("old-buff",200u,false,0),("new",300u,true,1) });
        pending.Dispatch(ready,0,s=>new(true,s.ActionId)); pending.ReleasePhase(1,discardEarlier:true);
        Check(pending.PendingGcd?.Step.CueId == "accepted" && pending.Confirm(100)?.CueId == "accepted",
            "A native request accepted before the transition must still confirm normally");
        Check(pending.NextGcd?.CueId == "new" && pending.NextOgcd == null,
            "After old confirmation, skip only the unrequested previous-phase actions");
        Check(pending.Confirm(100) == null && pending.Dispatch(ready,1,s=>new(true,s.ActionId)),
            "A late duplicate confirmation cannot consume the new phase opener");

        var later = new ReplayPhaseSession(ReplayPhaseMode.AlignToPhase,
            [new("p1",0,2.5f),new("p2",20,2.5f),new("p3",40,2.5f)],
            [new("s2",18,true),new("s3",38,true)]);
        Check(later.Observe(id=>id=="s3")?.Segment == 2, "A later observed phase can recover from a missed earlier signal");
        Check(later.Observe(_=>true)==null, "The same phase signal cannot reset the queue repeatedly");

        var document = TimelineJson.DeserializeDocument("""{"ReplayEnabled":true,"Cues":[]}""", out _)!;
        Check(document.ReplayPhaseMode == ReplayPhaseMode.ContinueSequence, "Old and new presets default to continuation without difficulty heuristics");
        document.ReplayPhaseMode = ReplayPhaseMode.AlignToPhase;
        document.Cues.Add(new() { Kind=TimelineCueKind.Sync,ActionId=123,TimeOffsetSec=18,ReplayStartsPhase=true });
        var reloaded = TimelineJson.DeserializeDocument(TimelineJson.SerializeDocument(document),out _)!;
        Check(reloaded.ReplayPhaseMode == ReplayPhaseMode.AlignToPhase && reloaded.Cues.Single().ReplayStartsPhase,
            "Phase policy and explicit start markers survive save/reload");
        Check(reloaded.Cues.Single().CopyForDocument().ReplayStartsPhase, "Cue copy/paste retains an explicit phase marker");
        reloaded.Cues.Single().Kind = TimelineCueKind.Memo;
        Check(!reloaded.Cues.Single().CopyForDocument().ReplayStartsPhase, "An ordinary memo must not inherit a live phase marker");
        var other = new TimelineDocument { ReplayEnabled=true };
        Check(other.ReplayPhaseMode == ReplayPhaseMode.ContinueSequence && document.ReplayPhaseMode == ReplayPhaseMode.AlignToPhase,
            "Each duty preset owns its own selection; changing one never changes another");
        Console.WriteLine("Per-preset phase selection, continuation, early/late add clears, real sync gates, pending requests and persistence checks passed.");
    }
}
