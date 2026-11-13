using Oracle.Models;

namespace Oracle.UI;

internal sealed partial class ConfigWindow
{
    private void DrawReplayPhaseSettings(TimelineDocument doc, string id)
    {
        if (!doc.ReplayEnabled) return;
        using var scope = ImRaii.PushId(id);
        using (ImRaii.Disabled(_engine.IsRunning))
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(I18n.Get("phase.mode"));
            ImGui.SetNextItemWidth(-1);
            var labels = new[] { I18n.Get("phase.continue"), I18n.Get("phase.align") };
            var mode = doc.ReplayPhaseMode == ReplayPhaseMode.AlignToPhase ? 1 : 0;
            if (ImGui.Combo("##replayPhaseMode", ref mode, labels, labels.Length))
            {
                doc.ReplayPhaseMode = (ReplayPhaseMode)mode;
                PersistDocument(doc);
            }
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(I18n.Get("phase.before_pull"));
        MirageUi.Text(I18n.Get(doc.ReplayPhaseMode == ReplayPhaseMode.AlignToPhase ? "phase.align_help" : "phase.continue_help"), MirageUi.Color.Secondary);
        if (!C.ReplayFollowCurrentGcd)
            MirageUi.Text(I18n.Get("phase.requires_adaptive"), MirageUi.Color.Secondary);
    }
}
