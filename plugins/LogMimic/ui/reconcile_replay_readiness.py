"""Use live recast/ready checks consistently instead of a stale GCD snapshot."""
import json
import shutil


def apply(root, assets):
    for name in ['ReplayGcdReadiness.cs', 'LogMimicService.Readiness.cs']:
        shutil.copyfile(assets / name, root / 'Oracle/Services' / name)
    services = root / 'Oracle/Services'
    # Queue dispatch, its final execution gate, clock holds and owned input
    # recovery must use the same native readiness decision in each frame.
    for name in ['LogMimicService.cs', 'LogMimicService.Adaptive.cs',
                 'LogMimicService.ClockHold.cs', 'LogMimicService.TargetRecovery.cs']:
        p = services / name
        source = p.read_text(encoding='utf-8')
        old = 'var window = ReplayNativeTiming.Read(manager);'
        if source.count(old) != 1:
            raise RuntimeError(f'Readiness anchor changed in {name}')
        p.write_text(source.replace(old, 'var window = ReadReplayWindow(manager);'), encoding='utf-8')
    p = services / 'LogMimicService.cs'
    source = p.read_text(encoding='utf-8')
    old = '    public void Update()\n    {'
    if source.count(old) != 1:
        raise RuntimeError('Replay update anchor changed')
    source = source.replace(old, '    private void UpdateReplayCore()\n    {')
    old = '                Message(I18n.Format("replay.progress", _state, _mainIndex, _main.Count, _independentDone.Count, _independent.Count));'
    if source.count(old) != 1:
        raise RuntimeError('Replay status anchor changed')
    source = source.replace(old, old + '\n                PrintLiveReplayStatus();')
    p.write_text(source, encoding='utf-8')
    texts = {
        'replay.live_status': (
            'Live: GCD {0:0.00}s / cast {1:0.00}s / lock {2:0.00}s / queued {3} / native status {4} / pending confirmation {5} / replay tick {6}s ago',
            '실시간: 글쿨 {0:0.00}초 / 시전 {1:0.00}초 / 경직 {2:0.00}초 / 예약 {3} / 게임 판정 {4} / 사용 확인 대기 {5} / 재생 틱 {6}초 전'),
        'replay.update_error': ('Replay update error: {0}', '재생 업데이트 오류: {0}'),
    }
    for lang, index in [('en', 0), ('ko', 1)]:
        p = root / f'Oracle/Data/I18n/{lang}.json'
        messages = json.loads(p.read_text(encoding='utf-8'))
        messages.update({key: value[index] for key, value in texts.items()})
        p.write_text(json.dumps(messages, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('Applied consistent native GCD readiness and on-demand live status diagnostics.')
