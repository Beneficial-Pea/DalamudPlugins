using FFXIVClientStructs.FFXIV.Client.Game;

namespace Oracle.Services;

internal sealed unsafe partial class LogMimicService
{
    private long _lastReplayTick;
    private string _lastReplayUpdateError = string.Empty;

    public void Update()
    {
        try
        {
            UpdateReplayCore();
            _lastReplayUpdateError = string.Empty;
        }
        catch (Exception ex)
        {
            _lastReplayUpdateError = ex.GetType().Name + ": " + ex.Message;
            throw;
        }
        finally { _lastReplayTick = Environment.TickCount64; }
    }

    private ReplayNativeWindow ReadReplayWindow(ActionManager* manager) => ReadReplayWindow(manager, out _);

    private ReplayNativeWindow ReadReplayWindow(ActionManager* manager, out uint? fullStatus)
    {
        var window = ReplayNativeTiming.Read(manager);
        fullStatus = null;
        if (!_adaptiveMode || _adaptive?.NextGcd is not { } step
            || !_adaptiveCues.TryGetValue(step.CueId, out var cue)) return window;
        var actionId = manager->GetAdjustedActionId(cue.ActionId);
        if (actionId == 0) actionId = cue.ActionId;
        actionId = ResolveProcAction(manager, cue, actionId);
        if (!ReplayNativeTiming.IsGcd(actionId)) return window;
        var target = ResolveTarget(actionId, out var reason);
        if (reason != null || target == null) return window;
        fullStatus = manager->GetActionStatus(ActionType.Action, actionId, target.GameObjectId, true, true);
        return ReplayGcdReadiness.Reconcile(window, fullStatus);
    }

    private void PrintLiveReplayStatus()
    {
        var manager = ActionManager.Instance();
        if (manager == null) return;
        var window = ReadReplayWindow(manager, out var nativeStatus);
        var pending = _adaptive?.PendingGcd;
        var age = _lastReplayTick == 0 ? "-" : Math.Max(0, (Environment.TickCount64 - _lastReplayTick) / 1000f).ToString("0.00");
        Message(I18n.Format("replay.live_status", window.GcdRemaining, window.CastRemaining,
            window.AnimationLock, window.ActionQueued, nativeStatus?.ToString() ?? "-",
            pending == null ? "-" : ActionLookup.GetName(pending.ActualActionId), age));
        if (!string.IsNullOrEmpty(_lastReplayUpdateError))
            Message(I18n.Format("replay.update_error", _lastReplayUpdateError));
    }
}
