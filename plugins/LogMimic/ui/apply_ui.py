"""Apply LogMimic's UI after the versioned Oracle replay patch."""
import json, shutil, sys
from pathlib import Path
root = Path(sys.argv[1]).resolve()
assets = Path(__file__).resolve().parent

def replace(path, old, new):
    p = root / path
    text = p.read_text(encoding='utf-8')
    if text.count(old) != 1:
        raise RuntimeError(f'Unexpected anchor in {path}: {old[:90]}')
    p.write_text(text.replace(old, new), encoding='utf-8')
config = 'Oracle/UI/ConfigWindow.cs'
p = root / config
s = p.read_text(encoding='utf-8')
start = s.index('    private void DrawPages()')
end = s.index('    public void TogglePage(', start)
s = s[:start] + '    private void DrawPages() => DrawLogMimic();\n\n' + s[end:]
s = s.replace('WindowName = I18n.Get("window.config.title") + "###oracleTimelineConfig";', 'WindowName = "LogMimic###logmimicMain";')
s = s.replace('base("Oracle###oracleTimelineConfig", ImGuiWindowFlags.NoDecoration)', 'base("LogMimic###logmimicMain", ImGuiWindowFlags.NoDecoration)')
s = s.replace('        _pageId = pageId;\n        _chromeCollapsed = false;', '        _pageId = pageId;\n        _lmPage = pageId == OracleWindowChrome.SettingsPageId ? "settings" : "home";\n        _chromeCollapsed = false;')
s = s.replace('if (IsOpen && _pageId == pageId && !_chromeCollapsed)', 'if (IsOpen && _lmPage == (pageId == OracleWindowChrome.SettingsPageId ? "settings" : "home"))')
s = s.replace('        _settings.ShowAutoRecordPage();', '        _settings.ShowAutoRecordPage();\n        _lmPage = "settings";')
s = s.replace('new Vector2(1100f, 630f)', 'new Vector2(1100f, 720f) * Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale')
p.write_text(s, encoding='utf-8')
shutil.copyfile(assets / 'ConfigWindow.LogMimic.cs', root / 'Oracle/UI/ConfigWindow.LogMimic.cs')
shutil.copyfile(assets / 'icon.png', root / 'Oracle/Data/plugin-icon.png')
replace('Oracle/Plugin.cs', '        _timelineConfigWindow = timelineWindow;', '        _timelineConfigWindow = timelineWindow;\n        _timelineConfigWindow.Replay = _replay;')
replace('Oracle/Plugin.cs', 'new WindowSystem("Oracle")', 'new WindowSystem("LogMimic")')
replace('Oracle/Plugin.cs', 'private const string CommandName = "/logmimic";', 'private const string CommandName = "/lm";')
replace('Oracle/Plugin.cs', '            info.Message = "Join our Discord for updates and support!";\n            info.DiscordUrl = "https://discord.gg/gRfxXNZWMs";\n            info.SupportUrl = "https://exatrines.github.io/support/";', '            info.Message = "Discord: 달라살려 | Based on Oracle (AGPL-3.0-or-later)";\n            info.DiscordUrl = null;\n            info.SupportUrl = null;')
replace('Oracle/Configuration.cs', '    public int Version { get; set; } = 1;', '    public int Version { get; set; } = 1;\n    public bool LogMimicThemeApplied { get; set; }')
replace('Oracle/UI/OracleTheme.cs', '    public static bool IsBuiltin(string id) => MirageColorSettings.IsBuiltinTheme(id);', '    public static bool IsBuiltin(string id) => id == "logmimic-midnight" || MirageColorSettings.IsBuiltinTheme(id);')
replace('Oracle/UI/OracleTheme.cs', '            Builtin(MirageColorSettings.WhiteThemeId,', '            Builtin("logmimic-midnight", "LogMimic Midnight", CreateLogMimic()),\n            Builtin(MirageColorSettings.WhiteThemeId,')
replace('Oracle/UI/OracleTheme.cs', '        Apply();\n    }\n\n    public static void Select', '        if (!C.LogMimicThemeApplied)\n        {\n            C.SelectedThemeId = "logmimic-midnight";\n            C.LogMimicThemeApplied = true;\n            C.Save();\n        }\n        Apply();\n    }\n\n    public static void Select')
replace('Oracle/UI/OracleTheme.cs', '    public static void Bind()', '    private static MirageColorSettings CreateLogMimic()\n    {\n        var t = MirageColorSettings.CreateBlack();\n        t.WindowBg = new(0.045f, 0.070f, 0.105f, 1f);\n        t.ChromeBg = t.WindowBg;\n        t.PageBg = new(0.063f, 0.098f, 0.141f, 1f);\n        t.ChildBg = new(0.075f, 0.118f, 0.165f, 1f);\n        t.FrameBg = new(0.11f, 0.17f, 0.23f, 1f);\n        t.FrameBgHovered = new(0.15f, 0.23f, 0.29f, 1f);\n        t.TitleBg = t.WindowBg;\n        t.Separator = new(0.20f, 0.30f, 0.36f, 1f);\n        t.BlurStrength = 0f;\n        t.ApplyAccent(new(0.275f, 0.898f, 0.753f, 1f));\n        t.SetColor(MirageUi.Color.Title, new(0.275f, 0.898f, 0.753f, 1f));\n        t.SetColor(MirageUi.Color.Secondary, new(0.65f, 0.74f, 0.81f, 1f));\n        return t;\n    }\n\n    public static void Bind()')
for p in (root / 'Oracle').rglob('*.cs'):
    s = p.read_text(encoding='utf-8').replace('/oracle ', '/lm ').replace('/logmimic ', '/lm ')
    p.write_text(s, encoding='utf-8')
for lang in ('en', 'ja'):
    p = root / f'Oracle/Data/I18n/{lang}.json'
    m = json.loads(p.read_text(encoding='utf-8'))
    m = {k: v.replace('Oracle', 'LogMimic').replace('/oracle', '/lm').replace('/logmimic', '/lm') for k, v in m.items()}
    if lang == 'en':
        m.update(json.loads((assets / 'en-extra.json').read_text(encoding='utf-8')))
    p.write_text(json.dumps(m, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
shutil.copyfile(assets / 'ko.json', root / 'Oracle/Data/I18n/ko.json')
replace('Oracle/Oracle.csproj', '        <None Update="Data\\I18n\\en.json">', '        <None Update="Data\\I18n\\ko.json">\n            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>\n        </None>\n        <None Update="Data\\I18n\\en.json">')
replace('Oracle/I18n/I18n.cs', 'var concrete = _lang == "ja" ? "ja" : "en";', 'var concrete = _lang is "ja" or "ko" ? _lang : "en";')
replace('Oracle/I18n/I18n.cs', 'var next = string.Equals(NormalizeLangCode(langCode), "ja", StringComparison.Ordinal) ? "ja" : "en";', 'var normalized = NormalizeLangCode(langCode);\n        var next = normalized is "ja" or "ko" ? normalized : "en";')
replace('Oracle/I18n/I18n.cs', '        var map = ReadLangFile(_lang);\n        if (map.Count == 0 && !string.Equals(_lang, "en", StringComparison.Ordinal))\n            map = ReadLangFile("en");', '        var map = ReadLangFile("en");\n        foreach (var pair in ReadLangFile(_lang)) map[pair.Key] = pair.Value;\n        MirageUi.I18n.Register("ko", ReadLangFile("ko"));')
f = 'MirageUI/MirageUI/I18n/MirageUiI18n.cs'
replace(f, '        private static readonly Dictionary<string, string> JapaneseTable', '        private static readonly Dictionary<string, string> KoreanTable = new(StringComparer.Ordinal);\n        private static readonly Dictionary<string, string> JapaneseTable')
replace(f, 'var next = IsJapaneseCode(language) ? Japanese : English;', 'var next = language?.StartsWith("ko", StringComparison.OrdinalIgnoreCase) == true ? "ko" : IsJapaneseCode(language) ? Japanese : English;')
replace(f, 'var table = IsJapaneseCode(language) ? JapaneseTable : EnglishTable;', 'var table = language.StartsWith("ko", StringComparison.OrdinalIgnoreCase) ? KoreanTable : IsJapaneseCode(language) ? JapaneseTable : EnglishTable;')
replace(f, '            if (IsJapanese && JapaneseTable.TryGetValue', '            if (Language == "ko" && KoreanTable.TryGetValue(key, out var korean)) return korean;\n            if (IsJapanese && JapaneseTable.TryGetValue')
replace(f, '            JapaneseTable.Clear();', '            JapaneseTable.Clear();\n            KoreanTable.Clear();')
shutil.copyfile(assets / 'LanguagePage.cs', root / 'Oracle/UI/LanguagePage.cs')
replace('Oracle/UI/PluginSettingsWindow.cs', 'private string _selectedTabId = SettingsPluginLog;', 'private string _selectedTabId = SettingsFFLogsApi;')
print('Applied LogMimic UI, icon, /lm command and EN/KO localization.')
from localize_replay import apply
apply(root, assets)
replace('Oracle/UI/ConfigWindow.cs', '    public void SelectImportedTimeline(TimelineDocument doc)\n    {', '    public void SelectImportedTimeline(TimelineDocument doc)\n    {\n        _lmPage = "library";')

replace("Oracle/UI/DebugWindow.cs", '"Oracle Debug"', '"LogMimic Debug"')
