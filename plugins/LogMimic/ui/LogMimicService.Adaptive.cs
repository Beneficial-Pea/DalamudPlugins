using FFXIVClientStructs.FFXIV.Client.Game;
using Oracle.Models;

namespace Oracle.Services;

internal sealed unsafe partial class LogMimicService
{
    private bool _adaptiveMode;
    private bool _adaptiveTargetBlocked;
    private ReplayAdaptiveQueue? _adaptive;
    private ReplayPhasePlan? _adaptivePhases;
    private int _adaptivePhaseIndex;
    private Dictionary<string, TimelineCue> _adaptiveCues = [];
    private readonly Dictionary<uint, float> _observedLocks = [];

    private void PrepareAdaptive()
    {
        _adaptiveMode = C.ReplayFollowCurrentGcd;
        _adaptiveCues = _main.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
        _adaptivePhaseIndex = 0;
        _adaptivePhases = new ReplayPhasePlan(_main.Select(c => new ReplayPhaseMark(c.Id, Scheduled(c),
            Math.Max(ActionTiming.GetCastSeconds(c.ActionId), ActionTiming.GetRecastSeconds(c.ActionId)))),
            _doc!.Cues.Where(c => c.IsClockSync).Select(c => new ReplayPhaseAnchor(c.Id, c.TimeOffsetSec)));
        _adaptive = _adaptiveMode ? new ReplayAdaptiveQueue(_main.Select(c =>
            (c.Id, c.ActionId, ReplayNativeTiming.IsGcd(c.ActionId), _adaptivePhases.Segments[c.Id]))) : null;
        if (_adaptiveMode) _playbackOffset = 0;
    }

    private TimelineCue? AdaptiveNextCue()
    {
        var step = _adaptive?.NextGcd ?? _adaptive?.NextOgcd;
        return step != null && _adaptiveCues.TryGetValue(step.CueId, out var cue) ? cue : null;
    }

    private float ExpectedLock(uint actionId)
    {
        var manager = ActionManager.Instance();
        var adjusted = manager == null ? actionId : manager->GetAdjustedActionId(actionId);
        return _observedLocks.GetValueOrDefault(adjusted, 0.6f);
    }

    private void UpdateAdaptive(long now)
    {
        var manager = ActionManager.Instance();
        if (manager == null || _adaptive == null) return;
        var window = ReplayNativeTiming.Read(manager);
        while (_adaptivePhases != null && _adaptivePhaseIndex < _adaptivePhases.Boundaries.Count)
        {
            var boundary = _adaptivePhases.Boundaries[_adaptivePhaseIndex];
            if (!boundary.CanRelease(_engine.ElapsedSeconds, _engine.HasObservedReplaySync)) break;
            _adaptive.ReleasePhase(boundary.Segment, boundary.SyncCueId != null);
            _adaptivePhaseIndex++;
        }
        _adaptive.Expire(now, window);
        // Explicitly detached utility/mitigation keeps encounter-time scheduling.
        foreach (var cue in _independent)
        {
            if (_independentDone.Contains(cue.Id)) continue;
            if (PlaybackElapsed < Scheduled(cue)) break;
            if (PlaybackElapsed > Scheduled(cue) + 5f) { _independentDone.Add(cue.Id); continue; }
            if (_pendingIndependentId != null) break;
            var canTry = ReplayNativeTiming.IsGcd(cue.ActionId) ? window.CanQueueGcd
                : window.CanWeave(false, ExpectedLock(cue.ActionId));
            if (canTry && Attempt(cue, now, true)) return;
        }

        var reason = string.Empty;
        var targetAvailable = true;
        if (_adaptive.NextGcd is { } nextGcd)
        {
            var next = _adaptiveCues[nextGcd.CueId];
            if (!ReplayNativeTiming.UsesSelf(next.ActionId, next.ReplayTargetLogId))
            {
                var candidate = ResolveTarget(next, out var targetReason);
                targetAvailable = targetReason == null;
                if (targetAvailable && ReplayNativeTiming.Traits(next.ActionId).Hostile && candidate == null)
                    targetAvailable = PluginServices.TargetManager.Target is { IsDead: false, IsTargetable: true };
                if (!targetAvailable) reason = I18n.Format("replay.target", next.ReplayTargetLogId, next.ReplayTargetName,
                    targetReason ?? I18n.Get("replay.not_spawned"));
            }
        }
        var dispatched = _adaptive.Dispatch(window, now, step =>
        {
            var cue = _adaptiveCues[step.CueId];
            var beforeSequence = manager->LastUsedActionSequence;
            var sent = Attempt(cue, now, false);
            if (!sent) { reason = _state; return new(false, TargetBlocked: _adaptiveTargetBlocked); }
            var request = Feedback.LastRequest!;
            var cast = Math.Max(0, ActionManager.GetAdjustedCastTime(ActionType.Action, request.ActionId) / 1000f);
            return new(true, request.ActionId, ReplayPacing.ConfirmationTimeoutMs(cast + 0.4f),
                NativeQueued: manager->ActionQueued && manager->QueuedActionId == request.ActionId,
                BeforeUseSequence: beforeSequence);
        }, step => ExpectedLock(step.ActionId), targetAvailable);
        _mainIndex = _adaptive.CompletedCount;
        if (dispatched) return;
        // A timed defensive can still be needed during untargetable downtime or after
        // the damage queue ends. Give a ready GCD priority, then allow the timed action.
        if (_pendingIndependentId == null && window.GcdRemaining <= 0.001f && window.CanWeave(true))
        {
            foreach (var cue in _independent)
            {
                if (_independentDone.Contains(cue.Id)) continue;
                if (PlaybackElapsed < Scheduled(cue)) break;
                if (!ReplayNativeTiming.IsGcd(cue.ActionId) && Attempt(cue, now, true)) return;
            }
        }
        if (_adaptive.Finished) { _state = I18n.Get("replay.finished"); return; }
        if (_adaptive.NextGcd == null && _adaptivePhases != null && _adaptivePhaseIndex < _adaptivePhases.Boundaries.Count)
        {
            _state = I18n.Format("adaptive.phase_wait", _adaptivePhases.Boundaries[_adaptivePhaseIndex].At);
            return;
        }
        string Name(ReplayAdaptiveStep? step) => step == null ? "-" : ActionLookup.GetName(step.ActionId);
        _state = I18n.Format("adaptive.wait", Name(_adaptive.NextGcd), Name(_adaptive.NextOgcd),
            window.GcdRemaining, window.CastRemaining);
        if (!string.IsNullOrEmpty(reason)) _state += "\n" + reason;
    }

    private void SkipAdaptive()
    {
        if (_adaptive?.Skip() == true)
        {
            _mainIndex = _adaptive.CompletedCount;
            Message(I18n.Get("adaptive.skipped"));
        }
        else Message(I18n.Get("adaptive.pending"));
    }
}
