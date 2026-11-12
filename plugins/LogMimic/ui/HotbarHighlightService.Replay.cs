using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace Oracle.Services;

internal sealed unsafe partial class HotbarHighlightService
{
    public LogMimicService? Replay { get; set; }
    internal sealed record ReplayHotbarSlot(string AddonName, int Type, int Index,
        byte HotbarId, byte SlotId, uint CommandId);
    private readonly ReplayClickSequence<ReplayHotbarSlot> _replayClicks = new();

    // Capture before UseAction: a combo/stance action can change its apparent ID on use.
    public IReadOnlyList<ReplayHotbarSlot> CaptureReplayClick(uint actionId)
    {
        var slots = new List<ReplayHotbarSlot>();
        if (!C.ReplayShowHotbarPulse || !PluginServices.ClientState.IsLoggedIn) return slots;
        var hotbar = RaptureHotbarModule.Instance();
        if (hotbar == null) return slots;
        var actions = new Dictionary<uint, bool>();
        AddActionAndAdjusted(actions, actionId, false);
        foreach (var name in NormalActionBarAddonNames) CaptureReplayBar(name, 0, hotbar, actions, slots);
        CaptureReplayBar("_ActionCross", 1, hotbar, actions, slots);
        foreach (var name in WxhbAddonNames) CaptureReplayBar(name, 2, hotbar, actions, slots);
        return slots;
    }

    public void StartReplayClick(IReadOnlyList<ReplayHotbarSlot>? slots, long now)
    {
        _replayClicks.Start(slots, now);
        UpdateReplayClicks();
    }

    public void ClearReplayClicks() => _replayClicks.Clear();

    // Runs on Framework.Update, without delayed callbacks or retained native pointers.
    public void UpdateReplayClicks()
    {
        if (!C.ReplayShowHotbarPulse || !PluginServices.ClientState.IsLoggedIn
            || PluginServices.ObjectTable.LocalPlayer is not { IsDead: false }
            || Replay == null || !Replay.ReplayClockRunning || Replay.Suspended)
        {
            ClearReplayClicks();
            return;
        }
        var hotbar = RaptureHotbarModule.Instance();
        if (hotbar == null) { ClearReplayClicks(); return; }
        foreach (var captured in _replayClicks.TakeDue(Environment.TickCount64))
        {
            if (!TryGetVisibleReplayBar(captured.AddonName, captured.Type, out var addon)) continue;
            if (captured.Index < 0 || captured.Index >= ReplayBarCount(addon, captured.Type)) continue;
            if (!TryGetReplaySlot(addon, captured.Type, hotbar, captured.Index, out var bar, out var slotId)
                || bar != captured.HotbarId || slotId != captured.SlotId) continue;
            var slot = hotbar->GetSlotById(bar, slotId);
            if (slot == null || slot->IsEmpty || slot->CommandType != RaptureHotbarModule.HotbarSlotType.Action
                || slot->CommandId != captured.CommandId) continue;
            ref var barSlot = ref addon->ActionBarSlotVector[captured.Index];
            if (barSlot.Icon == null || !barSlot.Icon->AtkResNode.IsVisible()) continue;
            // The native hotbar keypress animation, also used by Rotation Solver Reborn.
            // This is visual only: do not execute the slot or dispatch a second UseAction.
            addon->PulseActionBarSlot(captured.Index);
        }
    }

    private static void CaptureReplayBar(string name, int type, RaptureHotbarModule* hotbar,
        Dictionary<uint, bool> actions, List<ReplayHotbarSlot> slots)
    {
        if (!TryGetVisibleReplayBar(name, type, out var addon)) return;
        for (var i = 0; i < ReplayBarCount(addon, type); i++)
        {
            if (!TryGetReplaySlot(addon, type, hotbar, i, out var bar, out var slotId)) continue;
            var slot = hotbar->GetSlotById(bar, slotId);
            if (slot == null || slot->IsEmpty || !TryMatchSlotPhase(slot, actions, out _)) continue;
            ref var barSlot = ref addon->ActionBarSlotVector[i];
            if (barSlot.Icon == null || !barSlot.Icon->AtkResNode.IsVisible()) continue;
            slots.Add(new(name, type, i, bar, slotId, slot->CommandId));
        }
    }

    private static bool TryGetVisibleReplayBar(string name, int type, out AddonActionBarBase* addon)
        => TryGetActionBar(name, out addon) && (type == 1 ? IsCrossAddonShown(addon) : IsAddonVisible(addon));

    private static int ReplayBarCount(AddonActionBarBase* addon, int type)
        => type == 2 ? Math.Min(8, AddonSlotCount(addon)) : AddonSlotCount(addon);

    private static bool TryGetReplaySlot(AddonActionBarBase* addon, int type, RaptureHotbarModule* hotbar,
        int index, out byte bar, out byte slotId)
    {
        if (type == 1) return TryResolveCrossSlot(addon, hotbar, index, out bar, out slotId);
        if (type == 2) return TryResolveWxhbSlot(addon, index, out bar, out slotId);
        bar = addon->RaptureHotbarId; slotId = (byte)index;
        return true;
    }

    private void DrawReplayFeedback()
    {
        if (!C.ReplayShowHotbarPulse || Replay == null || !PluginServices.ClientState.IsLoggedIn
            || !Replay.ReplayClockRunning || Replay.Suspended || Replay.NextMainCue is not { } cue
            || Replay.NextPlannedAt - Replay.ClockSeconds > 1f) return;
        var hotbar = RaptureHotbarModule.Instance();
        if (hotbar == null) return;
        var next = new Dictionary<uint, bool>();
        AddActionAndAdjusted(next, cue.ActionId, false);
        foreach (var name in NormalActionBarAddonNames) DrawReplayBar(name, 0, hotbar, next);
        DrawReplayBar("_ActionCross", 1, hotbar, next);
        if (C.ShowHotbarHighlightDoubleCross)
            foreach (var name in WxhbAddonNames) DrawReplayBar(name, 2, hotbar, next);
    }

    private static void DrawReplayBar(string name, int type, RaptureHotbarModule* hotbar,
        Dictionary<uint, bool> next)
    {
        if (!TryGetVisibleReplayBar(name, type, out var addon)) return;
        var draw = ImGui.GetForegroundDrawList();
        for (var i = 0; i < ReplayBarCount(addon, type); i++)
        {
            if (!TryGetReplaySlot(addon, type, hotbar, i, out var bar, out var slotId)
                || !C.IsHotbarHighlightEnabled(bar)) continue;
            var slot = hotbar->GetSlotById(bar, slotId);
            if (slot == null || slot->IsEmpty || !TryMatchSlotPhase(slot, next, out _)) continue;
            ref var barSlot = ref addon->ActionBarSlotVector[i];
            if (!TryGetSlotScreenRect(ref barSlot, type != 0, out var min, out var max)) continue;
            var accent = Oracle.UI.OracleTheme.Current.GetColor(MirageUi.Color.Accent);
            draw.AddRect(min, max, ImGui.GetColorU32(accent), 2, ImDrawFlags.None, 2);
        }
    }
}
