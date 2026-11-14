using Oracle.Services;

internal static class TimelineHoldChecks
{
    private static void Check(bool value,string name) { if (!value) throw new Exception(name); }
    public static void Run()
    {
        var origin = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);
        var playbackOrigin = origin;
        var hold = new ReplayTimelineHold();
        double Playback(double seconds) => (hold.Now(origin.AddSeconds(seconds))-playbackOrigin).TotalSeconds;
        Check(Playback(10)==10,"Normal playback follows time before a target stall");
        Check(hold.SetHeld(true,origin.AddSeconds(10))==TimeSpan.Zero,"Begin a hold without shifting the current position");
        Check(Playback(15)==10 && Playback(40)==10,"Attack timeline stays at the same position through targetless downtime");
        Check((origin.AddSeconds(40)-origin).TotalSeconds==40,"Encounter time and real game cooldowns still advance while playback is held");
        var highlightStart = origin.AddSeconds(8);
        var resumed = hold.SetHeld(false,origin.AddSeconds(40));
        playbackOrigin += resumed; highlightStart += resumed;
        Check(resumed.TotalSeconds==30 && Playback(40)==10 && Playback(42)==12,"Resuming removes exactly the paused interval without jumping or restarting");
        Check((highlightStart.AddSeconds(5)-origin.AddSeconds(40)).TotalSeconds==3,"Highlight lifetime resumes with the same remaining duration");
        hold.SetHeld(true,origin.AddSeconds(50)); hold.SetHeld(true,origin.AddSeconds(51));
        Check(Playback(54)==20,"Repeated pause observations cannot move the held position");
        playbackOrigin += hold.SetHeld(false,origin.AddSeconds(55));
        Check(Playback(55)==20 && Playback(56)==21,"Multiple holds accumulate without drift");
        Check(hold.HasHeld && !hold.IsHeld,"Encounter syncs can distinguish a resumed playback clock from an untouched clock");
        hold.Reset();
        Check(!hold.HasHeld && !hold.IsHeld,"A fresh fight clears all hold state");

        var noTarget = ReplayClockHoldPolicy.Decide(true,10,false,true,true,false,false,false);
        Check(noTarget.Hold && noTarget.BlockMainActions && noTarget.Reason==ReplayClockHoldReason.Target,
            "Targetless downtime stops attack playback and damage dispatch");
        var gcd = ReplayClockHoldPolicy.Decide(true,10,false,true,true,true,true,true);
        Check(!gcd.Hold,"Normal GCD/cast/animation/input waits do not freeze playback on every action");
        var blocked = ReplayClockHoldPolicy.Decide(true,10,false,true,true,true,false,true);
        Check(blocked.Hold && !blocked.BlockMainActions,"Unavailable action conditions hold the clock but allow a preparation ability to unblock the GCD");
        Check(!ReplayClockHoldPolicy.Decide(true,10,false,true,true,true,false,false).Hold,
            "Selecting a usable target resumes playback");
        Check(!ReplayClockHoldPolicy.Decide(false,10,false,true,true,false,false,false).Hold,
            "Phase-sync mode, previews and non-replay documents retain their original clock behavior");
        Check(!ReplayClockHoldPolicy.Decide(true,-0.1f,true,true,true,false,false,true).Hold,
            "Countdown time before zero must keep running; no automatic pre-pull execution is introduced");
        Check(ReplayClockHoldPolicy.Decide(true,10,true,true,true,true,false,false).Reason==ReplayClockHoldReason.ManualPause,
            "An explicit replay pause also pauses continuous playback");
        Check(!ReplayClockHoldPolicy.Decide(true,10,false,false,true,false,false,false).Hold,
            "A finished attack list does not freeze unrelated later timeline events");

        var reassemble = new ReplayActionTraits(18,73,true,false,false,false,false);
        var shot = new ReplayActionTraits(58,0,false,true,false,false,false);
        Check(ReplayDowntimePrep.IsSafeSelfAbility(reassemble,4,0),"Native Reassemble metadata classifies as a non-damaging self preparation");
        Check(!ReplayDowntimePrep.IsSafeSelfAbility(shot,3,1),"Attacks must not bypass missing-target waits");
        Check(!ReplayDowntimePrep.IsSafeSelfAbility(reassemble with { AdditionalCooldownGroup=58 },4,0),
            "Channels or self attacks that use the GCD are not auto-detached as preparations");
        Check(!ReplayDowntimePrep.IsSafeSelfAbility(reassemble,4,1),"Self-centered damage abilities are not preparations");
        var gaps = new[] { new ReplayPhaseMark("a",0,2.5f),new ReplayPhaseMark("b",2.5f,2.5f),
            new ReplayPhaseMark("c",40,2.5f),new ReplayPhaseMark("d",42.5f,2.5f) };
        var preparations = new[] { new ReplayPrepMark("manual-prepull",-5,true),new ReplayPrepMark("opener",0,true),
            new ReplayPrepMark("downtime-reassemble",38,true),new ReplayPrepMark("ordinary-buff",41,true),new ReplayPrepMark("damage",39,false) };
        var detached = ReplayDowntimePrep.Find(gaps,preparations);
        Check(detached.SetEquals(new[]{"downtime-reassemble"}),"Auto-detach only safe preparations recorded inside downtime; ordinary burst order is preserved");
        Check(!detached.Contains("manual-prepull"),"Preparation actions before zero remain manual");
        var longCast = ReplayDowntimePrep.Find([new("cast",0,10),new("next",10,2.5f)],[new("during-cast",8,true)]);
        Check(longCast.Count==0,"A long native cast does not create a false downtime preparation lane");
        var downtimeHold = new ReplayTimelineHold(); downtimeHold.SetHeld(true,origin.AddSeconds(10));
        Check((downtimeHold.Now(origin.AddSeconds(38))-origin).TotalSeconds==10
            && 38>=preparations.Single(p=>p.CueId=="downtime-reassemble").At,
            "Encounter-timed Reassemble can become due while the attack timeline is paused behind it");
        var feedback = new ReplayFeedbackLog();
        feedback.Requested("downtime-reassemble",2876,"Self",38,38,38000);
        Check(feedback.LastConfirmed==null,"A preparation request is not a fake success");
        feedback.Confirmed("downtime-reassemble",2876,38.1f,38100);
        Check(feedback.LastConfirmed?.CueId=="downtime-reassemble","A real preparation confirmation completes exactly its source cue");
        Console.WriteLine("Attack-clock holds, zero-time start, smooth resume, independent encounter preparation timing and safe Reassemble gap classification checks passed.");
    }
}
