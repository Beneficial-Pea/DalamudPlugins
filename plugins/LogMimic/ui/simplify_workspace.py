"""Keep preset management in the library and expose one global Auto Load switch."""
import json
import shutil


def apply(root, assets):
    def rep(path, old, new):
        p = root / path
        s = p.read_text(encoding='utf-8')
        if s.count(old) != 1:
            raise RuntimeError(f'Workspace anchor {path}: {old[:90]} ({s.count(old)})')
        p.write_text(s.replace(old, new), encoding='utf-8')

    rep('Oracle/Configuration.cs', '    public int Version { get; set; } = 1;',
        '    public int Version { get; set; } = 1;\n    public bool LogMimicAutoLoadEnabled { get; set; } = true;')
    settings = 'Oracle/UI/PluginSettingsWindow.cs'
    shutil.copyfile(assets / 'PluginSettingsWindow.LogMimic.cs', root / 'Oracle/UI/PluginSettingsWindow.LogMimic.cs')
    rep(settings, '    private const string SettingsPluginLog =',
        '    private const string SettingsLogMimic = "settings:logmimic";\n    private const string SettingsPluginLog =')
    rep(settings, 'private string _selectedTabId = SettingsFFLogsApi;',
        'private string _selectedTabId = SettingsLogMimic;')
    rep(settings, '    private static List<MirageTwoColumnSidebarNode> BuildSettingsSidebarNodes() =>\n    [',
        '''    private static List<MirageTwoColumnSidebarNode> BuildSettingsSidebarNodes() =>
    [
        new MirageTwoColumnPageNode
        {
            Entry = new MirageTwoColumnEntry
            {
                Id = SettingsLogMimic,
                Label = "LogMimic",
            },
        },''')
    rep(settings, '        switch (_selectedTabId)\n        {',
        '''        switch (_selectedTabId)
        {
            case SettingsLogMimic:
                DrawLogMimicSettings();
                return;''')
    engine = 'Oracle/Services/TimelineEngine.cs'
    rep(engine, '    private List<(TimelineDocument Doc, int Spec)> ListAutoLoadCandidates()\n    {',
        '''    private List<(TimelineDocument Doc, int Spec)> ListAutoLoadCandidates()
    {
        if (!C.LogMimicAutoLoadEnabled) return [];''')
    rep(engine, '    private TimelineDocument? FindHeldDocument()\n    {',
        '''    private TimelineDocument? FindHeldDocument()
    {
        if (!C.LogMimicAutoLoadEnabled) return null;''')
    texts = {
        'en': 'Enable or disable Auto Load for all configured presets. Zone, job and phase matching still apply; edit individual presets in the Library. A running timeline is kept, and manual loading remains available. Countdown auto-start is configured separately.',
        'ko': '설정된 모든 프리셋의 자동 불러오기를 한 번에 켜거나 끕니다. 지역·직업·페이즈 매칭은 유지하며, 프리셋별 설정은 라이브러리에서 바꿀 수 있습니다. 재생 중인 타임라인은 유지하고 수동 불러오기는 계속 사용할 수 있습니다. 초읽기 자동 재생은 별도 옵션입니다.',
    }
    for lang, text in texts.items():
        p = root / f'Oracle/Data/I18n/{lang}.json'
        m = json.loads(p.read_text(encoding='utf-8'))
        m['lm.autoload_global_help'] = text
        p.write_text(json.dumps(m, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Applied compact logo-free header and global Auto Load settings.')
