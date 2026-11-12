"""Add observable replay requests, cast-start timing and confirmed hotbar feedback."""
import json, shutil

def apply(root, assets):
 def rep(path, old, new):
  p=root/path;s=p.read_text(encoding='utf-8')
  if s.count(old)!=1: raise RuntimeError(f'Feedback anchor {path}: {old[:100]} ({s.count(old)})')
  p.write_text(s.replace(old,new),encoding='utf-8')
 for name in ['ReplayCastTiming.cs','ReplayFeedbackLog.cs','ReplayClickSequence.cs','LogMimicService.Feedback.cs','HotbarHighlightService.Replay.cs','LogMimicStatusWindow.cs']:
  shutil.copyfile(assets/name,root/'Oracle/Services'/name)
 rep('Oracle/Configuration.cs','    public int Version { get; set; } = 1;','    public int Version { get; set; } = 1;\n    public bool ReplayShowFeedback { get; set; } = true;\n    public bool ReplayShowHotbarPulse { get; set; } = true;\n    public bool ReplayAlignFirstActionToZero { get; set; } = true;\n    public int ReplayZeroStartVersion { get; set; }')
 model='Oracle/Models/TimelineModels.cs'
 rep(model,'    public float TimeOffsetSec { get; set; }','    public float TimeOffsetSec { get; set; }\n    public float? ReplayStartOffsetSec { get; set; }')
 rep(model,'            TimeOffsetSec = TimeOffsetSec,','            TimeOffsetSec = TimeOffsetSec,\n            ReplayStartOffsetSec = Kind == TimelineCueKind.Action ? ReplayStartOffsetSec : null,')
 rep('Oracle/UI/ConfigWindow.CueTable.cs','            cue.TimeOffsetSec = parsedTime;','            if (cue.ReplayStartOffsetSec.HasValue) cue.ReplayStartOffsetSec += parsedTime - cue.TimeOffsetSec;\n            cue.TimeOffsetSec = parsedTime;')
 rep('Oracle/UI/ConfigWindow.CueTable.cs','                cue.Kind = next;', '                cue.Kind = next;\n                if (next != TimelineCueKind.Action) cue.ReplayStartOffsetSec = null;')
 rep('Oracle/UI/ConfigWindow.cs','        cue.ActionId = action.ActionId;','        cue.ActionId = action.ActionId;\n        cue.ReplayStartOffsetSec = null;')
 rep('Oracle/Services/TimelineEngine.cs','        var cast = ActionTiming.GetCastSeconds(cue);\n        return cue.TimeOffsetSec - cast;','        if (cue.ReplayStartOffsetSec is { } recordedStart) return recordedStart;\n        var cast = ActionTiming.GetCastSeconds(cue);\n        return cue.TimeOffsetSec - cast;')
 client=root/'Oracle/Services/FFLogs/FFLogsClient.cs';s=client.read_text(encoding='utf-8')
 start=s.index('    public async Task<IReadOnlyList<FFLogsCastEvent>> GetCastsAsync(');end=s.index('    public async Task<IReadOnlyList<FFLogsCastEvent>> GetEnemyCastsAsync(',start)
 part=s[start:end].replace('ParseCastEvents(data)','ParseCastEvents(data, includeBeginCast: true)');client.write_text(s[:start]+part+s[end:],encoding='utf-8')
 imp='Oracle/Services/FFLogs/FFLogsImportService.cs'
 rep(imp,'        foreach (var cast in casts.OrderBy(c => c.Timestamp))\n        {','''        var marks = casts.Select((cast, index) => new ReplayCastMark(index, cast.AbilityGameId, cast.TargetId,
            cast.Timestamp, string.Equals(cast.Type, "begincast", StringComparison.OrdinalIgnoreCase)));
        foreach (var window in ReplayCastTiming.Resolve(marks))
        {
            var cast = casts[window.Index];''')
 rep(imp,'                TimeOffsetSec = fight.OffsetSec(cast.Timestamp),','                TimeOffsetSec = fight.OffsetSec(cast.Timestamp),\n                ReplayStartOffsetSec = fight.OffsetSec(window.StartTimestamp),')
 # Server-confirmed raw action ID must not be replaced with a future combo/stance variant.
 rep('Oracle/Services/Sync/ActionUseDetector.cs','        ActionUsed?.Invoke(recordId, targetJobId);','        ActionUsed?.Invoke(actionId, targetJobId);')
 svc='Oracle/Services/LogMimicService.cs'
 rep(svc,'internal sealed unsafe class LogMimicService : IDisposable','internal sealed unsafe partial class LogMimicService : IDisposable')
 rep(svc,'            _state = I18n.Get("replay.inactive");','            _state = IdleReplayState();')
 rep(svc,'        var elapsed = _engine.ElapsedSeconds;','        var elapsed = PlaybackElapsed;')
 rep(svc,'now - _pendingMainSince > 2600','now - _pendingMainSince > ReplayPacing.ConfirmationTimeoutMs(_mainIndex < _main.Count ? ActionTiming.GetCastSeconds(_main[_mainIndex].ActionId) : 0)')
 rep(svc,'now - _pendingIndependentSince > 2600','now - _pendingIndependentSince > ReplayPacing.ConfirmationTimeoutMs(ActionTiming.GetCastSeconds(_independent.FirstOrDefault(c => c.Id == _pendingIndependentId)?.ActionId ?? 0))')
 rep(svc,'            if (elapsed < cue.TimeOffsetSec) break;','            if (elapsed < Scheduled(cue)) break;')
 rep(svc,'            if (elapsed > cue.TimeOffsetSec + 5f)','            if (elapsed > Scheduled(cue) + 5f)')
 rep(svc,'        if (elapsed < next.TimeOffsetSec)','        if (elapsed < Scheduled(next))')
 rep(svc,'next.TimeOffsetSec - elapsed','Scheduled(next) - elapsed')
 rep(svc,'            .OrderBy(c => c.TimeOffsetSec).ToList();','            .OrderBy(Scheduled).ToList();')
 rep(svc,'        _independent = ordered.Where(c => !excluded.Contains(c.ActionId) && detached.Contains(c.ActionId)).ToList();','        _independent = ordered.Where(c => !excluded.Contains(c.ActionId) && detached.Contains(c.ActionId)).ToList();\n        PrepareFeedback();')
 rep(svc,'                    _independentDone.Add(pending.Id);','                    Feedback.Confirmed(pending.Id, used, _engine.ElapsedSeconds, Environment.TickCount64);\n                    _independentDone.Add(pending.Id);')
 rep(svc,'            if (_mainIndex < _main.Count && Matches(_main[_mainIndex].ActionId, used))','            if (_mainIndex < _main.Count && _pendingMainId == _main[_mainIndex].Id && Matches(_main[_mainIndex].ActionId, used))')
 rep(svc,'                _mainIndex++;\n                _pendingMainId = null;','                Feedback.Confirmed(_main[_mainIndex].Id, used, _engine.ElapsedSeconds, Environment.TickCount64);\n                _mainIndex++;\n                _pendingMainId = null;')
 rep(svc,'        if (manager->GetActionStatus(ActionType.Action, actionId, targetId) != 0)','        var gameStatus = manager->GetActionStatus(ActionType.Action, actionId, targetId);\n        Feedback.LastGameStatus = gameStatus;\n        if (gameStatus != 0)')
 rep(svc,'        if (sent)\n        {','''        if (sent)
        {
            HotbarFeedback?.StartReplayClick(clickSlots, now);
            Feedback.Requested(cue.Id, actionId,
                target?.Name.TextValue ?? PluginServices.TargetManager.Target?.Name.TextValue ?? I18n.Get("lm.target_unknown"),
                Scheduled(cue) - _playbackOffset, _engine.ElapsedSeconds, now);''')
 hotbar='Oracle/Services/HotbarHighlightService.cs'
 rep(hotbar,'internal sealed unsafe class HotbarHighlightService','internal sealed unsafe partial class HotbarHighlightService')
 rep(hotbar,'        LastFrame = frame;','        LastFrame = frame;\n        DrawReplayFeedback();')
 rep('Oracle/Plugin.cs','        _hotbarHighlight = new HotbarHighlightService(_engine);','        _hotbarHighlight = new HotbarHighlightService(_engine) { Replay = _replay };')
 rep('Oracle/Plugin.cs','        _hotbarHighlight = new HotbarHighlightService(_engine) { Replay = _replay };','        _hotbarHighlight = new HotbarHighlightService(_engine) { Replay = _replay };\n        _replay.HotbarFeedback = _hotbarHighlight;')
 rep('Oracle/Plugin.cs','        _replay.Update();','        _replay.Update();\n        _hotbarHighlight.UpdateReplayClicks();')
 rep('Oracle/Plugin.cs','        ReplayAutoLoad.MigrateOnce(timelineStore);','''        ReplayAutoLoad.MigrateOnce(timelineStore);
        if (C.ReplayZeroStartVersion < 1)
        {
            C.ReplayAlignFirstActionToZero = true;
            C.ReplayZeroStartVersion = 1;
            C.Save();
        }''')
 rep(svc,'        var sent = manager->UseAction(ActionType.Action, actionId, targetId);','        var clickSlots = HotbarFeedback?.CaptureReplayClick(actionId);\n        var sent = manager->UseAction(ActionType.Action, actionId, targetId);')
 texts={
 'feedback.preview':('Preview only: no action requests are sent.','미리보기 상태: 스킬 사용 요청은 보내지 않습니다.'),
 'feedback.ready':('Preset matched. Waiting for countdown or manual replay.','프리셋 매칭 완료. 초읽기 또는 수동 재생 대기 중.'),
 'feedback.no_match':('No matching replay preset. Check zone/job and Auto Load, or load a replay preset.','일치하는 재생 프리셋이 없습니다. 지역·직업·자동 불러오기를 확인하거나 프리셋을 불러오세요.'),
 'feedback.clock':('Timeline: {0:0.00}s','타임라인: {0:0.00}초'),
 'feedback.next':('NEXT','다음 스킬'),
 'feedback.planned':('Planned: {0:0.00}s','예정: {0:0.00}초'),
 'feedback.request':('Requested','사용 요청'),
 'feedback.request_detail':('At {0:0.00}s / delay {1:+0.00;-0.00;0.00}s / requested target: {2}','{0:0.00}초 / 예정 대비 {1:+0.00;-0.00;0.00}초 / 요청 대상: {2}'),
 'feedback.confirmed':('Confirmed: {0} at {1:0.00}s','사용 확인: {0} / {1:0.00}초'),
 'feedback.game_wait':('Waiting for game readiness (status {0})','게임의 사용 조건 대기 중 (상태 {0})'),
 'feedback.legend':('Outline: next / button press: request accepted / confirmation: shown above','테두리: 다음 스킬 / 버튼 눌림: 요청 수락 / 사용 확인: 위 기록'),
 'feedback.show':('Show replay status panel','재생 상태 패널 표시'),
 'feedback.hotbar':('Show native hotbar button presses','단축바 클릭 모션 표시'),
 'feedback.align':('Align the first recorded action to 0s','첫 기록 스킬을 0초에 맞추기'),
 'feedback.align_help':('Enabled by default: the first action is due at countdown 0s, keeping the relative action intervals. This update enables it once for existing settings too; you can turn it off to preserve original timestamps. Set before the countdown. Re-import for cast-start data.','기본으로 켜져 있으며, 스킬 간 간격을 유지하고 첫 스킬을 초읽기 0초에 맞춥니다. 기존 설정에도 이번 업데이트에서 한 번 적용하며, 끄면 로그의 원래 시각을 유지합니다. 초읽기 전에 설정하세요. 시전 시작 기록을 적용하려면 로그를 다시 가져오세요.'),
 }
 for lang,i in [('en',0),('ko',1)]:
  p=root/f'Oracle/Data/I18n/{lang}.json';m=json.loads(p.read_text(encoding='utf-8'));m.update({k:v[i] for k,v in texts.items()});p.write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 print('Added replay feedback panel, native hotbar button presses and recorded cast-start timing.')

 rep(svc, '        var target = ResolveTarget(cue, out var reason);', '        Feedback.LastGameStatus = 0;\n        var target = ResolveTarget(cue, out var reason);')
