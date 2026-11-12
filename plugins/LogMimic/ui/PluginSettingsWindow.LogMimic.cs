namespace Oracle.UI;

internal sealed partial class PluginSettingsWindow
{
    private static void DrawLogMimicSettings()
    {
        MirageUi.Header(I18n.Get("lm.settings"));
        var autoLoad = C.LogMimicAutoLoadEnabled;
        if (ImGui.Checkbox(I18n.Get("lm.autoload"), ref autoLoad))
        {
            C.LogMimicAutoLoadEnabled = autoLoad;
            C.Save();
        }
        ImGui.TextWrapped(I18n.Get("lm.autoload_global_help"));
    }
}
