using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Oracle.Services;

internal sealed unsafe partial class LogMimicService
{
    private readonly ReplayActionCondition _conditionWait = new();
    private bool _skippedCondition;
    public string LastConditionSkip { get; private set; } = string.Empty;

    private static string? AttackTargetBlockReason(uint actionId, IGameObject target)
    {
        if (!ReplayNativeTiming.Traits(actionId).Hostile) return null;
        var player = PluginServices.ObjectTable.LocalPlayer;
        if (player == null) return I18n.Get("adaptive.player_wait");
        if (!ActionManager.CanUseActionOnTarget(actionId, (GameObject*)target.Address))
            return I18n.Format("manual_target.unusable", target.Name.TextValue);
        var range = ActionManager.GetActionInRangeOrLoS(actionId, (GameObject*)player.Address, (GameObject*)target.Address);
        return range == 0 ? null : I18n.Format("manual_target.range", ActionLookup.GetName(actionId), target.Name.TextValue, range);
    }
}
