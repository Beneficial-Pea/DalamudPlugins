namespace Oracle.UI;

internal static class LanguagePage
{
    public static void Draw()
    {
        MirageUi.Header(I18n.Get("language.title"));
        if (ImGui.Button("English")) I18n.SetLanguage("en");
        ImGui.SameLine();
        if (ImGui.Button("한국어")) I18n.SetLanguage("ko");
        ImGui.SameLine();
        if (ImGui.Button("日本語")) I18n.SetLanguage("ja");
    }
}
