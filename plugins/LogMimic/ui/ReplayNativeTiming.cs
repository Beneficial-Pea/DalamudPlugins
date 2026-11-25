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
        // RSR reads the default GCD through the native recast APIs for Heated
        // Split Shot. Resolve that shared timer through the client instead of
        // assuming a fixed native recast-group slot and reading its raw fields.
        const uint defaultGcdAction = 7411;
        var remaining = ReplayGcdReadiness.Remaining(
            manager->GetRecastTime(ActionType.Action, defaultGcdAction),
            manager->GetRecastTimeElapsed(ActionType.Action, defaultGcdAction));
        var cast = manager->CastActionId != 0 ? Math.Max(0, manager->CastTimeTotal - manager->CastTimeElapsed) : 0;
        return new(remaining, cast, Math.Max(0, manager->AnimationLock), manager->ActionQueued,
            manager->ActionQueued ? manager->QueuedActionId : 0, cast > 0 ? manager->CastActionId : 0,
            manager->LastUsedActionSequence,
            manager->ActionQueued ? (ulong)manager->QueuedTargetId : 0,
            manager->ActionQueued ? (uint)manager->QueuedActionType : 0);
    }
}
