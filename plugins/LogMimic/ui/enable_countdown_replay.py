"""Patch the already localized standalone source with countdown-driven replay."""
import json, shutil

def apply(root, assets):
 def rep(path,old,new):
  p=root/path;s=p.read_text(encoding='utf-8')
  if s.count(old)!=1: raise RuntimeError(f'Countdown patch anchor: {path}: {old[:90]} ({s.count(old)})')
  p.write_text(s.replace(old,new),encoding='utf-8')
 for name in ['ReplayStartGate.cs','CountdownSignals.cs']:
  shutil.copyfile(assets/name,root/'Oracle/Services'/name)
 shutil.copyfile(assets/'CountdownSyncDetector.cs',root/'Oracle/Services/Sync/CountdownSyncDetector.cs')
 rep('Oracle/Configuration.cs','    public int Version { get; set; } = 1;','    public int Version { get; set; } = 1;\n    public bool ReplayAutoStartOnCountdown { get; set; } = true;')
 engine='Oracle/Services/TimelineEngine.cs'
 rep(engine,'    public bool IsRunning => _running;','    public long CountdownSequence { get; private set; }\n    public bool IsCountdownSession { get; private set; }\n    public bool IsRunning => _running;')
 rep(engine,'        _previewMode = preview;\n        Activate(doc, clockOffset);','        IsCountdownSession = false;\n        _previewMode = preview;\n        Activate(doc, clockOffset);')
 rep(engine,'    private void StopClock()\n    {','    private void StopClock()\n    {\n        IsCountdownSession = false;')
 rep(engine,'        _countdown.Update();\n        if (!_countdown.JustStarted)','        _countdown.Update();\n        if (_countdown.JustCancelled && IsCountdownSession && !_combat.InCombat)\n            StopClock();\n        if (!_countdown.JustStarted)')
 rep(engine,'        StartClock(_activeDoc, -Math.Abs(_countdown.StartedRemaining), preview: false);','        if (_combat.InCombat) return;\n        StartClock(_activeDoc, -Math.Abs(_countdown.StartedRemaining), preview: false);\n        CountdownSequence++;\n        IsCountdownSession = true;')
 svc='Oracle/Services/LogMimicService.cs'
 rep(svc,'    private bool _suspended = true;','    private bool _suspended = true;\n    private long _replayCountdownSequence = -1;\n    private readonly ReplayStartGate _autoStart = new();')
 rep(svc,'            if (_doc != null) Reset();\n            _state = I18n.Get("replay.inactive");','            _autoStart.Observe(_engine.CountdownSequence, _engine.IsCountdownSession, false, C.ReplayAutoStartOnCountdown);\n            if (_doc != null) Reset();\n            _state = I18n.Get("replay.inactive");')
 rep(svc,'        var currentJobId = PluginServices.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;','''        var player = PluginServices.ObjectTable.LocalPlayer;
        if (player == null || player.IsDead)
        {
            _autoStart.Pause(_engine.CountdownSequence);
            _suspended = true;
            _pendingMainId = null;
            _pendingIndependentId = null;
            while (_received.TryDequeue(out _)) { }
            _state = I18n.Get("replay.dead");
            return;
        }
        var currentJobId = player.ClassJob.RowId;''')
 rep(svc,'            _state = I18n.Format("replay.job", active.ClassJobId, currentJobId);','            _autoStart.Observe(_engine.CountdownSequence, _engine.IsCountdownSession, false, C.ReplayAutoStartOnCountdown);\n            _state = I18n.Format("replay.job", active.ClassJobId, currentJobId);')
 rep(svc,'        if (!ReferenceEquals(_doc, active)) Begin(active);','''        if (!ReferenceEquals(_doc, active) || (_engine.IsCountdownSession && _replayCountdownSequence != _engine.CountdownSequence)) Begin(active);
        if (_autoStart.Observe(_engine.CountdownSequence, _engine.IsCountdownSession, true, C.ReplayAutoStartOnCountdown))
        {
            SkipMissedPreparation();
            _suspended = false;
        }''')
 rep(svc,'        _doc = doc;', '        _doc = doc;\n        _replayCountdownSequence = _engine.CountdownSequence;')
 rep(svc,'    private void Reset()','''    private void SkipMissedPreparation()
    {
        var elapsed = _engine.ElapsedSeconds;
        while (_mainIndex < _main.Count && ReplayPreparation.IsMissed(_main[_mainIndex].TimeOffsetSec, elapsed))
            _mainIndex++;
        foreach (var cue in _independent.Where(c => ReplayPreparation.IsMissed(c.TimeOffsetSec, elapsed)))
            _independentDone.Add(cue.Id);
    }

    private void Reset()''')
 rep(svc,'            case "on":','''            case "on":
                if (PluginServices.ObjectTable.LocalPlayer is not { IsDead: false })
                {
                    Message(I18n.Get("replay.dead"));
                    break;
                }''')
 rep(svc,'                _suspended = false;\n                Message(I18n.Get("replay.on"));','                SkipMissedPreparation();\n                _suspended = false;\n                Message(I18n.Get("replay.on"));')
 rep(svc,'            case "off":\n                _suspended = true;','            case "off":\n                _autoStart.Pause(_engine.CountdownSequence);\n                _suspended = true;')
 # Negative cast offsets are preserved. A time-bounded query includes preparation events
 # outside the fight ID, without collecting actions from the previous fight.
 path=root/'Oracle/Services/FFLogs/FFLogsClient.cs';s=path.read_text(encoding='utf-8')
 start=s.index('    public async Task<IReadOnlyList<FFLogsCastEvent>> GetCastsAsync(')
 end=s.index('    public async Task<IReadOnlyList<FFLogsCastEvent>> GetEnemyCastsAsync(',start)
 part=s[start:end]
 part=part.replace('CancellationToken ct = default)','CancellationToken ct = default,\n        double? preparationStartTime = null)')
 part=part.replace('              $fightIDs: [Int]!\n','').replace('                    fightIDs: $fightIDs\n','').replace('                fightIDs = new[] { fightId },\n','')
 part=part.replace('            fightStartTime,\n            ct)', '            preparationStartTime ?? fightStartTime,\n            ct)')
 path.write_text(s[:start]+part+s[end:],encoding='utf-8')
 rep('Oracle/UI/FFLogsImportPanel.cs','''                    C.FFLogsClientSecret,
                    ct);
            var hitsTask''','''                    C.FFLogsClientSecret,
                    ct,
                    preparationStartTime: replayAll ? ReplayPreparation.ImportStart(fight.StartTime,
                        _meta?.Fights.Where(f => f.Id != fight.Id && f.EndTime <= fight.StartTime)
                            .Select(f => f.EndTime).DefaultIfEmpty(0).Max() ?? 0) : null);
            var hitsTask''')
 # Update runtime-facing copy after the existing localization patch generates its entries.
 updates={
 'replay.need_fight':('Load a replay preset and start a countdown or combat first.','재생 프리셋을 불러온 뒤 초읽기 또는 전투를 시작하세요.'),
 'replay.dead':('Replay paused: player is dead or unavailable. Reviving does not resume this countdown.','사망했거나 플레이어를 확인할 수 없어 재생을 중지했습니다. 부활만으로는 다시 시작하지 않습니다.'),
 'replay.loaded':('Loaded {0} main + {1} independent actions. Waiting for automatic countdown start or /lm replay on.','메인 {0}개 + 별도 스킬 {1}개 불러옴. 초읽기 자동 시작 또는 /lm replay on 대기.'),
 'lm.auto_start':('Automatically replay on countdown','초읽기 시작 시 자동 재생'),
 'lm.manual':('Countdown auto-start follows recorded preparation times before 0s. Pause stops the current countdown; a fresh countdown can start again. Death pauses replay. Without a countdown, enable replay manually.','초읽기 자동 시작 시 0초 이전 준비 스킬도 기록된 시각에 맞춰 사용합니다. 중지를 누르면 현재 초읽기에서는 다시 켜지지 않으며, 새 초읽기에서 다시 시작할 수 있습니다. 사망하면 재생을 중지합니다. 초읽기가 없으면 직접 재생을 켜세요.'),
 'lm.prep_missing':('This preset has no preparation actions before 0s. Re-import the log or add negative-time actions in the library. Only recorded preparation can be imported.','이 프리셋에는 0초 이전 준비 스킬이 없습니다. 로그를 다시 가져오거나 라이브러리에서 음수 시간의 스킬을 추가하세요. 로그에 남아 있는 준비 스킬만 가져올 수 있습니다.'),
 'lm.intro':('Import a log and select a preset. With countdown auto-start enabled, replay begins at the recorded preparation timings.','로그를 가져와 프리셋을 선택하세요. 초읽기 자동 재생이 켜져 있으면 기록된 준비 스킬 시각부터 재생합니다.'),
 'lm.autoload_help':('Load the matching zone/job/phase preset. One preset per match; no automatic switching during combat. Countdown auto-start is configured separately.','지역·직업·페이즈가 일치하면 프리셋을 불러옵니다. 같은 조건에는 하나만 지정할 수 있고 전투 중에는 자동 교체하지 않습니다. 초읽기 자동 재생은 별도 옵션입니다.'),
 }
 for lang,index in [('en',0),('ko',1)]:
  p=root/f'Oracle/Data/I18n/{lang}.json';m=json.loads(p.read_text(encoding='utf-8'));m.update({k:v[index] for k,v in updates.items()});p.write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 print('Applied countdown auto-replay, preparation import, cancellation and death pause.')
