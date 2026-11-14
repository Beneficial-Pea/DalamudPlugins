using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Oracle.Models;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace Oracle.Services;

internal sealed unsafe partial class LogMimicService
{
    public string LastProcSubstitution { get; private set; } = string.Empty;

    private uint ResolveProcAction(ActionManager* manager, TimelineCue cue, uint adjusted)
    {
        if (_doc?.ClassJobId is not (23 or 38)) return adjusted;
        var player = PluginServices.ObjectTable.LocalPlayer;
        var state = PlayerState.Instance();
        if (player == null || state == null) return adjusted;
        var level = state->IsLevelSynced ? state->SyncedLevel : state->CurrentLevel;
        return ReplayProcPairs.Select(player.ClassJob.RowId, cue.ActionId, adjusted,
            id => manager->GetAdjustedActionId(id),
            id => player.StatusList.Any(s => s.StatusId == id && ReplayProcPairs.IsLiveStatus(s.StatusId, s.RemainingTime)),
            id => PluginServices.DataManager.GetExcelSheet<LuminaAction>()?.GetRowOrDefault(id) is { } row
                && row.ClassJobLevel <= level,
            _doc?.ReplayExcludedActionIds);
    }

    private bool OwnsReplayConfirmation(uint actionId)
    {
        if (_doc is not { ReplayEnabled: true } || actionId == 0
            || !ReferenceEquals(_doc, _engine.ActiveDocument)) return false;
        if (_pendingIndependentId is { } utility && Feedback.MatchesRequest(utility, actionId)) return true;
        if (_adaptiveMode) return _adaptive?.PendingGcd?.ActualActionId == actionId
            || _adaptive?.PendingOgcd?.ActualActionId == actionId;
        return _pendingMainId is { } main && Feedback.MatchesRequest(main, actionId);
    }
}
