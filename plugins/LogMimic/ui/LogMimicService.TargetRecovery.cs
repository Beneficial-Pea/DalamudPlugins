using FFXIVClientStructs.FFXIV.Client.Game;

namespace Oracle.Services;

internal sealed unsafe partial class LogMimicService
{
    private ReplayQueuedRequest? _queuedReplay;
    public string LastTargetRecovery { get; private set; } = string.Empty;

    private void CaptureQueuedReplay(ActionManager* manager, string cueId, uint actionId,
        ulong targetId, ReplayNativeWindow beforeWindow, long now)
    {
        var cast = ActionManager.GetAdjustedCastTime(ActionType.Action, actionId) / 1000f;
        var queued = ReplayQueuedTargetRecovery.Capture(cueId, actionId, targetId,
            ReplayNativeTiming.Traits(actionId), beforeWindow, ReplayNativeTiming.Read(manager),
            now + ReplayPacing.ConfirmationTimeoutMs(cast + 0.4f));
        if (queued != null) _queuedReplay = queued;
    }

    private bool IsReplayCuePending(string id) => _pendingMainId == id || _pendingIndependentId == id
        || _adaptive?.PendingGcd?.Step.CueId == id || _adaptive?.PendingOgcd?.Step.CueId == id;

    private void ConfirmQueuedReplay(string cueId)
    {
        if (_queuedReplay?.CueId == cueId) _queuedReplay = null;
    }

    private void RetryUnsentCue(string cueId)
    {
        _adaptive?.CancelPending(cueId);
        if (_pendingMainId == cueId) _pendingMainId = null;
        if (_pendingIndependentId == cueId) _pendingIndependentId = null;
        Feedback.CancelPending(cueId);
        _queuedReplay = null;
    }

    private void RecoverQueuedTarget()
    {
        if (_queuedReplay is not { } queued) return;
        var manager = ActionManager.Instance();
        if (manager == null) return;
        var window = ReplayNativeTiming.Read(manager);
        var target = queued.FollowSelection ? ResolveTarget(queued.ActionId, out _)
            : PluginServices.ObjectTable.SearchById(queued.TargetId);
        var usable = target is { IsDead: false, IsTargetable: true }
            && manager->GetActionStatus(ActionType.Action, queued.ActionId, target.GameObjectId, false, false) == 0;
        var decision = ReplayQueuedTargetRecovery.Decide(queued, window, target?.GameObjectId, usable,
            Environment.TickCount64, IsReplayCuePending(queued.CueId));
        switch (decision)
        {
            case ReplayQueueRecovery.Retarget:
                // Only this replay's unexecuted native queue is changed. No UseAction
                // call, hard-target write, cast cancellation or timeline advance occurs.
                manager->QueuedTargetId = target!.GameObjectId;
                _queuedReplay = queued with { TargetId = target.GameObjectId };
                Feedback.RetargetPending(queued.CueId, target.Name.TextValue);
                LastTargetRecovery = I18n.Format("target_recovery.retargeted", target.Name.TextValue);
                break;
            case ReplayQueueRecovery.AdoptNativeTarget:
                _queuedReplay = queued with { TargetId = window.QueuedTargetId };
                Feedback.RetargetPending(queued.CueId, target!.Name.TextValue);
                LastTargetRecovery = I18n.Format("target_recovery.retargeted", target.Name.TextValue);
                break;
            case ReplayQueueRecovery.CancelOwnedQueue:
                manager->ActionQueued = false;
                RetryUnsentCue(queued.CueId);
                LastTargetRecovery = I18n.Get("target_recovery.retry");
                break;
            case ReplayQueueRecovery.RetryUnsent:
                // A native cancellation or a player's replacement input is left intact.
                RetryUnsentCue(queued.CueId);
                LastTargetRecovery = I18n.Get("target_recovery.retry");
                break;
            case ReplayQueueRecovery.ForgetSent:
                _queuedReplay = null;
                break;
        }
    }
}
