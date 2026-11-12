"""Use the live GCD/queue window, separate ability waits, and remove the floating HUD."""
import json
import shutil


def apply(root, assets):
    def rep(path, old, new):
        p = root / path
        s = p.read_text(encoding='utf-8')
        if s.count(old) != 1:
            raise RuntimeError(f'Adaptive anchor {path}: {old[:100]} ({s.count(old)})')
        p.write_text(s.replace(old, new), encoding='utf-8')

    for name in ['ReplayAdaptiveQueue.cs', 'ReplayPhasePlan.cs', 'ReplayNativeTiming.cs', 'LogMimicService.Adaptive.cs']:
        shutil.copyfile(assets / name, root / 'Oracle/Services' / name)
    rep('Oracle/Configuration.cs', '    public int Version { get; set; } = 1;',
        '    public int Version { get; set; } = 1;\n    public bool ReplayFollowCurrentGcd { get; set; } = true;')
    rep('Oracle/Plugin.cs', '        _windowSystem.AddWindow(new LogMimicStatusWindow(_replay));\n', '')
    rep('Oracle/Services/TimelineEngine.cs', '    public bool IsRunning => _running;',
        '    public bool IsRunning => _running;\n    public bool HasObservedReplaySync(string cueId) => _appliedAnchorCueIds.Contains(cueId);')

    detector = 'Oracle/Services/Sync/ActionUseDetector.cs'
    rep(detector, '    public event Action<uint, uint>? ActionUsed;',
        '    public event Action<uint, uint>? ActionUsed;\n    public event Action<uint, uint>? ActionConfirmed;')
    rep(detector, '        ActionUsed?.Invoke(actionId, targetJobId);',
        '        ActionConfirmed?.Invoke(actionId, header->SourceSequence);\n        ActionUsed?.Invoke(actionId, targetJobId);')

    svc = 'Oracle/Services/LogMimicService.cs'
    rep(svc, '    private readonly ConcurrentQueue<uint> _received = new();',
        '    private readonly ReplayConfirmationQueue _received = new();')
    rep(svc, '        _engine.ActionUse.ActionUsed += OnActionUsed;',
        '        _engine.ActionUse.ActionConfirmed += OnActionUsed;')
    rep(svc, '    public void Dispose() => _engine.ActionUse.ActionUsed -= OnActionUsed;',
        '    public void Dispose() => _engine.ActionUse.ActionConfirmed -= OnActionUsed;')
    rep(svc, '    private void OnActionUsed(uint actionId, uint targetJobId) => _received.Enqueue(actionId);',
        '    private void OnActionUsed(uint actionId, uint sourceSequence) => _received.Enqueue(actionId, sourceSequence);')
    rep(svc, '        if (player == null || player.IsDead)', '''        if (player == null)
        {
            _state = I18n.Get("adaptive.player_wait");
            return;
        }
        if (player.IsDead)''')
    # A server confirmation from a request sent before pausing must still be consumed.
    rep(svc, '        DrainConfirmations();\n\n        var elapsed', '        var elapsed')
    rep(svc, '            _suspended = false;\n        }\n        if (_suspended)',
        '            _suspended = false;\n        }\n        DrainConfirmations();\n        if (_suspended)')
    rep(svc, 'if (now - _lastAttempt < 130) return;', 'if (now - _lastAttempt < (_adaptiveMode ? 40 : 130)) return;')
    rep(svc, '        // Independent mitigation lane does not wait for the blocked DPS lane.',
        '        if (_adaptiveMode) { UpdateAdaptive(now); return; }\n\n        // Independent mitigation lane does not wait for the blocked DPS lane.')
    rep(svc, '        PrepareFeedback();', '        PrepareFeedback();\n        PrepareAdaptive();')
    rep(svc, '        _doc = null;',
        '        _doc = null;\n        _adaptive = null;\n        _adaptiveMode = false;\n        _adaptiveCues.Clear();')
    rep(svc, '        _suspended = true;\n        while (_received.TryDequeue(out _)) { }\n    }\n\n    private void DrainConfirmations()',
        '        _suspended = true;\n        _received.Clear();\n    }\n\n    private void DrainConfirmations()')
    rep(svc, 'if (pending != null && Matches(pending.ActionId, used))',
        'if (pending != null && Feedback.MatchesRequest(pending.Id, used))')
    rep(svc, '            if (_mainIndex < _main.Count && _pendingMainId == _main[_mainIndex].Id && Matches(_main[_mainIndex].ActionId, used))',
        '''            if (_adaptiveMode)
            {
                if (_adaptive?.Confirm(used) is { } step)
                {
                    Feedback.Confirmed(step.CueId, used, _engine.ElapsedSeconds, Environment.TickCount64);
                    _mainIndex = _adaptive.CompletedCount;
                }
                continue;
            }
            if (_mainIndex < _main.Count && _pendingMainId == _main[_mainIndex].Id && Feedback.MatchesRequest(_main[_mainIndex].Id, used))''')
    rep(svc, '''    private static bool Matches(uint expected, uint used)
    {
        if (expected == used) return true;
        var manager = ActionManager.Instance();
        return manager != null && manager->GetAdjustedActionId(expected) == used;
    }

''', '')

    rep(svc, '''        Feedback.LastGameStatus = 0;
        var target = ResolveTarget(cue, out var reason);''', '''        Feedback.LastGameStatus = 0;
        _adaptiveTargetBlocked = false;
        var manager = ActionManager.Instance();
        if (manager == null) return false;
        var actionId = manager->GetAdjustedActionId(cue.ActionId);
        if (actionId == 0) actionId = cue.ActionId;
        var selfTarget = ReplayNativeTiming.UsesSelf(actionId, cue.ReplayTargetLogId);
        string? reason = null;
        var target = selfTarget ? PluginServices.ObjectTable.LocalPlayer : ResolveTarget(cue, out reason);''')
    rep(svc, '        if (reason != null)\n        {',
        '        if (reason != null)\n        {\n            _adaptiveTargetBlocked = true;')
    # This block has already been moved ahead of target resolution above.
    rep(svc, '''        var manager = ActionManager.Instance();
        if (manager == null) return false;
        var actionId = manager->GetAdjustedActionId(cue.ActionId);
        if (actionId == 0) actionId = cue.ActionId;
        // Status != 0''', '''        // Status != 0''')
    rep(svc, '''        if (target != null && cue.ReplayTargetKind == "enemy")
            PluginServices.TargetManager.Target = target;
''', '')
    rep(svc, '        var gameStatus = manager->GetActionStatus(ActionType.Action, actionId, targetId);',
        '''        var queueGcd = _adaptiveMode && ReplayNativeTiming.IsGcd(actionId);
        if (_adaptiveMode)
        {
            var window = ReplayNativeTiming.Read(manager);
            if (queueGcd ? !window.CanQueueGcd : window.ActionQueued || window.AnimationLock > 0.001f || window.CastRemaining > 0.001f)
            {
                _state = I18n.Get("adaptive.busy");
                return false;
            }
        }
        // Only the native GCD queue window may ignore recast/casting checks here.
        // UseAction itself still validates cooldowns, charges, range and resources.
        var gameStatus = manager->GetActionStatus(ActionType.Action, actionId, targetId, !queueGcd, !queueGcd);''')
    rep(svc, '            _state = I18n.Format("replay.retrying", ActionLookup.GetName(cue.ActionId), I18n.Get(independent ? "replay.independent" : "replay.strict"));',
        '''            _state = _adaptiveMode
                ? I18n.Format("adaptive.blocked", ActionLookup.GetName(cue.ActionId), gameStatus)
                : I18n.Format("replay.retrying", ActionLookup.GetName(cue.ActionId), I18n.Get(independent ? "replay.independent" : "replay.strict"));''')
    rep(svc, '        _lastAttempt = now;\n        var clickSlots',
        '        if (target != null && !selfTarget && cue.ReplayTargetKind == "enemy")\n            PluginServices.TargetManager.Target = target;\n        _lastAttempt = now;\n        var clickSlots')
    rep(svc, '            HotbarFeedback?.StartReplayClick(clickSlots, now);',
        '            HotbarFeedback?.StartReplayClick(clickSlots, now);\n            if (manager->AnimationLock > 0) _observedLocks[actionId] = manager->AnimationLock;')
    rep(svc, '            else\n            {\n                _pendingMainId = cue.Id;',
        '            else if (!_adaptiveMode)\n            {\n                _pendingMainId = cue.Id;')
    rep(svc, 'cue.ReplayTargetKind == "default" || cue.ReplayTargetLogId == 0',
        'cue.ReplayTargetKind == "default" || cue.ReplayTargetLogId <= 0')
    rep(svc, '            case "skip":',
        '            case "skip":\n                if (_adaptiveMode) { SkipAdaptive(); break; }')
    imp = 'Oracle/Services/FFLogs/FFLogsImportService.cs'
    rep(imp, 'ReplayTargetKind = cast.TargetId == 0 ? "default"',
        'ReplayTargetKind = cast.TargetId <= 0 ? "default"')
    # Existing documents also get a correct label without re-importing their logs.
    labels = 'Oracle/UI/ConfigWindow.TargetLabels.cs'
    p = root / labels
    s = p.read_text(encoding='utf-8')
    s = s.replace('cue.ReplayTargetLogId == 0', 'cue.ReplayTargetLogId <= 0')
    s = s.replace('cue.ReplayTargetLogId != 0', 'cue.ReplayTargetLogId > 0')
    marker = '    private static string RecordedTargetLabel(TimelineCue cue)\n    {'
    if s.count(marker) != 1:
        raise RuntimeError('Recorded target label anchor changed')
    s = s.replace(marker, marker + '\n        if (ReplayNativeTiming.UsesSelf(cue.ActionId, cue.ReplayTargetLogId)) return I18n.Get("lm.target_self");')
    p.write_text(s, encoding='utf-8')

    dtr = 'Oracle/Services/LogMimicDtrEntry.cs'
    rep(dtr, '        var tooltip = I18n.Get("dtr.help") + "\\n\\n" + _replay.State;',
        '''        var tooltip = I18n.Get("dtr.help") + "\\n\\n" + _replay.State;
        if (_replay.Feedback.LastConfirmed is { } confirmed)
            tooltip += "\\n" + I18n.Format("dtr.last_confirmed", ActionLookup.GetName(confirmed.ActionId), confirmed.ConfirmedAt ?? 0);''')
    texts = {
        'adaptive.enable': ('Follow my current GCD', '현재 글쿨에 맞춰 재생'),
        'adaptive.help': ('Uses the live GCD and native queue window. GCD order is preserved; usable abilities weave between GCDs. Long recorded gaps keep encounter timing, and configured sync cues gate the next phase. Explicit independent timers retain encounter time. Change before starting replay.', '실제 남은 글쿨과 게임의 입력 예약 구간을 사용합니다. 글쿨은 순서대로, 사용 가능한 논글쿨은 사이에 넣습니다. 긴 대기 구간은 전투 시간을 유지하고, 해당 구간에 동기화 신호를 설정했다면 실제 신호를 기다립니다. 별도 스킬 타이머도 전투 시간을 유지합니다. 재생 전에 설정하세요.'),
        'adaptive.wait': ('GCD: {0} / ability: {1} / recast {2:0.00}s / cast {3:0.00}s', '글쿨: {0} / 논글쿨: {1} / 남은 글쿨 {2:0.00}초 / 시전 {3:0.00}초'),
        'adaptive.blocked': ('Waiting for {0} (game status {1})', '{0} 사용 조건 대기 (게임 상태 {1})'),
        'adaptive.busy': ('Waiting for casting, animation lock or the queued action.', '시전·스킬 동작 또는 이미 예약된 입력을 기다리는 중입니다.'),
        'adaptive.pending': ('Wait for the pending action confirmation before skipping.', '요청한 스킬의 사용 확인을 받은 뒤 건너뛸 수 있습니다.'),
        'adaptive.skipped': ('Skipped the next replay action.', '다음 재생 스킬을 건너뛰었습니다.'),
        'adaptive.player_wait': ('Waiting for the local player.', '플레이어 정보를 확인하는 중입니다.'),
        'adaptive.phase_wait': ('Waiting for the next timeline section / phase sync ({0:0.00}s).', '다음 타임라인 구간·페이즈 동기화 대기 ({0:0.00}초).'),
        'dtr.last_confirmed': ('Last confirmed: {0} / {1:0.00}s', '마지막 사용 확인: {0} / {1:0.00}초'),
    }
    for lang, index in [('en', 0), ('ko', 1)]:
        p = root / f'Oracle/Data/I18n/{lang}.json'
        m = json.loads(p.read_text(encoding='utf-8'))
        m.update({key: value[index] for key, value in texts.items()})
        p.write_text(json.dumps(m, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    manifest = root / 'Oracle/LogMimic.json'
    m = json.loads(manifest.read_text(encoding='utf-8'))
    m['Punchline'] = 'Replay FFLogs action order with live GCD pacing.'
    m['Description'] = ('Experimental FFLogs replay with live GCD pacing, separate ability waits, '
                        'target matching, timed utility and LM: ON/OFF control. '
                        'In-game Korean/global client compatibility requires verification.')
    manifest.write_text(json.dumps(m, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Applied live GCD pacing, separate ability waits, self-target support and no floating HUD.')
