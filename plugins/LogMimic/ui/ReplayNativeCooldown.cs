using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Oracle.Services;

internal static unsafe class ReplayNativeCooldown
{
    public static float Remaining(ActionManager* manager, uint actionId)
    {
        var group = ReplayCooldownPolicy.PersonalGroup(ReplayNativeTiming.Traits(actionId));
        if (manager == null || group == 0) return 0;
        var detail = manager->GetRecastGroupDetail(group - 1);
        if (detail == null || !detail->IsActive) return 0;
        var current = manager->GetCurrentCharges(actionId);
        if (current > 0) return 0;
        var player = PlayerState.Instance();
        if (player == null) return 0;
        var level = (uint)(player->IsLevelSynced ? player->SyncedLevel : player->CurrentLevel);
        var max = Math.Max(1u, ActionManager.GetMaxCharges(actionId, level));
        var perCharge = max > 1 ? ActionManager.GetAdjustedRecastTime(ActionType.Action, actionId) / 1000f : 0;
        return ReplayCooldownPolicy.Remaining(new(group, detail->IsActive, detail->Total, detail->Elapsed,
            current, max, perCharge));
    }
}
