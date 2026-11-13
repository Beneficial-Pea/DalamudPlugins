using Oracle.Services;

internal static class ManualTargetChecks
{
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    public static void Run()
    {
        var attack = new ReplayActionTraits(58, 0, false, true, false, false, false);
        var selfBuff = new ReplayActionTraits(0, 0, true, false, false, false, false);
        var allySkill = new ReplayActionTraits(0, 0, true, false, true, true, false);
        var a = new ReplayManualObject(101, false, true);
        var b = new ReplayManualObject(202, false, true);
        Check(ReplayManualTarget.Choose(attack, 1, a).ObjectId == 101, "Use the selected enemy without a recorded-name/ID restriction");
        Check(ReplayManualTarget.Choose(attack, 1, b).ObjectId == 202, "The next request follows a changed selection");
        Check(ReplayManualTarget.Choose(attack, 1, null).ObjectId == null, "Clearing the target must not reacquire a logged/previous enemy");
        Check(ReplayManualTarget.Choose(attack, 1, new(101, true, true)).ObjectId == null, "Dead selection must not redirect to another enemy");
        Check(ReplayManualTarget.Choose(attack, 1, new(101, false, false)).ObjectId == null, "Untargetable selection waits rather than finding another enemy");
        Check(ReplayManualTarget.Choose(attack, 1, new(0, false, true)).ObjectId == null
            && ReplayManualTarget.Choose(attack, 1, new(0xE0000000, false, true)).ObjectId == null, "Invalid target sentinels never enter UseAction");
        Check(ReplayManualTarget.Choose(selfBuff, 1, a) == new ReplayManualSelection(1, true)
            && ReplayManualTarget.Choose(selfBuff, 1, null) == new ReplayManualSelection(1, true), "Tactician and other self-only buffs retain self use without changing enemy selection");
        Check(ReplayManualTarget.Choose(selfBuff, null, a).ObjectId == null, "Missing local player cannot redirect a self buff");
        Check(ReplayManualTarget.Choose(allySkill, 1, b).ObjectId == 202
            && !ReplayManualTarget.Choose(allySkill, 1, b).Self, "Targeted ally actions use the selected character, not an inferred log target");
        Check(ReplayManualTarget.Choose(allySkill, 1, null).ObjectId == null, "A targeted ally skill must not silently select a party member");
        Check(!ReplayManualTarget.IsSelfOnly(selfBuff with { Area = true }), "Ground targeting must not be misclassified as a self-only buff");

        ReplayManualObject? current = null;
        var sent = new List<(uint Action, ulong Target)>();
        var queue = new ReplayAdaptiveQueue(new[] { ("first", 7411u, true), ("next", 7412u, true) });
        ReplayAttemptOutcome Attempt(ReplayAdaptiveStep step)
        {
            var choice = ReplayManualTarget.Choose(attack, 1, current);
            if (choice.ObjectId is not { } target) return new(false, TargetBlocked: true);
            sent.Add((step.ActionId, target));
            return new(true, step.ActionId);
        }
        var ready = new ReplayNativeWindow(0, 0, 0, false);
        Check(!queue.Dispatch(ready, 0, Attempt) && queue.CompletedCount == 0, "No target leaves the attack pending without consuming it");
        current = a;
        Check(queue.Dispatch(ready, 100, Attempt), "Selecting a target resumes the waiting attack");
        current = b;
        Check(!queue.Dispatch(ready, 150, Attempt), "A target change must not duplicate an already accepted native request");
        Check(queue.Confirm(7411)?.CueId == "first", "The old target's accepted request confirms normally");
        Check(queue.Dispatch(ready, 200, Attempt), "The next action uses the new current target");
        Check(sent.SequenceEqual(new[] { (7411u, 101ul), (7412u, 202ul) }), "Only newly dispatched skills follow the latest selection");
        Console.WriteLine("Manual target changes, no-target waits, self buffs, ally selection and native queue handoff checks passed.");
    }
}
