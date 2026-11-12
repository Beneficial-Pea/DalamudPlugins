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

var identicalEnemies = new ulong[] { 0x40000101, 0x40000102, 0x40000103, 0x40000104 };
foreach (var id in identicalEnemies)
{
    var decision = ReplayTargetChoice.Choose(true, "enemy", new(id, true, false, true));
    Check(decision.OverrideRecorded && decision.ObjectId == id, "Follow each explicitly selected instance among four identical NPCs");
}
var followFirst = ReplayTargetChoice.Choose(true, "enemy", new(11, true, false, true));
var followNext = ReplayTargetChoice.Choose(true, "enemy", new(12, true, false, true));
Check(followFirst.ObjectId == 11 && followNext.ObjectId == 12, "Target switches must use the new selected object without cached mappings");
foreach (var invalid in new SelectedReplayTarget?[] {null, new(13, true, true, true), new(14, true, false, false), new(15, false, false, true), new(0, true, false, true), new(0xE0000000, true, false, true)})
{
    var decision = ReplayTargetChoice.Choose(true, "enemy", invalid);
    Check(decision.OverrideRecorded && decision.ObjectId == null, "Invalid selection must wait instead of falling back to another enemy");
}
foreach (var kind in new[] { "self", "ally", "default", "unknown" })
    Check(!ReplayTargetChoice.Choose(true, kind, new(16, true, false, true)).OverrideRecorded, "Preserve non-enemy targeting: " + kind);
Check(!ReplayTargetChoice.Choose(false, "enemy", new(17, true, false, true)).OverrideRecorded, "Disabled option must preserve recorded/mapped targeting");
Console.WriteLine("Current-target selection, switching among identical NPCs, missing/dead target waiting and self/ally targeting checks passed.");
