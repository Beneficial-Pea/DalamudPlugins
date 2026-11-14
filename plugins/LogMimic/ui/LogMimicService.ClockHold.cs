using FFXIVClientStructs.FFXIV.Client.Game;
using Oracle.Models;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace Oracle.Services;

internal sealed unsafe partial class LogMimicService
{
    private bool _hasHostileReplay;
    private ReplayClockHoldDecision _clockHold;
    public bool TimelineIsHeld => _engine.IsReplayTimelineHeld;
    public string TimelineHoldText => I18n.Get(_clockHold.Reason switch {
        ReplayClockHoldReason.ManualPause => "clock_hold.manual",
        ReplayClockHoldReason.ActionCondition => "clock_hold.condition",
        _ => "clock_hold.target",
    });

    private static bool SafeDowntimePreparation(uint id)
    {
        var row = PluginServices.DataManager.GetExcelSheet<LuminaAction>()?.GetRowOrDefault(id);
        return row is { } action && ReplayDowntimePrep.IsSafeSelfAbility(ReplayNativeTiming.Traits(id),
            action.ActionCategory.RowId, action.AttackType.RowId);
    }

    private static HashSet<string> FindDowntimePreparations(TimelineDocument doc, IReadOnlyList<TimelineCue> ordered)
    {
        var damage = ordered.Where(c => ReplayNativeTiming.Traits(c.ActionId) is { IsGcd: true, Hostile: true })
            .Select(c => new ReplayPhaseMark(c.Id,Scheduled(c),Math.Max(ActionTiming.GetCastSeconds(c.ActionId),2.5f)));
        return ReplayDowntimePrep.Find(damage,ordered.Select(c => new ReplayPrepMark(c.Id,Scheduled(c),SafeDowntimePreparation(c.ActionId))));
    }

    private void PrepareClockHold()
    {
        _clockHold = default;
        _hasHostileReplay = _main.Any(c => ReplayNativeTiming.Traits(c.ActionId).Hostile);
    }

    private bool WantsTimelineHold()
    {
        var eligible = _doc is { ReplayEnabled: true, ReplayPhaseMode: ReplayPhaseMode.ContinueSequence }
            && ReferenceEquals(_doc,_engine.ActiveDocument) && _engine.IsRunning && !_engine.IsPreview;
        if (!eligible) { _clockHold = default; return false; }
        var remaining = _adaptiveMode ? _adaptive is { Finished: false } : _mainIndex < _main.Count;
        var selected = PluginServices.TargetManager.Target;
        var targetReady = selected is { IsDead: false, IsTargetable: true }
            && selected.GameObjectId is > 0 and not 0xE0000000UL;
        var busy = true;
        var blocked = false;
        var manager = ActionManager.Instance();
        var cue = _adaptiveMode && _adaptive?.NextGcd is { } step ? _adaptiveCues.GetValueOrDefault(step.CueId)
            : !_adaptiveMode && _mainIndex < _main.Count ? _main[_mainIndex] : null;
        if (manager != null && cue != null && targetReady && !_suspended)
        {
            var window = ReplayNativeTiming.Read(manager);
            busy = !window.CanQueueGcd || (_adaptiveMode ? _adaptive?.PendingGcd != null : _pendingMainId != null)
                || (!_adaptiveMode && PlaybackElapsed < Scheduled(cue));
            if (!busy)
            {
                var id = manager->GetAdjustedActionId(cue.ActionId);
                if (id == 0) id = cue.ActionId;
                id = ResolveProcAction(manager,cue,id);
                var target = ResolveTarget(id,out var reason);
                var skip = ReplayCooldownPolicy.ShouldSkip(ReplayNativeCooldown.Remaining(manager,id),_adaptiveMode && ReplayNativeTiming.IsGcd(id));
                blocked = !skip && (reason != null || target == null
                    || manager->GetActionStatus(ActionType.Action,id,target.GameObjectId,false,false) != 0);
            }
        }
        var manuallyPaused = _suspended && (!C.ReplayAutoStartOnCountdown || !_engine.IsCountdownSession
            || _autoStart.IsHandled(_engine.CountdownSequence));
        _clockHold = ReplayClockHoldPolicy.Decide(true,_engine.ElapsedSeconds,manuallyPaused,
            remaining,_hasHostileReplay,targetReady,busy,blocked);
        return _clockHold.Hold;
    }
}
