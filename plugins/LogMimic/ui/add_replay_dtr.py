"""Add the native server-info-bar replay control, sharing the existing replay controller."""
import json
import shutil


def apply(root, assets):
    def rep(path, old, new):
        p = root / path
        s = p.read_text(encoding='utf-8')
        if s.count(old) != 1:
            raise RuntimeError(f'DTR anchor {path}: {old[:90]} ({s.count(old)})')
        p.write_text(s.replace(old, new), encoding='utf-8')

    shutil.copyfile(assets / 'LogMimicDtrEntry.cs', root / 'Oracle/Services/LogMimicDtrEntry.cs')
    plugin = 'Oracle/Plugin.cs'
    rep(plugin, '    private readonly LogMimicService _replay;',
        '    private readonly LogMimicService _replay;\n    private readonly LogMimicDtrEntry _replayDtr;')
    rep(plugin, '        IDutyState dutyState)\n    {\n        // 1.',
        '        IDutyState dutyState,\n        IDtrBar dtrBar)\n    {\n        // 1.')
    rep(plugin, '        _timelineConfigWindow.Replay = _replay;',
        '        _timelineConfigWindow.Replay = _replay;\n        _replayDtr = new LogMimicDtrEntry(dtrBar, _replay, () => { _timelineConfigWindow.IsOpen = true; });')
    rep(plugin, '        _replay.Update();', '        _replay.Update();\n        _replayDtr.Update();')
    rep(plugin, '        _replay.Dispose();', '        _replayDtr.Dispose();\n        _replay.Dispose();')
    texts = {
        'en': 'Left click: enable or pause replay\nRight click: open LogMimic\nLoad a replay preset and start a countdown or combat before enabling replay.',
        'ko': '왼쪽 클릭: 재생 켜기/중지\n오른쪽 클릭: LogMimic 열기\n재생을 켜려면 프리셋을 불러오고 초읽기 또는 전투를 시작하세요.',
    }
    for lang, help_text in texts.items():
        p = root / f'Oracle/Data/I18n/{lang}.json'
        m = json.loads(p.read_text(encoding='utf-8'))
        m['dtr.help'] = help_text
        p.write_text(json.dumps(m, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Added native LM: ON/OFF server-info-bar control.')
