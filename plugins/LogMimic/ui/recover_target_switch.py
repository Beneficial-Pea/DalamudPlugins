"""Recover owned, unexecuted input queues across manual enemy-target changes."""
import json
import shutil


def apply(root, assets):
    def rep(path, old, new):
        p = root / path
        s = p.read_text(encoding='utf-8')
        if s.count(old) != 1:
            raise RuntimeError(f'Target recovery anchor {path}: {old[:100]} ({s.count(old)})')
        p.write_text(s.replace(old, new), encoding='utf-8')

    for name in ['ReplayQueuedTargetRecovery.cs', 'LogMimicService.TargetRecovery.cs']:
        shutil.copyfile(assets / name, root / 'Oracle/Services' / name)
    svc = 'Oracle/Services/LogMimicService.cs'
    rep(svc, '        var now = Environment.TickCount64;\n        // One attempt',
        '        var now = Environment.TickCount64;\n        RecoverQueuedTarget();\n        // One attempt')
    rep(svc, '        LastProcSubstitution = string.Empty;',
        '        LastProcSubstitution = string.Empty;\n        _queuedReplay = null;\n        LastTargetRecovery = string.Empty;')
    rep(svc, '        var sent = manager->UseAction(ActionType.Action, actionId, targetId);',
        '        var beforeSendSequence = manager->LastUsedActionSequence;\n        var sent = manager->UseAction(ActionType.Action, actionId, targetId);')
    rep(svc, '                Scheduled(cue) - _playbackOffset, _engine.ElapsedSeconds, now);',
        '                Scheduled(cue) - _playbackOffset, _engine.ElapsedSeconds, now);\n            CaptureQueuedReplay(manager, cue.Id, actionId, targetId, beforeSendSequence, now);')
    for cue_id, indent in [('pending.Id', 20), ('step.CueId', 20), ('_main[_mainIndex].Id', 16)]:
        line = ' ' * indent + f'_engine.CompleteConfirmedReplayCue({cue_id});'
        rep(svc, line, line + '\n' + ' ' * indent + f'ConfirmQueuedReplay({cue_id});')
    adaptive = 'Oracle/Services/LogMimicService.Adaptive.cs'
    rep(adaptive, '        if (!string.IsNullOrEmpty(reason)) _state += "\\n" + reason;',
        '''        if (window.ActionQueued)
            _state += "\\n" + I18n.Format("target_recovery.native_queue", ActionLookup.GetName(window.QueuedActionId), window.QueuedTargetId);
        if (!string.IsNullOrEmpty(reason)) _state += "\\n" + reason;''')
    dtr = 'Oracle/Services/LogMimicDtrEntry.cs'
    rep(dtr, '        if (!string.IsNullOrEmpty(_replay.LastProcSubstitution))',
        '''        if (_replay.IsReplayEnabled)
            tooltip += "\\n" + I18n.Format("target_recovery.selected", PluginServices.TargetManager.Target?.Name.TextValue ?? I18n.Get("lm.none"));
        if (!string.IsNullOrEmpty(_replay.LastTargetRecovery)) tooltip += "\\n" + _replay.LastTargetRecovery;
        if (!string.IsNullOrEmpty(_replay.LastProcSubstitution))''')

    texts = {
        'target_recovery.retargeted': ('Queued attack target updated: {0}', '예약 중인 공격 대상 변경: {0}'),
        'target_recovery.retry': ('An unsent queued attack was cancelled. Retry the same record on the selected target.', '아직 실행되지 않은 공격 예약을 취소했습니다. 직접 선택한 대상에게 같은 기록을 다시 시도합니다.'),
        'target_recovery.native_queue': ('Native input queue: {0} / target ID {1}', '게임 입력 예약: {0} / 대상 ID {1}'),
        'target_recovery.selected': ('Current selection: {0}', '현재 선택한 대상: {0}'),
        'manual_target.help': ('Choose targets in game. Targeted actions use your current selection; self-only actions still use you. Without a target, attacks wait. An unsent replay attack queue follows your selection; actions already sent or casting keep their committed target.', '대상은 게임에서 직접 선택하세요. 대상 지정 스킬은 현재 선택한 대상에게, 자신 전용 스킬은 자신에게 사용합니다. 대상이 없으면 공격을 기다립니다. 아직 실행되지 않은 공격 예약은 선택한 대상을 따라가며, 이미 전송했거나 시전 중인 스킬은 기존 대상에게 나갑니다.'),
    }
    for lang, index in [('en', 0), ('ko', 1)]:
        p = root / f'Oracle/Data/I18n/{lang}.json'
        m = json.loads(p.read_text(encoding='utf-8'))
        m.update({key: value[index] for key, value in texts.items()})
        p.write_text(json.dumps(m, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    # Hard target selection remains under the player's control.
    for p in (root / 'Oracle/Services').glob('LogMimicService*.cs'):
        if 'TargetManager.Target =' in p.read_text(encoding='utf-8'):
            raise RuntimeError(f'Automatic hard-target write remains in {p}')
    print('Applied owned input-queue retargeting, cancellation recovery and bounded native queue waits.')
