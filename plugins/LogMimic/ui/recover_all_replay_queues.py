"""Track every owned native input, bound its lifetime, and retain stall evidence."""
import json
import shutil


def apply(root, assets):
    for name in ['ReplayStallHistory.cs', 'LogMimicService.Diagnostics.cs']:
        shutil.copyfile(assets / name, root / 'Oracle/Services' / name)

    def rep(path, old, new):
        p = root / path
        source = p.read_text(encoding='utf-8')
        if source.count(old) != 1:
            raise RuntimeError(f'Owned input recovery anchor {path}: {old[:100]} ({source.count(old)})')
        p.write_text(source.replace(old, new), encoding='utf-8')

    svc = 'Oracle/Services/LogMimicService.cs'
    rep(svc, 'CaptureQueuedReplay(manager, cue.Id, actionId, targetId, beforeSendSequence, now);',
        'CaptureQueuedReplay(manager, cue.Id, actionId, targetId, beforeWindow, now);')
    rep(svc, '        var sent = ReplayNativeRequest.Accepted(returned, beforeWindow, ReplayNativeTiming.Read(manager), actionId, targetId);',
        '        var sent = ReplayNativeRequest.Accepted(returned, beforeWindow, ReplayNativeTiming.Read(manager), actionId, targetId);\n        _lastNativeAccepted = sent;')
    rep(svc, '        var beforeSendSequence = manager->LastUsedActionSequence;\n', '')
    rep(svc, '        _conditionWait.Reset();\n        LastConditionSkip = string.Empty;',
        '        _conditionWait.Reset();\n        _stallHistory.ResetActive();\n        _lastNativeAccepted = null;\n        LastConditionSkip = string.Empty;')
    rep(svc, '        while (_received.TryDequeue(out var used))\n        {',
        '        while (_received.TryDequeue(out var used))\n        {\n            RecordLateReplayConfirmation(used);')
    rep('Oracle/Services/LogMimicService.TargetRecovery.cs', '            case ReplayQueueRecovery.CancelOwnedQueue:\n                manager->ActionQueued = false;',
        '            case ReplayQueueRecovery.CancelOwnedQueue:\n                SaveReplayDiagnostic("owned input queue recovery");\n                manager->ActionQueued = false;')
    rep('Oracle/Plugin.cs', '        _engine.Update();\n        _replay.Update();',
        '''        try { _engine.Update(); }
        catch (Exception ex) { _replay.RecordEngineUpdateError(ex); throw; }
        _replay.Update();''')
    for lang, message in [('en', 'Last recorded input stall (retained after recovery): {0}'),
                          ('ko', '최근 자동 입력 대기 기록 (재개 후에도 유지): {0}')]:
        p = root / f'Oracle/Data/I18n/{lang}.json'
        messages = json.loads(p.read_text(encoding='utf-8'))
        messages['replay.last_stall'] = message
        messages['target_recovery.retry'] = ('Released an unsent replay input. Retrying its original record.' if lang == 'en'
            else '아직 실행되지 않은 재생 입력 예약을 해제했습니다. 같은 기록부터 다시 시도합니다.')
        p.write_text(json.dumps(messages, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('Applied self/ally queue ownership, bounded unsent input recovery and retained local stall history.')
