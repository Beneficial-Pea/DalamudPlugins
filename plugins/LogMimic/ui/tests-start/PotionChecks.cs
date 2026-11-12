using Oracle.Models;
using Oracle.Services;

internal static class PotionChecks
{
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    public static void Run()
    {
        const uint loggedHq = 34603667;
        Check(ReplayPotionPolicy.TryDecodeItem(loggedHq, out var hq) && hq.ItemId == 49235 && hq.Hq
            && hq.NativeId == 1049235, "Real FFLogs HQ dexterity potion decodes to native item ID");
        Check(ReplayPotionPolicy.TryDecodeItem(0x02000000 | 49235, out var nq) && !nq.Hq && nq.NativeId == 49235,
            "Normal-quality FFLogs item has no HQ offset");
        foreach (var id in new uint[] { 0, 7410, 0x02000000, 0x02200000, 0x03000001 })
            Check(!ReplayPotionPolicy.TryDecodeItem(id, out _), "Invalid/non-item IDs must not decode");
        Check(!ReplayPotionPolicy.IsSkill(loggedHq) && !ReplayPotionPolicy.IsSkill(0x03000001)
            && ReplayPotionPolicy.IsSkill(7410), "Packed items and other event types never enter action queues");
        Check(ReplayPotionIds.Global.ContainsKey(49235) && !ReplayPotionIds.Global.ContainsKey(4559),
            "Global stat-potion fallback includes newer logs but excludes elixirs");

        var inventory = new[] {
            new ReplayPotionCandidate(49235, 2, 710, 0, 0), // not owned on an older client
            new ReplayPotionCandidate(45996, 2, 690, 3, 0),
            new ReplayPotionCandidate(44163, 2, 660, 1, 2),
            new ReplayPotionCandidate(49234, 1, 710, 5, 5),
        };
        var settings = new ReplayPotionSettings();
        Check(ReplayPotionPolicy.Select(settings, 2, inventory) == null, "Automatic inventory use defaults to off");
        settings.Enabled = true;
        Check(ReplayPotionPolicy.Select(settings, 2, inventory) == new ReplayPotionChoice(45996, false),
            "Automatic selection uses the highest owned local tier for dexterity jobs");
        settings.Quality = ReplayPotionQuality.HqOnly;
        Check(ReplayPotionPolicy.Select(settings, 2, inventory) == new ReplayPotionChoice(44163, true),
            "HQ-only can use an older owned tier instead of an unavailable global potion");
        settings.ItemId = 49235;
        Check(ReplayPotionPolicy.Select(settings, 2, inventory) == null, "A missing explicitly selected potion is never substituted");
        settings.ItemId = 49234;
        Check(ReplayPotionPolicy.Select(settings, 2, inventory) == null, "Wrong-stat selection must not consume inventory");
        Check(ReplayPotionPolicy.Select(settings, 1, inventory) == new ReplayPotionChoice(49234, true),
            "Strength jobs select strength potions");
        settings.Quality = ReplayPotionQuality.NormalOnly;
        Check(ReplayPotionPolicy.Select(settings, 1, inventory) == new ReplayPotionChoice(49234, false), "NQ-only excludes HQ");
        Check(ReplayPotionPolicy.Select(settings, 0, inventory) == null, "Unresolved job must not consume items");
        var snapshot = settings.Copy(); settings.Enabled = false; settings.ItemId = 0;
        Check(snapshot.Enabled && snapshot.ItemId == 49234, "Imported presets own a settings snapshot");

        var old = TimelineJson.DeserializeDocument("""
            {"ReplayEnabled":true,"Cues":[{"Kind":"Action","ActionId":34603667,"TimeOffsetSec":25.8},
            {"Kind":"Action","ActionId":7410,"TimeOffsetSec":26.4}]}
            """, out var dropped)!;
        Check(dropped == 0 && old.Cues[0].Kind == TimelineCueKind.Item && old.Cues[0].ReplayItemId == 49235
            && old.Cues[0].ReplayItemHq && !old.ReplayPotions.Enabled, "Old presets migrate item records without opting in");
        ReplayPotionPolicy.Normalize(old);
        var copiedCue = old.Cues[0].CopyForDocument();
        Check(copiedCue.Kind == TimelineCueKind.Item && copiedCue.ReplayItemId == 49235 && copiedCue.ReplayItemHq,
            "Copy/paste and duplicate preserve potion metadata");
        var roundTrip = TimelineJson.DeserializeDocument(TimelineJson.SerializeDocument(old), out dropped)!;
        Check(dropped == 0 && roundTrip.Cues[0].Kind == TimelineCueKind.Item && roundTrip.Cues[1].ActionId == 7410,
            "Item cues survive JSON save and reload without becoming actions or retired cues");
        var numeric = TimelineJson.DeserializeDocument("""{"Cues":[{"Kind":5,"ReplayItemId":49235}]}""", out dropped)!;
        Check(dropped == 0 && numeric.Cues[0].Kind == TimelineCueKind.Item, "Numeric Item kind is accepted");
        var allowed = new HashSet<uint>();
        Check(!ReplayPotionPolicy.Include(copiedCue, true, allowed, new()), "Replay-all never overrides potion opt-out");
        Check(ReplayPotionPolicy.Include(copiedCue, false, allowed, snapshot), "Potion inclusion is independent of skill selection");
        Check(ReplayPotionPolicy.Include(old.Cues[1], true, allowed, new()), "Potion opt-out preserves ordinary skills");

        var calls = 0;
        var lane = new ReplayPotionLane([new("potion", 25.8f)]);
        Check(!lane.Dispatch(25, _ => { calls++; return ReplayPotionAttempt.Sent; }) && calls == 0, "Wait for recorded potion time");
        Check(!lane.Dispatch(25.8f, _ => ReplayPotionAttempt.Retry) && lane.CompletedCount == 0, "Busy weave waits without blocking skills");
        Check(!lane.Dispatch(26, _ => ReplayPotionAttempt.Skip) && lane.CompletedCount == 1, "Missing/cooldown potion is terminal and nonblocking");
        var actions = old.Cues.Where(c => c.Kind == TimelineCueKind.Action && ReplayPotionPolicy.IsSkill(c.ActionId));
        var queue = new ReplayAdaptiveQueue(actions.Select(c => (c.Id, c.ActionId, true)));
        Check(queue.Dispatch(new(0, 0, 0, false), 26000, _ => new(true, 7410)),
            "The next damage action can dispatch in the same update after a skipped potion");
        Check(!lane.Dispatch(30, _ => throw new Exception("Skipped item retried")), "Skip is remembered");
        var used = new ReplayPotionLane([new("potion", 0)]);
        Check(used.Dispatch(0, _ => ReplayPotionAttempt.Sent), "Accepted native item request advances the item lane");
        Check(!used.Dispatch(0.1f, _ => throw new Exception("Double item use")), "No repeated item consumption while waiting on action confirmations");
        var stale = new ReplayPotionLane([new("prep", -15), new("late", 10)]);
        Check(!stale.Dispatch(16, _ => throw new Exception("Stale item fired")) && stale.CompletedCount == 1,
            "Preparation items are manual; expired potion events do not fire late");
        Check(!stale.Dispatch(float.NaN, _ => throw new Exception("Invalid clock")), "Invalid timeline cannot consume items");
        Console.WriteLine("Potion ID decoding, opt-in import, tier/quality selection, legacy migration and nonblocking replay checks passed.");
    }
}
