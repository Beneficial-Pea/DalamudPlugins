"""Hold continuous attack playback on stalls; keep encounter-timed preparations live."""
import json
import shutil


def apply(root, assets):
    def rep(path, old, new):
        p = root / path
        s = p.read_text(encoding='utf-8')
        if s.count(old) != 1:
            raise RuntimeError(f'Timeline hold anchor {path}: {old[:100]} ({s.count(old)})')
        p.write_text(s.replace(old, new), encoding='utf-8')

    for name in ['ReplayTimelineHold.cs','ReplayDowntimePrep.cs','LogMimicService.ClockHold.cs','LogMimicVersion.cs']:
        shutil.copyfile(assets / name, root / 'Oracle/Services' / name)
    engine = 'Oracle/Services/TimelineEngine.cs'
    rep(engine, '    private DateTime ClockNowUtc => _previewPaused ? _pauseHeldUtc : DateTime.UtcNow;',
        '''    private readonly ReplayTimelineHold _replayHold = new();
    private DateTime _encounterSyncUtc;
    private float _encounterOffset;
    public Func<bool>? ReplayShouldHoldClock { get; set; }
    public bool IsReplayTimelineHeld => _replayHold.IsHeld;
    public float EncounterElapsedSeconds => _running ? _encounterOffset + (float)(DateTime.UtcNow - _encounterSyncUtc).TotalSeconds : 0;
    private DateTime ClockNowUtc => _previewPaused ? _pauseHeldUtc : _replayHold.Now(DateTime.UtcNow);

    public void SetReplayTimelineHeld(bool held)
    {
        if (!_running || _previewMode) { _replayHold.Reset(); return; }
        var delta = _replayHold.SetHeld(held,DateTime.UtcNow);
        if (delta <= TimeSpan.Zero) return;
        _syncUtc += delta;
        for (var i=0; i<_highlights.Count; i++)
        {
            var h = _highlights[i];
            _highlights[i] = new ActiveHighlight { CueId=h.CueId, StartedUtc=h.StartedUtc+delta, DurationSec=h.DurationSec };
        }
    }''')
    rep(engine, '    private void StopClock()\n    {',
        '    private void StopClock()\n    {\n        _replayHold.Reset();')
    rep(engine, '        _activeDoc = doc;\n        _previewPaused = false;\n        _syncUtc = DateTime.UtcNow;',
        '''        _activeDoc = doc;
        _replayHold.Reset();
        _encounterSyncUtc = DateTime.UtcNow;
        _encounterOffset = clockOffset;
        _previewPaused = false;
        _syncUtc = DateTime.UtcNow;''')
    rep(engine, '        // anchors / cues\n        if (_running && !_previewPaused)',
        '        SetReplayTimelineHeld(_running && !_previewMode && ReplayShouldHoldClock?.Invoke() == true);\n\n        // anchors / cues\n        if (_running && !_previewPaused)')
    rep(engine, '            _syncUtc = DateTime.UtcNow;\n            _clockOffset = -remaining;',
        '            _syncUtc = ClockNowUtc;\n            _clockOffset = -remaining;\n            _encounterSyncUtc = DateTime.UtcNow;\n            _encounterOffset = -remaining;')
    rep(engine, '            _appliedAnchorCueIds.Add(match.Id);\n            var from = ElapsedSeconds;',
        '''            _appliedAnchorCueIds.Add(match.Id);
            _encounterSyncUtc = DateTime.UtcNow;
            _encounterOffset = match.TimeOffsetSec;
            // After a continuous-playback hold, encounter signals still align the
            // preparation timers but must not jump the paused attack clock forward.
            if (_replayHold.HasHeld && _activeDoc.ReplayEnabled
                && _activeDoc.ReplayPhaseMode == ReplayPhaseMode.ContinueSequence) continue;
            var from = ElapsedSeconds;''')

    svc = 'Oracle/Services/LogMimicService.cs'
    rep(svc, '        _engine.ReplayOwnsConfirmation = OwnsReplayConfirmation;',
        '        _engine.ReplayOwnsConfirmation = OwnsReplayConfirmation;\n        _engine.ReplayShouldHoldClock = WantsTimelineHold;')
    rep(svc, '        _engine.ReplayOwnsConfirmation = null;',
        '        _engine.ReplayOwnsConfirmation = null;\n        _engine.ReplayShouldHoldClock = null;\n        _engine.SetReplayTimelineHeld(false);')
    rep(svc, '        DrainConfirmations();\n        if (_suspended)',
        '        DrainConfirmations();\n        _engine.SetReplayTimelineHeld(WantsTimelineHold());\n        if (_suspended)')
    rep(svc, '        if (UpdatePotions(now)) return;',
        '        if (!_clockHold.BlockMainActions && UpdatePotions(now)) return;')
    rep(svc, '            if (elapsed < Scheduled(cue)) break;',
        '            if (_clockHold.BlockMainActions && !SafeDowntimePreparation(cue.ActionId)) continue;\n            if (_engine.EncounterElapsedSeconds < Scheduled(cue)) break;')
    rep(svc, '            if (elapsed > Scheduled(cue) + 5f)',
        '            if (_engine.EncounterElapsedSeconds > Scheduled(cue) + 5f)')
    rep(svc, '        while (_mainIndex < _main.Count)',
        '        if (_clockHold.BlockMainActions) { _state = TimelineHoldText; return; }\n        while (_mainIndex < _main.Count)')
    rep(svc, '''        _main = ordered.Where(c => !excluded.Contains(c.ActionId) && !detached.Contains(c.ActionId)).ToList();
        _independent = ordered.Where(c => !excluded.Contains(c.ActionId) && detached.Contains(c.ActionId)).ToList();''',
        '''        var downtimePrep = FindDowntimePreparations(doc,ordered.Where(c => !excluded.Contains(c.ActionId)).ToList());
        _main = ordered.Where(c => !excluded.Contains(c.ActionId) && !detached.Contains(c.ActionId) && !downtimePrep.Contains(c.Id)).ToList();
        _independent = ordered.Where(c => !excluded.Contains(c.ActionId) && (detached.Contains(c.ActionId) || downtimePrep.Contains(c.Id))).ToList();''')
    rep(svc, '        PrepareAdaptive();', '        PrepareAdaptive();\n        PrepareClockHold();')
    rep(svc, '    private void Reset()\n    {',
        '    private void Reset()\n    {\n        _engine.SetReplayTimelineHeld(false);\n        _clockHold = default;')
    rep(svc, 'Feedback.Confirmed(pending.Id, used, _engine.ElapsedSeconds, Environment.TickCount64);',
        'Feedback.Confirmed(pending.Id, used, _engine.EncounterElapsedSeconds, Environment.TickCount64);')
    rep(svc, '                Scheduled(cue) - _playbackOffset, _engine.ElapsedSeconds, now);',
        '''                Scheduled(cue) - (independent ? 0 : _playbackOffset),
                independent ? _engine.EncounterElapsedSeconds : _engine.ElapsedSeconds, now);''')

    adaptive = 'Oracle/Services/LogMimicService.Adaptive.cs'
    p = root / adaptive
    s = p.read_text(encoding='utf-8')
    if s.count('PlaybackElapsed') != 3:
        raise RuntimeError('Encounter-time preparation anchors changed')
    s = s.replace('PlaybackElapsed','_engine.EncounterElapsedSeconds')
    p.write_text(s,encoding='utf-8')
    rep(adaptive, '\n            if (_independentDone.Contains(cue.Id)) continue;',
        '\n            if (_independentDone.Contains(cue.Id)) continue;\n            if (_clockHold.BlockMainActions && !SafeDowntimePreparation(cue.ActionId)) continue;')
    rep(adaptive, '\n                if (_independentDone.Contains(cue.Id)) continue;',
        '\n                if (_independentDone.Contains(cue.Id)) continue;\n                if (_clockHold.BlockMainActions && !SafeDowntimePreparation(cue.ActionId)) continue;')
    rep(adaptive, '        var targetAvailable = true;', '        var targetAvailable = !_clockHold.BlockMainActions;')
    rep(adaptive, '            targetAvailable = candidate != null && targetReason == null;',
        '            targetAvailable = candidate != null && targetReason == null && !_clockHold.BlockMainActions;')
    rep('Oracle/Services/LogMimicService.Potions.cs', '_potions?.Dispatch(_engine.ElapsedSeconds,',
        '_potions?.Dispatch(_engine.EncounterElapsedSeconds,')

    dtr = 'Oracle/Services/LogMimicDtrEntry.cs'
    rep(dtr, '        var tooltip = I18n.Get("dtr.help") + "\\n\\n" + _replay.State;',
        '        var tooltip = "LogMimic " + LogMimicVersion.Display + "\\n" + I18n.Get("dtr.help") + "\\n\\n" + _replay.State;\n        if (_replay.TimelineIsHeld) tooltip += "\\n" + _replay.TimelineHoldText;')
    ui = 'Oracle/UI/ConfigWindow.LogMimic.cs'
    rep(ui, 'ImGui.TextColored(OracleTheme.Current.GetColor(MirageUi.Color.Title), "LOGMIMIC");',
        'ImGui.TextColored(OracleTheme.Current.GetColor(MirageUi.Color.Title), "LOGMIMIC " + LogMimicVersion.Display);')
    rep(ui, '        MirageUi.Header("LogMimic");', '        MirageUi.Header("LogMimic " + LogMimicVersion.Display);')

    texts = {
        'clock_hold.target': ('Attack timeline paused: no usable target. Recorded self preparations still follow encounter time.', '공격 타임라인 일시정지: 사용할 대상이 없습니다. 기록된 자기 준비기는 전투 시간에 맞춰 실행합니다.'),
        'clock_hold.condition': ('Attack timeline paused: waiting for the action condition. Preparations and game cooldowns keep running.', '공격 타임라인 일시정지: 스킬 사용 조건 대기 중입니다. 준비기와 실제 게임 쿨타임은 계속 진행됩니다.'),
        'clock_hold.manual': ('Replay and attack timeline paused.', '재생과 공격 타임라인이 일시정지되었습니다.'),
        'phase.continue_help': ('Keep waiting attack order. The attack timeline pauses for target/condition stalls and resumes without a jump. Recorded non-damaging self abilities in downtime gaps (such as Reassemble) and explicit utility timers retain encounter time. Actions before 0s remain manual.', '대기한 공격 순서를 유지하고 대상·사용 조건 때문에 막히면 공격 타임라인도 멈춥니다. 다시 진행할 수 있으면 같은 시점부터 이어갑니다. 로그의 다운타임에 기록된 정비 같은 비공격 자기 준비기와 별도 타이머는 전투 시간을 따릅니다. 0초 전 스킬은 직접 사용하세요.'),
    }
    for lang, index in [('en',0),('ko',1)]:
        p = root / f'Oracle/Data/I18n/{lang}.json'
        m = json.loads(p.read_text(encoding='utf-8'))
        m.update({key:value[index] for key,value in texts.items()})
        p.write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    manifest = root / 'Oracle/LogMimic.json'
    m = json.loads(manifest.read_text(encoding='utf-8'))
    version = '.'.join(m['AssemblyVersion'].split('.')[:3])
    m['Punchline'] = f"v{version} · " + m['Punchline']
    m['Description'] = f"LogMimic v{version}\n공격 타임라인 대기 일시정지 · 전투 시작 후 다운타임 준비기 재생\n\n" + m['Description']
    manifest.write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Applied attack-clock holds, encounter-timed downtime preparations and visible build versions.')
