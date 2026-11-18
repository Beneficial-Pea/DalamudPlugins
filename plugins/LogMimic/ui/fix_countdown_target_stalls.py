"""Arm on countdown start and recover unavailable actions on the manual target."""
import json
import shutil


def apply(root, assets):
    def rep(path, old, new):
        p = root / path
        text = p.read_text(encoding='utf-8')
        if text.count(old) != 1:
            raise RuntimeError(f'Countdown/condition anchor {path}: {old[:100]} ({text.count(old)})')
        p.write_text(text.replace(old, new), encoding='utf-8')

    for name in ['ReplayActionCondition.cs', 'ReplayNativeRequest.cs', 'LogMimicService.Conditions.cs']:
        shutil.copyfile(assets / name, root / 'Oracle/Services' / name)
    svc = 'Oracle/Services/LogMimicService.cs'
    rep(svc, '        _clockHold = default;',
        '        _clockHold = default;\n        _conditionWait.Reset();\n        LastConditionSkip = string.Empty;')
    rep(svc, '        skippedCooldown = false;',
        '        skippedCooldown = false;\n        _skippedCondition = false;')
    rep(svc, '        _engine.SetReplayTimelineHeld(WantsTimelineHold());',
        '''        _engine.SetReplayTimelineHeld(WantsTimelineHold());
        if (_suspended || _clockHold.BlockMainActions || !ReplayStartGate.CanExecuteAt(_engine.ElapsedSeconds))
            _conditionWait.Reset();''')
    rep(svc, '        if (reason != null)\n        {',
        '        if (reason != null)\n        {\n            _conditionWait.Forget(cue.Id);')
    rep(svc, '                _state = I18n.Get("adaptive.busy");',
        '                _conditionWait.Forget(cue.Id);\n                _state = I18n.Get("adaptive.busy");')
    rep(svc, '        var gameStatus = manager->GetActionStatus(ActionType.Action, actionId, targetId, !queueGcd, !queueGcd);',
        '''        if (AttackTargetBlockReason(actionId, target) is { } targetBlock)
        {
            _conditionWait.Forget(cue.Id);
            _adaptiveTargetBlocked = true;
            _state = targetBlock;
            return false;
        }
        var gameStatus = manager->GetActionStatus(ActionType.Action, actionId, targetId, !queueGcd, !queueGcd);''')
    rep(svc, '        Feedback.LastGameStatus = gameStatus;\n        if (gameStatus != 0)',
        '''        Feedback.LastGameStatus = gameStatus;
        if (_conditionWait.ShouldSkip(cue.Id, actionId, targetId, gameStatus, now))
        {
            _skippedCondition = true;
            skippedCooldown = true;
            LastConditionSkip = I18n.Format("condition.skipped", ActionLookup.GetName(actionId), gameStatus);
            _state = LastConditionSkip;
            return false;
        }
        if (gameStatus != 0)''')
    rep(svc, '        var beforeSendSequence = manager->LastUsedActionSequence;\n        var sent = manager->UseAction(ActionType.Action, actionId, targetId);',
        '''        var beforeWindow = ReplayNativeTiming.Read(manager);
        var beforeSendSequence = manager->LastUsedActionSequence;
        var returned = manager->UseAction(ActionType.Action, actionId, targetId);
        var sent = ReplayNativeRequest.Accepted(returned, beforeWindow, ReplayNativeTiming.Read(manager), actionId, targetId);''')
    adaptive = 'Oracle/Services/LogMimicService.Adaptive.cs'
    rep(adaptive, '        var window = ReplayNativeTiming.Read(manager);',
        '        var window = ReplayNativeTiming.Read(manager);\n        if (!window.CanQueueGcd || _adaptive.PendingGcd != null || _clockHold.BlockMainActions) _conditionWait.Reset();')
    rep(adaptive, '            if (skippedCooldown) return new(false, SkipCooldown: true);',
        '            if (skippedCooldown) return new(false, SkipCooldown: !_skippedCondition, SkipCondition: _skippedCondition);')
    for path in [svc, adaptive]:
        p = root / path
        p.write_text(p.read_text(encoding='utf-8').replace('skippedCooldown', 'skippedUnavailable'), encoding='utf-8')
    rep('Oracle/Services/LogMimicService.ClockHold.cs', '                blocked = !skip && (reason != null || target == null',
        '                blocked = !skip && (reason != null || target == null || AttackTargetBlockReason(id,target) != null')
    rep('Oracle/Services/LogMimicDtrEntry.cs', '        if (!string.IsNullOrEmpty(_replay.LastCooldownSkip))',
        '        if (!string.IsNullOrEmpty(_replay.LastConditionSkip)) tooltip += "\\n" + _replay.LastConditionSkip;\n        if (!string.IsNullOrEmpty(_replay.LastCooldownSkip))')

    texts = {
        'lm.auto_start': ('Automatically turn ON when countdown begins', '초읽기 시작 시 자동 ON'),
        'replay.wait_zero': ('LM is ON. Waiting for 0s; use pre-pull preparations manually.', 'LM은 ON 상태입니다. 0초까지 대기하며, 0초 전 준비는 직접 사용하세요.'),
        'replay.loaded': ('Loaded {0} main + {1} independent actions. Ready to arm on countdown or manual enable.', '메인 {0}개 + 별도 스킬 {1}개 불러옴. 초읽기 시작 또는 수동 ON 대기.'),
        'lm.intro': ('Select an imported preset. Countdown auto-start turns LM ON immediately; action replay begins at 0s.', '가져온 프리셋을 선택하세요. 초읽기가 뜨면 LM이 바로 ON이 되고, 스킬 재생은 0초부터 시작합니다.'),
        'lm.manual': ('With countdown auto-start enabled, LM turns ON when countdown begins and waits until 0s to use actions. Use pre-pull actions manually. OFF, death or countdown cancellation stops the session. Without a countdown, enable replay manually.', '초읽기 자동 ON을 켜면 초읽기가 뜰 때 LM이 켜지고, 스킬은 0초부터 사용합니다. 0초 전 준비는 직접 누르세요. OFF·사망·초읽기 취소 시 중지되며, 초읽기가 없으면 직접 ON으로 켜세요.'),
        'condition.skipped': ('Skipped missing proc/gauge/combo requirement: {0} (native status {1}). No use was confirmed.', '버프·게이지·콤보 조건이 없어 건너뜀: {0} (게임 상태 {1}). 사용 확인으로 처리하지 않습니다.'),
        'manual_target.unusable': ('The selected target cannot currently receive this attack: {0}. Waiting without changing your selection.', '현재 선택한 대상에게 이 공격을 사용할 수 없습니다: {0}. 선택한 대상을 유지하며 기다립니다.'),
        'manual_target.range': ('Waiting for range or line of sight: {0} → {1} (native status {2}).', '거리·시야 대기: {0} → {1} (게임 상태 {2}).'),
    }
    for lang, index in [('en', 0), ('ko', 1)]:
        p = root / f'Oracle/Data/I18n/{lang}.json'
        m = json.loads(p.read_text(encoding='utf-8'))
        m.update({key: value[index] for key, value in texts.items()})
        p.write_text(json.dumps(m, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    p = root / 'Oracle/LogMimic.json'
    m = json.loads(p.read_text(encoding='utf-8'))
    m['Description'] = m['Description'].replace('공격 타임라인 대기 일시정지 · 전투 시작 후 다운타임 준비기 재생',
        '초읽기 시작 시 자동 ON · 직접 선택한 대상 재생 · 사용 조건 대기 복구')
    p.write_text(json.dumps(m, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('Applied countdown arming, native queue acceptance, missing-condition recovery and manual-target diagnostics.')
