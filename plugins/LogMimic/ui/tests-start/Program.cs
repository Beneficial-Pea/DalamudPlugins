using System.IO.Compression;
using System.Text.Json;
using Oracle.Services;
static void Check(bool value, string name) { if (!value) throw new Exception(name); }
var gate = new ReplayStartGate();
Check(!gate.Observe(1,false,true,true,0), "No countdown: no automatic replay");
Check(!gate.Observe(1,true,false,true,-15), "Preview/unmatched job must not arm");
Check(!gate.Observe(1,true,true,true,0), "Readiness changes must not start late");
Check(!gate.Observe(2,true,true,true,-20), "Wait through the start of a countdown");
Check(!gate.Observe(2,true,true,true,-15), "Do not execute at preparation time");
Check(!gate.Observe(2,true,true,true,-0.01f), "Do not start just before zero");
Check(gate.Observe(2,true,true,true,0), "Start at zero");
Check(!gate.Observe(2,true,true,true,0.2f), "Only one start per countdown");
Check(!gate.Observe(3,true,true,true,-10), "Arm a fresh countdown");
gate.Pause(3);Check(!gate.Observe(3,true,true,true,0), "Pause before zero must cancel the pending automatic start");
Check(gate.Observe(4,true,true,true,0.01f), "A new countdown can start on the first frame after zero");
gate.Pause(4);Check(!gate.Observe(4,true,true,true,1), "Reviving must not resume a paused countdown");
Check(!gate.Observe(5,true,true,false,-10), "Opt-out must be respected");
Check(!gate.Observe(5,true,true,true,0), "Changing opt-in mid-countdown must wait for a fresh countdown");
Check(!ReplayStartGate.CanExecuteAt(-0.01f), "Manual enable must not execute before zero");
Check(ReplayStartGate.CanExecuteAt(0), "Manual enable may execute at zero");
Check(!ReplayStartGate.IsCombatAction(-15), "Exclude old preset preparation actions");
Check(!ReplayStartGate.IsCombatAction(-0.001f), "Exclude all negative action times");
Check(ReplayStartGate.IsCombatAction(0) && ReplayStartGate.IsCombatAction(1.2f), "Keep zero and positive action times unchanged");
Check(!ReplayStartGate.CanExecuteAt(float.NaN), "Invalid clock must not execute");
foreach (var text in new[]{"Battle commencing in 20 seconds!", "전투 시작까지 20초!", "전투 개시까지 20초", "戦闘開始まであと20秒"})
{
 var signals=new CountdownSignals();signals.Receive(text,1000);var first=signals.Drain(1100);
 Check(first.Started && Math.Abs(first.Remaining-19.9f)<0.01, "Localized countdown: "+text);
 signals.Receive("Battle commencing in 5 seconds!",16000);Check(!signals.Drain(16000).Started,"Repeated ticks must not re-arm replay");
 signals.Receive("초읽기가 취소되었습니다.",17000);Check(signals.Drain(17000).Cancelled,"Korean cancellation");
 signals.Receive(text,18000);Check(signals.Drain(18000).Started,"Restart after cancellation");
}
var late=new CountdownSignals();late.Receive("Battle commencing in 15 seconds!",0);Check(!late.Drain(20000).Started,"Expired queued countdown must not start late");
var cancel=new CountdownSignals();cancel.Receive("Battle commencing in 15 seconds!",0);cancel.Receive("Countdown canceled.",100);Check(!cancel.Drain(100).Started,"Cancelled pending start");
Console.WriteLine("Zero-time start, negative-time exclusion, manual pause, death pause and countdown cancellation checks passed.");

var names=new NpcNameIndex(new Dictionary<uint,string>{{101,"Alexander Prime"},{102,"Alexander Prime"},{103,"Cruise Chaser"}});
Check(names.Matches(" ALEXANDER PRIME ",101), "Resolve English name to numeric ID, independent of live Korean name");
Check(names.GetIds("Alexander Prime").Count==2, "Keep all IDs for duplicate names; do not choose an arbitrary row");
Check(!names.Matches("Alexander Prime",103), "Do not match a different NPC");
Check(names.GetIds("Unknown NPC").Count==0 && !names.Matches("Alexander Prime",0), "Missing IDs require fallback, not guessing");
using var npcFile=File.OpenRead(Path.Combine(AppContext.BaseDirectory,"BNpcNames.en.json.gz"));
using var npcZip=new GZipStream(npcFile,CompressionMode.Decompress);
var npcRows=JsonSerializer.Deserialize<Dictionary<uint,string>>(npcZip)!;
Check(npcRows.Count>10000, "Bundled index must contain real NPC data");
Check(new NpcNameIndex(npcRows).GetIds("Alexander Prime").Count>0, "Alexander Prime English target can resolve through the bundled index");
Console.WriteLine("English NPC identity lookup, ambiguous-name preservation and bundled data checks passed.");

var loggedUnique = ReplayTargetChoice.Choose(new ulong[] {11}, 22, true);
Check(loggedUnique.ObjectId == 11 && !loggedUnique.UsedCurrentSelection, "Unique logged target must win over a different selected enemy");
var identicalEnemies = new ulong[] {101,102,103,104};
foreach (var selected in identicalEnemies)
{
    var result = ReplayTargetChoice.Choose(identicalEnemies, selected, true);
    Check(result.ObjectId == selected && result.UsedCurrentSelection, "Choose the selected instance only among matching enemies");
}
Check(ReplayTargetChoice.Choose(identicalEnemies,101,true).ObjectId == 101
    && ReplayTargetChoice.Choose(identicalEnemies,103,true).ObjectId == 103, "Re-evaluate selection on every action request");
Check(ReplayTargetChoice.Choose(identicalEnemies,null,true).ObjectId == null, "Ambiguous target without a selection must wait");
Check(ReplayTargetChoice.Choose(identicalEnemies,999,true).ObjectId == null, "An unrelated selected enemy must never override the logged target group");
Check(ReplayTargetChoice.Choose(identicalEnemies,101,false).ObjectId == null, "Respect disabled ambiguity fallback");
Check(ReplayTargetChoice.Choose(Array.Empty<ulong>(),101,true).ObjectId == null, "Do not attack an unrelated target when the logged enemy is absent");
Check(ReplayTargetChoice.Choose(new ulong[] {11,11},22,true).ObjectId == 11, "Duplicate object references do not create false ambiguity");
Check(ReplayTargetChoice.Choose(new ulong[] {0,0xE0000000UL,11},22,true).ObjectId == 11, "Invalid object IDs must not become targets");
Console.WriteLine("Unique logged target priority, ambiguous NPC selection, target switching and missing/unrelated target checks passed.");

var castWindows = ReplayCastTiming.Resolve(new ReplayCastMark[] {
    new(0,100,1,0,true), new(1,100,1,2500,false), new(2,200,2,3000,false),
    new(3,100,1,4000,true), new(4,100,1,5000,true), new(5,100,1,7500,false),
    new(6,300,1,8000,true), new(7,300,2,9000,false), new(8,400,1,10000,true)
});
Check(castWindows.Count == 4, "Cast-start records must not create duplicate actions or include an interrupted cast");
Check(castWindows[0].Index == 1 && castWindows[0].StartTimestamp == 0, "Use the real cast start with the original completed event's target");
Check(castWindows[1].Index == 2 && castWindows[1].StartTimestamp == 3000, "Instant casts retain their logged time");
Check(castWindows[2].StartTimestamp == 5000, "Pair the most recent start after an interruption");
Check(castWindows[3].StartTimestamp == 9000, "Mismatched target starts must not be paired");
Check(ReplayPacing.ConfirmationTimeoutMs(6) >= 7500, "Long casts must not expire at the old 2.6 second timeout");
Check(ReplayPacing.Scheduled(2.5f,0) == 0 && ReplayPacing.Scheduled(2.5f,null) == 2.5f, "Preserve legacy timing without inventing an unavailable cast start");
var alignment = ReplayPacing.AlignmentOffset(true,new float[] {0.4f,1.0f});
Check(Math.Abs((0.4f-alignment)-0)<0.001f && Math.Abs((1.0f-alignment)-0.6f)<0.001f, "First-action alignment must preserve intervals");
Check(ReplayPacing.AlignmentOffset(false,new float[]{0.4f,1}) == 0, "Alignment off preserves raw log timing");
var feedback = new ReplayFeedbackLog();
feedback.Requested("cue",100,"Target",0.4f,0.5f,1000);
Check(feedback.LastConfirmed == null && feedback.LastRequest?.ActionId == 100, "Accepted queue request is not a confirmed skill use");
feedback.Confirmed("unknown",200,0.6f,1200);
Check(feedback.LastConfirmed == null, "Unrequested confirmations must not fake replay success");
feedback.Confirmed("cue",100,0.7f,1300);
Check(feedback.LastConfirmed?.ActionId == 100 && feedback.LastConfirmed.ConfirmedAt == 0.7f, "Record skill use only after a real confirmation");
feedback.Confirmed("cue",100,0.8f,1400);
Check(feedback.History.Count == 1, "A duplicate confirmation must not double-count the action");
Check(NpcNameIndex.MatchesLocalizedName("Living Liquid","활성 액체",new[]{"활성 액체"}), "Fallback to exact localized NPC text");
Check(!NpcNameIndex.MatchesLocalizedName("Living Liquid","인형 수렵병",new[]{"활성 액체"}), "Do not target unrelated localized names");
Console.WriteLine("Recorded cast starts, alignment, long-cast pending, true request/confirmation feedback and Korean-name fallback checks passed.");

var clicks = new ReplayClickSequence<string>();
Check(clicks.TakeDue(1000).Count == 0, "No accepted request must mean no simulated presses");
clicks.Start(null,1000); clicks.Start(Array.Empty<string>(),1000);
Check(clicks.TakeDue(1000).Count == 0, "A skill absent from visible hotbars must not press an unrelated slot");
var capturedSlots = new[] {"bar1-slot2", "bar3-slot4"};
clicks.Start(capturedSlots,1000); capturedSlots[0] = "different-slot";
Check(clicks.TakeDue(1000).SequenceEqual(new[] {"bar1-slot2", "bar3-slot4"}), "Press slots captured before a combo ID changes, without retaining mutable snapshots");
Check(clicks.TakeDue(1000).Count == 0 && clicks.TakeDue(1149).Count == 0, "Do not restart the native animation every frame");
Check(clicks.TakeDue(1150).Count == 2 && clicks.TakeDue(1300).Count == 0, "Finish the short press sequence without an endless flash");
clicks.Start(new[] {"first"},2000); clicks.Start(new[] {"second"},2010);
Check(clicks.TakeDue(2010).SequenceEqual(new[] {"first", "second"}), "Closely spaced accepted actions each retain their own slot snapshot");
clicks.Clear(); Check(clicks.TakeDue(2200).Count == 0, "Pause, death, logout or replay reset must discard pending presses");
clicks.Start(new[] {"stale"},3000);
Check(clicks.TakeDue(3500).Count == 0, "Stalled frames must discard stale presses instead of replaying them later");
Console.WriteLine("Native hotbar press scheduling, slot snapshots, frame deduplication and cancellation checks passed.");

var nativeCountdown = new NativeCountdownSignals();
Check(!nativeCountdown.Observe(false,0,0).Started, "An inactive native agent must not start replay");
var nativeStart = nativeCountdown.Observe(true,20,1000);
Check(nativeStart.Started && nativeStart.ClockRemaining == 20, "Read countdown directly even when no chat start arrives");
var nativeTick = nativeCountdown.Observe(true,0.02f,20980);
Check(!nativeTick.Started && nativeTick.ClockRemaining == 0.02f, "Native countdown ticks must resync timing without rearming replay");
var nativeZero = nativeCountdown.Observe(false,0,21000);
Check(!nativeZero.Cancelled && nativeZero.ClockRemaining == 0, "A natural countdown completion reaches zero without cancelling");
Check(nativeCountdown.Observe(false,0,21016).ClockRemaining == null, "Do not freeze the timeline at zero after the countdown ends");
Check(nativeCountdown.Observe(true,15,22000).Started, "A new native countdown can arm again");
Check(nativeCountdown.Observe(false,0,23000).Cancelled, "A countdown removed before zero must cancel replay");
Check(!nativeCountdown.Observe(true,float.NaN,24000).Started, "Invalid native timer data must not start replay");
var earlyEnd = new NativeCountdownSignals();
earlyEnd.Observe(true,0.1f,1000);
Check(earlyEnd.Observe(false,0,1016).ClockRemaining == null, "Do not move the timeline to zero before the sampled deadline");
Console.WriteLine("Native countdown start, zero synchronization, cancellation and clock continuation checks passed.");
AdaptiveChecks.Run();

PotionChecks.Run();
ManualTargetChecks.Run();
