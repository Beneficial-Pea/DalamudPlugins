using FFXIVClientStructs.FFXIV.Client.Game;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace Oracle.Services;

internal static unsafe class ReplayNativeTiming
{
    public static ReplayActionTraits Traits(uint actionId)
    {
        var row = PluginServices.DataManager.GetExcelSheet<LuminaAction>()?.GetRowOrDefault(actionId);
        if (row is not { } action) return default;
        return new(action.CooldownGroup, action.AdditionalCooldownGroup, action.CanTargetSelf,
            action.CanTargetHostile, action.CanTargetParty, action.CanTargetAlliance, action.TargetArea);
    }

    public static bool IsGcd(uint actionId) => Traits(actionId).IsGcd;
    public static bool UsesSelf(uint actionId, int targetId) => Traits(actionId).UsesSelf(targetId);

    public static ReplayNativeWindow Read(ActionManager* manager)
    {
        // The sheet's global cooldown group is 58; native group indexes are zero-based.
        var gcd = manager->GetRecastGroupDetail(57);
        var remaining = gcd != null && gcd->IsActive ? Math.Max(0, gcd->Total - gcd->Elapsed) : 0;
        var cast = manager->CastActionId != 0 ? Math.Max(0, manager->CastTimeTotal - manager->CastTimeElapsed) : 0;
        return new(remaining, cast, Math.Max(0, manager->AnimationLock), manager->ActionQueued,
            manager->ActionQueued ? manager->QueuedActionId : 0, cast > 0 ? manager->CastActionId : 0,
            manager->LastUsedActionSequence,
            manager->ActionQueued ? (ulong)manager->QueuedTargetId : 0,
            manager->ActionQueued ? (uint)manager->QueuedActionType : 0);
    }
}
