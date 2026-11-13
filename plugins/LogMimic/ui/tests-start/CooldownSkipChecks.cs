using Oracle.Services;

internal static class CooldownSkipChecks
{
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    public static void Run()
    {
        var shot = new ReplayActionTraits(58,0,false,true,false,false,false);
        var drill = shot with { CooldownGroup=5,AdditionalCooldownGroup=58 };
        Check(ReplayCooldownPolicy.PersonalGroup(shot)==0, "The shared global cooldown must never be skipped");
        Check(ReplayCooldownPolicy.PersonalGroup(drill)==5, "Drill's own cooldown must be checked separately from its GCD");
        Check(ReplayCooldownPolicy.PersonalGroup(shot with { AdditionalCooldownGroup=7 })==7,
            "A personal additional group must be used when the primary group is the GCD");
        Check(ReplayCooldownPolicy.Remaining(new(58,true,2.5f,0.1f))==0, "A running shared GCD is not an unavailable skill");
        Check(ReplayCooldownPolicy.Remaining(new(5,true,20,3))==17, "A non-charged action uses its active personal recast");
        Check(ReplayCooldownPolicy.Remaining(new(7,true,60,12,0,2,30))==18, "Zero charges use the time until one charge recovers, not full recovery");
        Check(ReplayCooldownPolicy.Remaining(new(7,true,60,35,1,2,30))==0, "A partially recharging skill remains usable with one charge");
        Check(ReplayCooldownPolicy.Remaining(new(7,true,90,10,0,1,30))==80, "A synced single-charge action must not assume extra charges");
        Check(ReplayCooldownPolicy.Remaining(new(7,false,60,0))==0
            && ReplayCooldownPolicy.Remaining(new(7,true,60,60))==0, "Inactive or completed recasts must not cause skips");
        Check(ReplayCooldownPolicy.Remaining(new(7,true,float.NaN,0))==0
            && ReplayCooldownPolicy.Remaining(new(7,true,60,-1))==0
            && ReplayCooldownPolicy.Remaining(new(7,true,60,1,0,2,float.NaN))==0, "Invalid cooldown data must not discard an action");
        Check(!ReplayCooldownPolicy.ShouldSkip(0.2f,true) && ReplayCooldownPolicy.ShouldSkip(0.5f,true),
            "Keep a personal GCD that is already inside the normal native input queue window");
        Check(ReplayCooldownPolicy.ShouldSkip(0.2f,false) && !ReplayCooldownPolicy.ShouldSkip(0,false),
            "An ability with its own cooldown is skipped, while a ready ability remains available");

        var ready = new ReplayNativeWindow(0,0,0,false);
        var gcd = new ReplayAdaptiveQueue(new[] { ("drill",16498u,true),("shot",7411u,true) });
        var attempts = new List<string>();
        Check(gcd.Dispatch(ready,0,s=> {
            attempts.Add(s.CueId);
            return s.CueId=="drill" ? new(false,SkipCooldown:true) : new(true,s.ActionId);
        }), "A ready attack must dispatch in the same update after a cooling GCD");
        Check(attempts.SequenceEqual(new[]{"drill","shot"}) && gcd.CooldownSkippedCount==1
            && gcd.PendingGcd?.Step.CueId=="shot", "Skipped Drill creates no request or confirmation wait");
        Check(gcd.Confirm(16498)==null && gcd.Confirm(7411)?.CueId=="shot" && gcd.Finished,
            "A skip is never treated as an actual use and cannot consume another skill's confirmation");

        var repeat = new ReplayAdaptiveQueue(new[] {
            ("buff-first",200u,false),("shot",100u,true),("buff-later",200u,false)
        });
        repeat.Dispatch(ready,0,s=>s.CueId=="buff-first" ? new(false,SkipCooldown:true) : new(true,s.ActionId));
        Check(repeat.PendingGcd?.Step.CueId=="shot" && repeat.PendingOgcd==null, "A cooling leading ability must not block the first GCD");
        repeat.Confirm(100);
        Check(repeat.Dispatch(new(1.5f,0,0,false),1000,s=>new(true,s.ActionId))
            && repeat.PendingOgcd?.Step.CueId=="buff-later", "A later occurrence of the same action is not globally excluded");
        repeat.Confirm(200);
        Check(repeat.Finished && repeat.CooldownSkippedCount==1, "A skipped occurrence is not retried when its cooldown later recovers");

        var unavailable = new ReplayAdaptiveQueue(new[] { ("needs-target",100u,true) });
        Check(!unavailable.Dispatch(ready,0,_=>throw new Exception("Targetless skip"),targetAvailable:false)
            && unavailable.CompletedCount==0, "No target keeps the waiting action intact");
        Check(!unavailable.Dispatch(ready,1,_=>new(false,TargetBlocked:true,SkipCooldown:true))
            && unavailable.CompletedCount==0, "Target failure wins over a conflicting skip signal");
        Check(!unavailable.Dispatch(ready,2,_=>new(false)) && unavailable.CompletedCount==0,
            "Range, resources or other unavailable states must not be mislabeled as cooldown skips");
        Check(!unavailable.Dispatch(new(1.5f,0,0,false),3,_=>throw new Exception("Skipped shared GCD")),
            "Wait for the normal shared GCD instead of skipping every attack");
        Check(!unavailable.Dispatch(new(0,0,0,true),4,_=>throw new Exception("Duplicate native queue")),
            "An already queued action blocks further requests and skips");

        var phased = new ReplayAdaptiveQueue(new[] { ("old",100u,true,0),("future",200u,true,1) });
        Check(!phased.Dispatch(ready,0,_=>new(false,SkipCooldown:true))
            && phased.CompletedCount==1 && phased.NextGcd==null, "Skipping a cooldown cannot bypass a phase signal gate");
        phased.ReleasePhase(1);
        Check(phased.Dispatch(ready,1,s=>new(true,s.ActionId)) && phased.PendingGcd?.Step.CueId=="future",
            "The next section is eligible only after the normal phase release");

        var all = new ReplayAdaptiveQueue(new[] { ("a",100u,true),("b",200u,false) });
        Check(!all.Dispatch(ready,0,_=>new(false,SkipCooldown:true)) && all.Finished
            && all.PendingGcd==null && all.PendingOgcd==null, "An all-cooldown section finishes without fake native requests");
        var many = new ReplayAdaptiveQueue(Enumerable.Range(0,71).Select(i=>($"step-{i}",(uint)i+1,true)));
        var count=0;
        Check(!many.Dispatch(ready,0,s=>{count++;return new(false,SkipCooldown:true);}) && count==64,
            "Bound cooldown scanning so long imported lists cannot monopolize a frame");
        Check(many.Dispatch(ready,1,s=>s.CueId=="step-70" ? new(true,s.ActionId) : new(false,SkipCooldown:true))
            && many.PendingGcd?.Step.CueId=="step-70" && many.CooldownSkippedCount==70,
            "Resume scanning the remaining cooldown cues on the next update");
        Console.WriteLine("Personal cooldown skips, shared GCD waits, native charges, same-update continuation, phase gates and real confirmations checks passed.");
    }
}
