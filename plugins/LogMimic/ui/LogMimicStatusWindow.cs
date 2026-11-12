using Dalamud.Interface.Utility;
using Oracle.UI;
namespace Oracle.Services;

internal sealed class LogMimicStatusWindow : Window
{
    private readonly LogMimicService _replay;
    public LogMimicStatusWindow(LogMimicService replay)
        : base("LogMimic Replay##lmReplayFeedback", ImGuiWindowFlags.NoDecoration |
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing)
    {
        _replay = replay; IsOpen = true; RespectCloseHotkey = false; DisableWindowSounds = true;
        Position = new Vector2(60, 120); PositionCondition = ImGuiCond.FirstUseEver;
    }
    public override bool DrawConditions() => C.ReplayShowFeedback && PluginServices.ClientState.IsLoggedIn
        && _replay.IsFeedbackContextActive;
    public override void PreDraw()
    {
        var bg = OracleTheme.Current.WindowBg; bg.W = 0.94f;
        ImGui.PushStyleColor(ImGuiCol.WindowBg, bg);
        ImGui.PushStyleColor(ImGuiCol.Text, OracleTheme.Current.GetColor(MirageUi.Color.Default));
    }
    public override void PostDraw() => ImGui.PopStyleColor(2);
    public override void Draw()
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.PushTextWrapPos(440 * scale);
        try
        {
            ImGui.TextColored(OracleTheme.Current.GetColor(MirageUi.Color.Accent), "LOGMIMIC");
            ImGui.TextUnformatted(_replay.LoadedPresetName);
            ImGui.TextWrapped(_replay.State);
            ImGui.TextUnformatted(I18n.Format("feedback.clock", _replay.ClockSeconds));
            ImGui.Separator();
            if (_replay.NextMainCue is { } next)
            {
                Icon(next.ActionId, 38 * scale); ImGui.SameLine();
                ImGui.BeginGroup();
                ImGui.TextUnformatted(I18n.Get("feedback.next") + ": " + ActionLookup.GetName(next.ActionId));
                ImGui.TextUnformatted(I18n.Format("feedback.planned", _replay.NextPlannedAt));
                ImGui.EndGroup();
            }
            if (_replay.Feedback.LastRequest is { } requested)
            {
                ImGui.TextUnformatted(I18n.Get("feedback.request") + ": " + ActionLookup.GetName(requested.ActionId));
                ImGui.TextWrapped(I18n.Format("feedback.request_detail", requested.RequestedAt, requested.RequestedAt - requested.PlannedAt, requested.Target));
            }
            if (_replay.Feedback.LastConfirmed is { } confirmed)
            {
                Icon(confirmed.ActionId, 30 * scale); ImGui.SameLine();
                ImGui.TextUnformatted(I18n.Format("feedback.confirmed", ActionLookup.GetName(confirmed.ActionId), confirmed.ConfirmedAt ?? 0));
            }
            if (_replay.ReplayClockRunning && !_replay.Suspended && _replay.Feedback.LastGameStatus != 0)
                ImGui.TextUnformatted(I18n.Format("feedback.game_wait", _replay.Feedback.LastGameStatus));
            ImGui.TextDisabled(I18n.Get("feedback.legend"));
        }
        finally { ImGui.PopTextWrapPos(); }
        MirageWindowHeader.DragClientArea(this);
    }
    private static void Icon(uint id, float size) => MirageUi.GameIcon(ActionLookup.GetIconId(id), size, size, framed: true);
}
