using Dalamud.Interface.Utility;
using Oracle.Models;

namespace Oracle.UI;

internal sealed partial class ConfigWindow
{
    private static string RecordedTargetLabel(TimelineCue cue)
    {
        if (cue.ReplayTargetKind == "self") return I18n.Get("lm.target_self");
        if (!string.IsNullOrWhiteSpace(cue.ReplayTargetName)) return cue.ReplayTargetKind == "enemy" ? TargetNameLookup.DisplayName(cue.ReplayTargetName) : cue.ReplayTargetName;
        return cue.ReplayTargetLogId != 0
            ? I18n.Format("lm.target_actor", cue.ReplayTargetLogId)
            : I18n.Get("lm.target_unknown");
    }

    private static string RecordedTargetTooltip(TimelineCue cue) =>
        I18n.Format("lm.recorded_target_tooltip",
            string.IsNullOrWhiteSpace(cue.ReplayTargetName) ? RecordedTargetLabel(cue) : cue.ReplayTargetName,
            cue.ReplayTargetLogId);

    private static void DrawRecordedTarget(string label, string? tooltip, float width)
    {
        var size = new Vector2(width, CueActionIconSize);
        ImGui.InvisibleButton("##recordedTarget", size);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(min, max, true);
        draw.AddText(min + new Vector2(2 * ImGuiHelpers.GlobalScale, Math.Max(0, (size.Y - ImGui.GetTextLineHeight()) / 2)),
            ImGui.GetColorU32(OracleTheme.Current.GetColor(MirageUi.Color.Secondary)), "→ " + label);
        draw.PopClipRect();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip ?? label);
    }
}
