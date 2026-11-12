using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace Oracle.Services;

internal sealed unsafe partial class HotbarHighlightService
{
    public LogMimicService? Replay { get; set; }
    private void DrawReplayFeedback()
    {
        if (!C.ReplayShowHotbarPulse || Replay == null || !PluginServices.ClientState.IsLoggedIn) return;
        var hotbar = RaptureHotbarModule.Instance();
        if (hotbar == null) return;
        var next = new Dictionary<uint,bool>();
        var request = new Dictionary<uint,bool>();
        var confirmed = new Dictionary<uint,bool>();
        if (Replay.ReplayClockRunning && !Replay.Suspended && Replay.NextMainCue is { } cue
            && Replay.NextPlannedAt - Replay.ClockSeconds <= 1f)
            AddActionAndAdjusted(next, cue.ActionId, false);
        var now = Environment.TickCount64;
        if (Replay.Feedback.RequestPulse(now) is var requestedId && requestedId != 0) AddActionAndAdjusted(request, requestedId, false);
        if (Replay.Feedback.ConfirmPulse(now) is var confirmedId && confirmedId != 0) AddActionAndAdjusted(confirmed, confirmedId, true);
        if (next.Count + request.Count + confirmed.Count == 0) return;
        foreach (var name in NormalActionBarAddonNames) DrawReplayBar(name, 0, hotbar, next, request, confirmed);
        DrawReplayBar("_ActionCross", 1, hotbar, next, request, confirmed);
        if (C.ShowHotbarHighlightDoubleCross)
            foreach (var name in WxhbAddonNames) DrawReplayBar(name, 2, hotbar, next, request, confirmed);
    }
    private static void DrawReplayBar(string name, int type, RaptureHotbarModule* hotbar,
        Dictionary<uint,bool> next, Dictionary<uint,bool> request, Dictionary<uint,bool> confirmed)
    {
        if (!TryGetActionBar(name, out var addon)) return;
        if (type == 1 ? !IsCrossAddonShown(addon) : !IsAddonVisible(addon)) return;
        var count = type == 2 ? Math.Min(8, AddonSlotCount(addon)) : AddonSlotCount(addon);
        var draw = ImGui.GetForegroundDrawList();
        for (var i=0; i<count; i++)
        {
            byte bar, slotId;
            if (type == 1) { if (!TryResolveCrossSlot(addon, hotbar, i, out bar, out slotId)) continue; }
            else if (type == 2) { if (!TryResolveWxhbSlot(addon, i, out bar, out slotId)) continue; }
            else { bar = addon->RaptureHotbarId; slotId = (byte)i; }
            if (!C.IsHotbarHighlightEnabled(bar)) continue;
            var slot = hotbar->GetSlotById(bar,slotId);
            if (slot == null || slot->IsEmpty) continue;
            var phase = TryMatchSlotPhase(slot, confirmed, out _) ? 2
                : TryMatchSlotPhase(slot, request, out _) ? 1 : TryMatchSlotPhase(slot, next, out _) ? 0 : -1;
            if (phase < 0) continue;
            ref var barSlot = ref addon->ActionBarSlotVector[i];
            if (!TryGetSlotScreenRect(ref barSlot, type != 0, out var min, out var max)) continue;
            var accent = Oracle.UI.OracleTheme.Current.GetColor(MirageUi.Color.Accent);
            var fill = phase == 2 ? new Vector4(1,1,1,0.24f) : new Vector4(accent.X,accent.Y,accent.Z,0.22f);
            if (phase > 0) draw.AddRectFilled(min,max,ImGui.GetColorU32(fill),2);
            var border = phase == 2 ? Vector4.One : accent;
            draw.AddRect(min,max,ImGui.GetColorU32(border),2,ImDrawFlags.None,phase > 0 ? 3 : 2);
        }
    }
}
