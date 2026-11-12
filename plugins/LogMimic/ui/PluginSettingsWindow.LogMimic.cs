namespace Oracle.UI;

internal sealed partial class PluginSettingsWindow
{
    public bool TimelineIsRunning { private get; set; }

    private void DrawLogMimicSettings()
    {
        MirageUi.Header(I18n.Get("lm.settings"));
        var autoLoad = C.LogMimicAutoLoadEnabled;
        if (ImGui.Checkbox(I18n.Get("lm.autoload"), ref autoLoad))
        {
            C.LogMimicAutoLoadEnabled = autoLoad;
            C.Save();
        }
        ImGui.TextWrapped(I18n.Get("lm.autoload_global_help"));
        ImGui.Separator();
        var hotbar = C.ReplayShowHotbarPulse;
        if (ImGui.Checkbox(I18n.Get("feedback.hotbar"), ref hotbar))
        {
            C.ReplayShowHotbarPulse = hotbar;
            C.Save();
        }
        using (ImRaii.Disabled(TimelineIsRunning))
        {
            var adaptive = C.ReplayFollowCurrentGcd;
            if (ImGui.Checkbox(I18n.Get("adaptive.enable"), ref adaptive))
            {
                C.ReplayFollowCurrentGcd = adaptive;
                C.Save();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(I18n.Get("adaptive.help"));
        }
        using (ImRaii.Disabled(TimelineIsRunning || C.ReplayFollowCurrentGcd))
        {
            var align = C.ReplayAlignFirstActionToZero;
            if (ImGui.Checkbox(I18n.Get("feedback.align"), ref align))
            {
                C.ReplayAlignFirstActionToZero = align;
                C.Save();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(I18n.Get("feedback.align_help"));
        }
    }
}
