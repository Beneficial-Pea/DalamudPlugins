using Oracle.Services;
static void Check(bool value, string name) { if (!value) throw new Exception(name); }
var gate = new ReplayStartGate();
Check(!gate.Observe(1,false,true,true), "No countdown: no automatic replay");
Check(!gate.Observe(1,true,false,true), "Preview/unmatched job must not start");
Check(!gate.Observe(1,true,true,true), "Readiness changes must not start late");
Check(gate.Observe(2,true,true,true), "A fresh matching countdown must start replay");
gate.Pause(2);Check(!gate.Observe(2,true,true,true), "Pause must remain effective in the current countdown");
gate.Pause(2);Check(!gate.Observe(2,true,true,true), "Reviving must not resume a paused countdown");
Check(gate.Observe(3,true,true,true), "The next countdown can start again");
Check(!gate.Observe(4,true,true,false), "Opt-out must be respected");
Check(ReplayPreparation.IsMissed(-20,-15), "Short countdown must not bunch missed preparation actions");
Check(!ReplayPreparation.IsMissed(-15,-14.8f), "Small frame delay must not skip the first action");
Check(!ReplayPreparation.IsMissed(0,10), "Combat actions must retain strict ordering");
Check(ReplayPreparation.ImportStart(100000,0)==70000, "Import 30 seconds before the pull");
Check(ReplayPreparation.ImportStart(100000,90000)==90000, "Do not import the previous fight");
Check(ReplayPreparation.ImportStart(10000,0)==0, "Clamp to the beginning of the report");
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
Console.WriteLine("Replay countdown gating, manual pause, preparation timing, localization and cancellation checks passed.");
