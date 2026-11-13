"""Per-preset phase behavior, selected before combat without difficulty heuristics."""
import json
import shutil


def apply(root, assets):
    def rep(path, old, new):
        p = root / path
        s = p.read_text(encoding='utf-8')
        if s.count(old) != 1:
            raise RuntimeError(f'Phase policy anchor {path}: {old[:100]} ({s.count(old)})')
        p.write_text(s.replace(old, new), encoding='utf-8')

    shutil.copyfile(assets / 'ReplayPhaseMode.cs', root / 'Oracle/Models/ReplayPhaseMode.cs')
    shutil.copyfile(assets / 'ReplayPhaseSession.cs', root / 'Oracle/Services/ReplayPhaseSession.cs')
    shutil.copyfile(assets / 'ConfigWindow.ReplayPhases.cs', root / 'Oracle/UI/ConfigWindow.ReplayPhases.cs')
    model = 'Oracle/Models/TimelineModels.cs'
    rep(model, '    public bool ReplayEnabled { get; set; }',
        '    public bool ReplayEnabled { get; set; }\n    public ReplayPhaseMode ReplayPhaseMode { get; set; } = ReplayPhaseMode.ContinueSequence;')
    rep(model, '    public bool Effected { get; set; }\n\n    [JsonIgnore]',
        '    public bool Effected { get; set; }\n\n    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]\n    public bool ReplayStartsPhase { get; set; }\n\n    [JsonIgnore]')
    rep(model, '            Effected = IsClockSync && Effected,',
        '            Effected = IsClockSync && Effected,\n            ReplayStartsPhase = IsClockSync && ReplayStartsPhase,')
    rep('Oracle/Services/TimelineStore.cs', '            ReplayEnabled = source.ReplayEnabled,',
        '            ReplayEnabled = source.ReplayEnabled,\n            ReplayPhaseMode = source.ReplayPhaseMode,')
    rep('Oracle/Services/LogMimicService.cs', '        _adaptiveCues.Clear();',
        '        _adaptiveCues.Clear();\n        _phaseSession = null;')

    adaptive = 'Oracle/Services/LogMimicService.Adaptive.cs'
    rep(adaptive, '    private ReplayPhasePlan? _adaptivePhases;\n    private int _adaptivePhaseIndex;',
        '    private ReplayPhaseSession? _phaseSession;')
    rep(adaptive, '''        _adaptivePhaseIndex = 0;
        _adaptivePhases = new ReplayPhasePlan(_main.Select(c => new ReplayPhaseMark(c.Id, Scheduled(c),
            Math.Max(ActionTiming.GetCastSeconds(c.ActionId), ActionTiming.GetRecastSeconds(c.ActionId)))),
            _doc!.Cues.Where(c => c.IsClockSync).Select(c => new ReplayPhaseAnchor(c.Id, c.TimeOffsetSec)));''',
        '''        _phaseSession = new ReplayPhaseSession(_doc!.ReplayPhaseMode,
            _main.Select(c => new ReplayPhaseMark(c.Id, Scheduled(c),
                Math.Max(ActionTiming.GetCastSeconds(c.ActionId), ActionTiming.GetRecastSeconds(c.ActionId)))),
            _doc.Cues.Where(c => c.IsClockSync).Select(c => new ReplayPhaseAnchor(c.Id, c.TimeOffsetSec, c.ReplayStartsPhase)));''')
    rep(adaptive, '_adaptivePhases.Segments[c.Id]', '_phaseSession.Plan.Segments[c.Id]')
    rep(adaptive, '''        while (_adaptivePhases != null && _adaptivePhaseIndex < _adaptivePhases.Boundaries.Count)
        {
            var boundary = _adaptivePhases.Boundaries[_adaptivePhaseIndex];
            if (!boundary.CanRelease(_engine.ElapsedSeconds, _engine.HasObservedReplaySync)) break;
            _adaptive.ReleasePhase(boundary.Segment, boundary.SyncCueId != null);
            _adaptivePhaseIndex++;
        }''', '''        if (_phaseSession?.Observe(_engine.HasObservedReplaySync) is { } boundary)
            _adaptive.ReleasePhase(boundary.Segment, discardEarlier: true);''')
    rep(adaptive, '        var dispatched = _adaptive.Dispatch(window, now, step =>',
        '        var phaseReady = _phaseSession?.CanDispatch(_engine.ElapsedSeconds) ?? true;\n        var dispatched = phaseReady && _adaptive.Dispatch(window, now, step =>')
    rep(adaptive, '''        if (_adaptive.NextGcd == null && _adaptivePhases != null && _adaptivePhaseIndex < _adaptivePhases.Boundaries.Count)
        {
            _state = I18n.Format("adaptive.phase_wait", _adaptivePhases.Boundaries[_adaptivePhaseIndex].At);
            return;
        }''', '''        if (!phaseReady)
        {
            _state = I18n.Format("phase.followup_wait", Math.Max(0, _phaseSession!.ReadyAt - _engine.ElapsedSeconds));
            return;
        }
        if (_adaptive.NextGcd == null && _phaseSession != null
            && _phaseSession.Plan.Boundaries.Any(b => b.Segment > _phaseSession.CurrentSegment))
        {
            _state = I18n.Get("phase.signal_wait");
            return;
        }''')

    rep('Oracle/UI/ConfigWindow.LogMimic.cs',
        '        ImGui.TextUnformatted(L("loaded") + ": " + (active?.Name ?? L("none")));',
        '        ImGui.TextUnformatted(L("loaded") + ": " + (active?.Name ?? L("none")));\n        if (active != null) DrawReplayPhaseSettings(active, "phaseHome");')
    rep('Oracle/UI/ConfigWindow.Editor.cs', '        DrawEditorMeta(_editDoc);',
        '        DrawEditorMeta(_editDoc);\n        DrawReplayPhaseSettings(_editDoc, "phaseLibrary");')
    table = 'Oracle/UI/ConfigWindow.CueTable.cs'
    rep(table, '        var labels = new[] { insertAbove, insertBelow };',
        '''        var phaseStart = I18n.Get("phase.mark_start");
        var labels = cue.IsClockSync ? new[] { insertAbove, insertBelow, phaseStart } : new[] { insertAbove, insertBelow };''')
    rep(table, '        MirageUi.ContextMenu.End();', '''        if (cue.IsClockSync && MirageUi.ContextMenu.DrawItem(phaseStart,
                cue.ReplayStartsPhase ? FontAwesomeIcon.Check : FontAwesomeIcon.ArrowRight, "phaseStart", style))
        {
            cue.ReplayStartsPhase = !cue.ReplayStartsPhase;
            PersistDocument(doc);
            ImGui.CloseCurrentPopup();
        }
        MirageUi.ContextMenu.End();''')
    rep(table, '                    cue.SyncType = default;',
        '                    cue.SyncType = default;\n                    cue.ReplayStartsPhase = false;')

    texts = {
        'phase.mode': ('Phase handling (saved with this preset)', '페이즈 처리 (이 프리셋에 저장)'),
        'phase.continue': ('Continue from the waiting action', '대기한 스킬부터 이어서'),
        'phase.align': ('Resume from the new phase section', '페이즈 전환에 맞춰 해당 구간부터'),
        'phase.before_pull': ('Select before countdown or combat. Each preset remembers its own choice.', '초읽기·전투 시작 전에 선택하세요. 프리셋마다 따로 저장됩니다.'),
        'phase.continue_help': ('Keep the unplayed action order across add phases and target downtime. Resume on your selected target at your current GCD. Timed utility and potions retain encounter time.', '쫄 구간이나 대상이 없는 동안에도 못 쓴 스킬 순서를 유지합니다. 직접 대상을 선택하면 현재 글쿨에 맞춰 이어갑니다. 별도 타이머 스킬·탕약은 전투 시간을 따릅니다.'),
        'phase.align_help': ('On an observed phase sync, skip unrequested actions from the old section and resume the new section with your current cooldowns and resources. Right-click a Sync row to mark a phase start. Syncs in long recorded gaps also apply. Without a signal, do not infer a transition from time or target changes.', '실제 페이즈 동기화 신호가 오면 이전 구간의 미사용 스킬을 건너뛰고, 현재 쿨타임·자원으로 새 구간을 재생합니다. 동기화 행 우클릭으로 페이즈 시작을 지정할 수 있고, 긴 대기 구간의 동기화도 사용합니다. 시간이나 타깃 변경만으로 전환을 추측하지 않습니다.'),
        'phase.requires_adaptive': ('This choice applies when Follow my current GCD is enabled.', '이 선택은 ‘현재 글쿨에 맞춰 재생’을 켰을 때 적용됩니다.'),
        'phase.mark_start': ('Use as phase start', '페이즈 시작으로 사용'),
        'phase.signal_wait': ('Waiting for the configured phase-start signal.', '설정한 페이즈 시작 신호를 기다리는 중입니다.'),
        'phase.followup_wait': ('Phase synchronized. Follow-up action in {0:0.00}s.', '페이즈 동기화 완료. 다음 구간 시작까지 {0:0.00}초.'),
    }
    for lang, index in [('en', 0), ('ko', 1)]:
        p = root / f'Oracle/Data/I18n/{lang}.json'
        m = json.loads(p.read_text(encoding='utf-8'))
        m.update({key: value[index] for key, value in texts.items()})
        p.write_text(json.dumps(m, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Applied per-preset continuation/phase alignment with observed syncs; no difficulty-based defaults.')
