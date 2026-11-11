using Dalamud.Interface.Utility;
using Dalamud.Interface.Textures;
using Oracle.Models;

namespace Oracle.UI;

internal sealed partial class ConfigWindow
{
    public LogMimicService? Replay { private get; set; }
    private string _lmPage = "home";
    private int _targetActorId;
    private float _lmNavWidth;
    private bool _lmMinimized;
    private bool _lmRestoreSize;
    private Vector2 _lmExpandedSize;
    private static string L(string key) => I18n.Get("lm." + key);

    private void DrawLogMimic()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetWindowSize().X;
        var wasMinimized = _lmMinimized;
        if (!_lmMinimized) _lmExpandedSize = ImGui.GetWindowSize();
        ImGui.SetCursorPos(new Vector2(20, 14) * scale);
        var icon = MirageUi.PluginInfo.IconPath;
        if (!string.IsNullOrEmpty(icon))
        {
            var texture = PluginServices.TextureProvider.GetFromFile(icon).GetWrapOrDefault();
            if (texture != null)
            {
                ImGui.Image(texture.Handle, new Vector2(44, 44) * scale);
                ImGui.SameLine();
            }
        }
        ImGui.BeginGroup();
        MirageUi.Text("LOGMIMIC", MirageUi.Color.Title, fontSize: MirageUi.FontSize.Large);
        MirageUi.Text(L("tagline"), MirageUi.Color.Secondary);
        ImGui.EndGroup();
        var style = ImGui.GetStyle();
        var controlsWidth = ImGui.CalcTextSize("EN").X + ImGui.CalcTextSize("한국어").X
            + ImGui.CalcTextSize("/lm").X + ImGui.CalcTextSize("X").X
            + ImGui.CalcTextSize(_lmMinimized ? "+" : "-").X
            + style.FramePadding.X * 8 + style.ItemSpacing.X * 4;
        ImGui.SetCursorPos(new Vector2(width - 12 * scale - controlsWidth, 14 * scale));
        if (ImGui.Button("EN##lmLanguage")) I18n.SetLanguage("en");
        ImGui.SameLine();
        if (ImGui.Button("한국어##lmLanguage")) I18n.SetLanguage("ko");
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("/lm");
        ImGui.SameLine();
        if (ImGui.Button((_lmMinimized ? "+" : "-") + "##lmMinimize")) ToggleLogMimicMinimize();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(L(_lmMinimized ? "restore" : "minimize"));
        ImGui.SameLine();
        if (ImGui.Button("X##lmClose")) IsOpen = false;
        if (_lmMinimized || wasMinimized) return;
        ImGui.SetCursorPos(new Vector2(20, 76) * scale);
        _lmNavWidth = (width - 40 * scale - style.ItemSpacing.X * 5) / 6;
        Nav("home", "home"); ImGui.SameLine();
        Nav("library", "library"); ImGui.SameLine();
        Nav("import", "import"); ImGui.SameLine();
        Nav("targets", "targets"); ImGui.SameLine();
        Nav("settings", "settings"); ImGui.SameLine();
        Nav("about", "about");
        ImGui.SetCursorPos(new Vector2(0, 116) * scale);
        ImGui.Separator();
        var bodyHeight = Math.Max(80 * scale, ImGui.GetWindowSize().Y - 150 * scale);
        ImGui.SetCursorPos(new Vector2(0, 128) * scale);
        using (var body = ImRaii.Child("##lmBody", new Vector2(0, bodyHeight), false))
        {
            if (body)
            {
                switch (_lmPage)
                {
                    case "library":
                        EnsureDefaultSelection();
                        var state = CreateTimelineTwoColumnState();
                        MirageUi.TwoColumn.Draw(state, DrawMainContent);
                        _sidebarCollapsed = state.SidebarCollapsed;
                        break;
                    case "import":
                        DrawImportSplit(() => _ffLogsImport.Draw(), () => DrawImportActionsColumn(_ffLogsImport.SelectedClassJobId, "lmImportActions"));
                        break;
                    case "settings": _settings.Draw(); break;
                    default:
                        ImGui.SetCursorPos(new Vector2(24, 12) * scale);
                        using (var content = ImRaii.Child("##lmContent", new Vector2(-24 * scale, 0), false))
                        {
                            if (content)
                            {
                                if (_lmPage == "targets") DrawReplayTargets();
                                else if (_lmPage == "about") DrawLogMimicAbout();
                                else DrawReplayHome();
                            }
                        }
                        break;
                }
            }
        }
        ImGui.SetCursorPos(new Vector2(20 * scale, ImGui.GetWindowSize().Y - 24 * scale));
        ImGui.TextDisabled("LogMimic  |  " + L("footer"));
        DrawResizeGrip();
    }

    private void InitializeLogMimicSize()
    {
        _lmMinimized = C.LogMimicMinimized;
        _lmExpandedSize = new Vector2(Math.Max(900, C.LogMimicExpandedWidth),
            Math.Max(540, C.LogMimicExpandedHeight)) * ImGuiHelpers.GlobalScale;
        _lmRestoreSize = _lmMinimized;
        ConfigureLogMimicSize();
    }

    private void ToggleLogMimicMinimize()
    {
        _lmMinimized = !_lmMinimized;
        _lmRestoreSize = true;
        C.LogMimicMinimized = _lmMinimized;
        C.LogMimicExpandedWidth = _lmExpandedSize.X / ImGuiHelpers.GlobalScale;
        C.LogMimicExpandedHeight = _lmExpandedSize.Y / ImGuiHelpers.GlobalScale;
        C.Save();
    }

    private void ConfigureLogMimicSize()
    {
        if (_lmMinimized)
        {
            Flags |= ImGuiWindowFlags.NoResize;
            var compact = new Vector2(_lmExpandedSize.X, 68 * ImGuiHelpers.GlobalScale);
            Size = compact;
            SizeCondition = ImGuiCond.Always;
            SizeConstraints = new() { MinimumSize = compact, MaximumSize = compact };
            return;
        }
        Flags &= ~ImGuiWindowFlags.NoResize;
        SizeCondition = _lmRestoreSize ? ImGuiCond.Always : ImGuiCond.FirstUseEver;
        if (_lmRestoreSize)
        {
            Size = _lmExpandedSize;
            _lmRestoreSize = false;
        }
        SizeConstraints = new()
        {
            MinimumSize = new Vector2(900, 540) * ImGuiHelpers.GlobalScale,
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    private static void DrawResizeGrip()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var corner = ImGui.GetWindowPos() + ImGui.GetWindowSize() - new Vector2(4 * scale);
        var color = ImGui.GetColorU32(OracleTheme.Current.GetColor(MirageUi.Color.Accent));
        var draw = ImGui.GetWindowDrawList();
        for (var length = 4; length <= 12; length += 4)
            draw.AddLine(corner - new Vector2(length * scale, 0),
                corner - new Vector2(0, length * scale), color, Math.Max(1, scale));
        if (ImGui.IsMouseHoveringRect(corner - new Vector2(18 * scale), corner))
            ImGui.SetTooltip(L("resize"));
    }

    private void Nav(string id, string key)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Button,
            _lmPage == id ? OracleTheme.Current.HeaderActive : OracleTheme.Current.FrameBg);
        if (ImGui.Button(L(key) + "##lmNav" + id, new Vector2(_lmNavWidth, 30 * ImGuiHelpers.GlobalScale)))
            _lmPage = id;
    }

    private void DrawReplayHome()
    {
        MirageUi.Header(L("session"));
        ImGui.TextWrapped(L("intro"));
        ImGui.Spacing();
        var active = _engine.ActiveDocument;
        ImGui.TextUnformatted(L("loaded") + ": " + (active?.Name ?? L("none")));
        ImGui.TextUnformatted(L("status") + ": " + (Replay?.State ?? L("none")));
        ImGui.TextUnformatted(L("mode") + ": " + (_engine.IsPreview ? L("preview") : _engine.IsRunning ? L("combat") : L("standby")));
        ImGui.Spacing();
        using (ImRaii.Disabled(Replay == null || !_engine.IsRunning || _engine.IsPreview || active?.ReplayEnabled != true))
        {
            if (ImGui.Button(L("start") + "##lmStart")) Replay?.Command(["on"]);
        }
        ImGui.SameLine();
        if (ImGui.Button(L("pause") + "##lmPause")) Replay?.Command(["off"]);
        ImGui.SameLine();
        if (ImGui.Button(L("import") + "##lmQuickImport")) _lmPage = "import";
        ImGui.Spacing();
        ImGui.TextWrapped(L("manual"));
        ImGui.Separator();
        MirageUi.Header(L("presets"));
        ImGui.TextWrapped(L("autoload_help"));
        var presets = _store.Documents.Where(d => d.ReplayEnabled).ToList();
        if (presets.Count == 0)
        {
            ImGui.TextWrapped(L("empty"));
            return;
        }
        foreach (var doc in presets)
        {
            using var id = ImRaii.PushId(doc.Id);
            ImGui.TextUnformatted(doc.Name);
            var autoLoad = doc.AutoLoadEnabled;
            using (ImRaii.Disabled(doc.TerritoryTypeId == 0 || doc.ClassJobId == 0))
            {
                if (ImGui.Checkbox(L("autoload") + "##lmAutoLoad", ref autoLoad))
                {
                    ReplayAutoLoad.Select(_store, doc, autoLoad);
                    if (!_engine.IsRunning) _engine.Unload();
                }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(L("autoload_help"));
            ImGui.SameLine();
            if (ImGui.SmallButton(L("load"))) Replay?.Command(["load", doc.Id]);
            ImGui.SameLine();
            if (ImGui.SmallButton(L("edit")))
            {
                OnSidebarSelectionChanged(TimelinePageId(doc.Id));
                _lmPage = "library";
            }
        }
    }

    private void DrawReplayTargets()
    {
        MirageUi.Header(L("targets"));
        ImGui.TextWrapped(L("target_help"));
        ImGui.InputInt(L("actor_id"), ref _targetActorId);
        using (ImRaii.Disabled(Replay == null || !Replay.HasReplayDocument || _targetActorId <= 0 || PluginServices.TargetManager.Target == null))
        {
            if (ImGui.Button(L("bind"))) Replay?.Command(["bind", _targetActorId.ToString()]);
        }
        ImGui.SameLine();
        if (ImGui.Button(L("list_targets"))) Replay?.Command(["targets"]);
        ImGui.Separator();
        var doc = _engine.ActiveDocument;
        if (doc == null) { ImGui.TextWrapped(L("empty_target")); return; }
        foreach (var cue in doc.Cues.Where(c => c.ReplayTargetLogId > 0).GroupBy(c => c.ReplayTargetLogId).Select(g => g.First()))
            ImGui.TextUnformatted($"{cue.ReplayTargetLogId}  |  {cue.ReplayTargetName}");
    }

    private static void DrawLogMimicAbout()
    {
        MirageUi.Header("LogMimic");
        ImGui.TextWrapped(L("intro"));
        ImGui.Spacing();
        ImGui.TextWrapped(L("support"));
        ImGui.Separator();
        ImGui.TextWrapped(L("credits"));
        ImGui.TextWrapped(L("experimental"));
    }
}
