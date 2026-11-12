"""Patch the already localized standalone source with countdown-driven replay."""
import json, shutil

def apply(root, assets):

    def rep(path, old, new):
        p = root / path
        s = p.read_text(encoding='utf-8')
        if s.count(old) != 1:
            raise RuntimeError(f'Countdown patch anchor: {path}: {old[:90]} ({s.count(old)})')
        p.write_text(s.replace(old, new), encoding='utf-8')
    for name in ['ReplayStartGate.cs', 'CountdownSignals.cs', 'NativeCountdownSignals.cs']:
        shutil.copyfile(assets / name, root / 'Oracle/Services' / name)
    shutil.copyfile(assets / 'CountdownSyncDetector.cs', root / 'Oracle/Services/Sync/CountdownSyncDetector.cs')
    rep('Oracle/Configuration.cs', '    public int Version { get; set; } = 1;', '    public int Version { get; set; } = 1;\n    public bool ReplayAutoStartOnCountdown { get; set; } = true;')
    engine = 'Oracle/Services/TimelineEngine.cs'
    rep(engine, '    public bool IsRunning => _running;', '    public long CountdownSequence { get; private set; }\n    public bool IsCountdownSession { get; private set; }\n    public bool IsRunning => _running;')
    rep(engine, '        _previewMode = preview;\n        Activate(doc, clockOffset);', '        IsCountdownSession = false;\n        _previewMode = preview;\n        Activate(doc, clockOffset);')
    rep(engine, '    private void StopClock()\n    {', '    private void StopClock()\n    {\n        IsCountdownSession = false;')
    rep(engine, '        _countdown.Update();\n        if (!_countdown.JustStarted)', '        _countdown.Update();\n        if (_countdown.JustCancelled && IsCountdownSession && !_combat.InCombat)\n            StopClock();\n        if (!_countdown.JustStarted)')
    rep(engine, '        if (!_countdown.JustStarted)\n            return;', '''        if (IsCountdownSession && _running && !_previewMode && _countdown.ClockRemaining is { } remaining)
        {
            _syncUtc = DateTime.UtcNow;
            _clockOffset = -remaining;
        }
        if (!_countdown.JustStarted)
            return;''')
    rep(engine, '        StartClock(_activeDoc, -Math.Abs(_countdown.StartedRemaining), preview: false);', '        if (_combat.InCombat) return;\n        StartClock(_activeDoc, -Math.Abs(_countdown.StartedRemaining), preview: false);\n        CountdownSequence++;\n        IsCountdownSession = true;')
    svc = 'Oracle/Services/LogMimicService.cs'
    rep(svc, '    private bool _suspended = true;', '    private bool _suspended = true;\n    private long _replayCountdownSequence = -1;\n    private readonly ReplayStartGate _autoStart = new();')
    rep(svc, '            if (_doc != null) Reset();\n            _state = I18n.Get("replay.inactive");', '            _autoStart.Observe(_engine.CountdownSequence, _engine.IsCountdownSession, false, C.ReplayAutoStartOnCountdown, _engine.ElapsedSeconds);\n            if (_doc != null) Reset();\n            _state = I18n.Get("replay.inactive");')
    rep(svc, '        var currentJobId = PluginServices.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;', '        var player = PluginServices.ObjectTable.LocalPlayer;\n        if (player == null || player.IsDead)\n        {\n            _autoStart.Pause(_engine.CountdownSequence);\n            _suspended = true;\n            _pendingMainId = null;\n            _pendingIndependentId = null;\n            while (_received.TryDequeue(out _)) { }\n            _state = I18n.Get("replay.dead");\n            return;\n        }\n        var currentJobId = player.ClassJob.RowId;')
    rep(svc, '            _state = I18n.Format("replay.job", active.ClassJobId, currentJobId);', '            _autoStart.Observe(_engine.CountdownSequence, _engine.IsCountdownSession, false, C.ReplayAutoStartOnCountdown, _engine.ElapsedSeconds);\n            _state = I18n.Format("replay.job", active.ClassJobId, currentJobId);')
    rep(svc, '        if (!ReferenceEquals(_doc, active)) Begin(active);', '        if (!ReferenceEquals(_doc, active) || (_engine.IsCountdownSession && _replayCountdownSequence != _engine.CountdownSequence)) Begin(active);\n        if (_autoStart.Observe(_engine.CountdownSequence, _engine.IsCountdownSession, true, C.ReplayAutoStartOnCountdown, _engine.ElapsedSeconds))\n        {\n            _suspended = false;\n        }')
    rep(svc, '        _doc = doc;', '        _doc = doc;\n        _replayCountdownSequence = _engine.CountdownSequence;')
    rep(svc, '            case "on":', '            case "on":\n                if (PluginServices.ObjectTable.LocalPlayer is not { IsDead: false })\n                {\n                    Message(I18n.Get("replay.dead"));\n                    break;\n                }')
    rep(svc, '            case "off":\n                _suspended = true;', '            case "off":\n                _autoStart.Pause(_engine.CountdownSequence);\n                _suspended = true;')
    updates = {'replay.need_fight': ('Load a replay preset and start a countdown or combat first.', '재생 프리셋을 불러온 뒤 초읽기 또는 전투를 시작하세요.'), 'replay.dead': ('Replay paused: player is dead or unavailable. Reviving does not resume this countdown.', '사망했거나 플레이어를 확인할 수 없어 재생을 중지했습니다. 부활만으로는 다시 시작하지 않습니다.'), 'replay.loaded': ('Loaded {0} main + {1} independent actions. Waiting for 0s or manual enable.', '메인 {0}개 + 별도 스킬 {1}개 불러옴. 0초 또는 수동 재생 대기.'), 'lm.auto_start': ('Automatically replay at countdown 0s', '초읽기 종료(0초) 시 자동 재생'), 'lm.manual': ('Replay waits during the countdown and starts at 0s, following each recorded action time. Actions before 0s are excluded. Pause and death stop replay for the current countdown. Without a countdown, enable replay manually.', '초읽기 동안 대기하고 0초부터 각 스킬의 기록 시각대로 재생합니다. 0초 이전 항목은 제외합니다. 중지 또는 사망 시 현재 초읽기에서는 자동으로 다시 켜지지 않습니다. 초읽기가 없으면 직접 재생을 켜세요.'), 'lm.intro': ('Import a log and select a preset. With automatic replay enabled, replay starts when the countdown reaches 0s.', '로그를 가져와 프리셋을 선택하세요. 자동 재생이 켜져 있으면 초읽기가 끝나는 0초부터 재생합니다.'), 'lm.autoload_help': ('Load the matching zone/job/phase preset. One preset per match; no automatic switching during combat. Countdown auto-start is configured separately.', '지역·직업·페이즈가 일치하면 프리셋을 불러옵니다. 같은 조건에는 하나만 지정할 수 있고 전투 중에는 자동 교체하지 않습니다. 초읽기 자동 재생은 별도 옵션입니다.'), 'replay.wait_zero': ('Waiting for countdown 0s', '초읽기 종료(0초) 대기 중'), 'lm.target_self': ('Self', '자신'), 'lm.target_unknown': ('No target data', '대상 정보 없음'), 'lm.target_actor': ('Actor #{0}', '대상 #{0}'), 'lm.recorded_target_tooltip': ('Recorded FFLogs target: {0}\nFFLogs actor ID: {1}', '로그에 기록된 대상: {0}\nFFLogs 대상 ID: {1}')}
    for lang, index in [('en', 0), ('ko', 1)]:
        p = root / f'Oracle/Data/I18n/{lang}.json'
        m = json.loads(p.read_text(encoding='utf-8'))
        m.update({k: v[index] for k, v in updates.items()})
        p.write_text(json.dumps(m, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Applied 0s countdown replay, cancellation/death pause and recorded target labels.')
    rep(svc, 'doc.Cues.Where(c => c.Kind == TimelineCueKind.Action && c.ActionId != 0)', 'doc.Cues.Where(c => c.Kind == TimelineCueKind.Action && c.ActionId != 0 && ReplayStartGate.IsCombatAction(c.TimeOffsetSec))')
    rep(svc, '            _state = I18n.Get("replay.paused");', '            _state = C.ReplayAutoStartOnCountdown && _engine.IsCountdownSession && !_autoStart.IsHandled(_engine.CountdownSequence) && _engine.ElapsedSeconds < 0\n                ? I18n.Get("replay.wait_zero") : I18n.Get("replay.paused");')
    rep(svc, '        DrainConfirmations();', '        if (!ReplayStartGate.CanExecuteAt(_engine.ElapsedSeconds))\n        {\n            while (_received.TryDequeue(out _)) { }\n            _state = I18n.Get("replay.wait_zero");\n            return;\n        }\n        DrainConfirmations();')
    shutil.copyfile(assets / 'ConfigWindow.TargetLabels.cs', root / 'Oracle/UI/ConfigWindow.TargetLabels.cs')
    table = 'Oracle/UI/ConfigWindow.CueTable.cs'
    rep(table, '            ref jobId,\n            ref role);', '            ref jobId,\n            ref role,\n            RecordedTargetLabel(cue),\n            RecordedTargetTooltip(cue));')
    rep(table, '        ref CueTargetRole targetRole)\n    {', '        ref CueTargetRole targetRole,\n        string? recordedTarget = null,\n        string? recordedTargetTooltip = null)\n    {')
    rep(table, '        var pickWidth = Math.Max(\n            1f,\n            ImGui.GetContentRegionAvail().X - CueActionIconSize - gap - CueActionIconSize - gap);', '        var available = ImGui.GetContentRegionAvail().X;\n        var targetWidth = recordedTarget == null ? 0f : Math.Clamp(available * 0.30f,\n            60 * Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale, 180 * Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale);\n        var pickWidth = Math.Max(1f, available - CueActionIconSize * 2 - gap * 2\n            - (recordedTarget == null ? 0 : targetWidth + gap));')
    rep(table, '        DrawCueActionPickButton(actionId, label, onPick, pickWidth);\n        ImGui.SameLine(0f, gap);', '        DrawCueActionPickButton(actionId, label, onPick, pickWidth);\n        ImGui.SameLine(0f, gap);\n        if (recordedTarget != null)\n        {\n            DrawRecordedTarget(recordedTarget, recordedTargetTooltip, targetWidth);\n            ImGui.SameLine(0f, gap);\n        }')

    # Resolve translated English NPC names through BNpcName IDs, not region-specific text.
    for name in ['NpcNameIndex.cs', 'TargetNameLookup.cs']:
        shutil.copyfile(assets / name, root / 'Oracle/Services' / name)
    shutil.copyfile(assets / 'BNpcNames.en.json.gz', root / 'Oracle/Data/BNpcNames.en.json.gz')
    shutil.copyfile(assets / 'NPC_DATA_SOURCE.md', root / 'Oracle/Data/NPC_DATA_SOURCE.md')
    rep('Oracle/Oracle.csproj', '        <None Update="LogMimic.json">', '        <None Update="Data\\BNpcNames.en.json.gz">\n            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>\n        </None>\n        <None Update="Data\\NPC_DATA_SOURCE.md">\n            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>\n        </None>\n        <None Update="LogMimic.json">')
    rep(svc, '&& string.Equals(o.Name.TextValue, cue.ReplayTargetName, StringComparison.OrdinalIgnoreCase))', '&& (cue.ReplayTargetKind == "enemy"\n                    ? TargetNameLookup.Matches(cue.ReplayTargetName, o)\n                    : string.Equals(o.Name.TextValue, cue.ReplayTargetName, StringComparison.OrdinalIgnoreCase)))')

    rep(svc, '            _boundTargets[cue.ReplayTargetLogId] = candidates[0]!.GameObjectId;\n', '')

    shutil.copyfile(assets / 'ReplayTargetChoice.cs', root / 'Oracle/Services/ReplayTargetChoice.cs')
    rep('Oracle/Configuration.cs', '    public bool ReplayAutoStartOnCountdown { get; set; } = true;', '    public bool ReplayAutoStartOnCountdown { get; set; } = true;\n    public bool ReplayUseSelectedForAmbiguousTargets { get; set; } = true;')
    rep(svc, '''        if (candidates.Length == 1)
        {
            return candidates[0];
        }
        reason = candidates.Length == 0 ? I18n.Get("replay.not_spawned") : I18n.Get("replay.multiple");''', '''        var selection = ReplayTargetChoice.Choose(
            candidates.Select(candidate => candidate.GameObjectId).ToArray(),
            PluginServices.TargetManager.Target?.GameObjectId,
            C.ReplayUseSelectedForAmbiguousTargets && cue.ReplayTargetKind == "enemy");
        if (selection.ObjectId is { } chosenId)
            return candidates.First(candidate => candidate.GameObjectId == chosenId);
        reason = candidates.Length == 0 ? I18n.Get("replay.not_spawned")
            : C.ReplayUseSelectedForAmbiguousTargets && cue.ReplayTargetKind == "enemy"
                ? I18n.Get("replay.select_matching_enemy") : I18n.Get("replay.multiple");''')
    rep(svc, '        if (target != null && (cue.ReplayTargetKind == "enemy" || cue.ReplayTargetKind == "ally"))', '        if (target != null && cue.ReplayTargetKind == "enemy")')
    hybrid_text = {
        'lm.follow_target': ('Use selected enemy when matching NPCs are ambiguous', '같은 적이 여러 마리면 현재 선택한 대상 사용'),
        'lm.follow_target_help': ('A unique logged target is selected automatically. With multiple matching enemies, use your current selection only if it is one of those matches. Explicit bindings take priority. Self/ally actions retain their recorded target and do not overwrite your enemy selection. Queued actions keep their previously sent target.', '로그 대상이 하나로 구분되면 자동으로 따라갑니다. 같은 대상 후보가 여러 마리일 때만 그중 현재 선택한 적을 사용합니다. 직접 연결한 대상은 매핑 설정을 우선합니다. 자신·아군 스킬은 원래 대상을 유지하고 선택한 적을 덮어쓰지 않습니다. 이미 요청한 스킬은 기존 대상으로 나갈 수 있습니다.'),
        'lm.follow_target_summary': ('Unique target: automatic. Multiple matching enemies: choose one to resume waiting actions.', '대상이 하나면 자동 매칭합니다. 같은 적이 여러 마리면 그중 하나를 선택하여 대기 중인 스킬을 재개하세요.'),
        'replay.select_matching_enemy': ('Multiple enemies match the logged target. Select one of those enemies to resume.', '로그 대상과 일치하는 적이 여러 마리입니다. 그중 공격할 적을 선택하면 재생을 계속합니다.'),
    }
    for lang, index in [('en', 0), ('ko', 1)]:
        p = root / f'Oracle/Data/I18n/{lang}.json'
        m = json.loads(p.read_text(encoding='utf-8'))
        m.update({key: values[index] for key, values in hybrid_text.items()})
        p.write_text(json.dumps(m, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
