using Dalamud.Game.ClientState.Objects.Types;

namespace Oracle.Services;

internal sealed unsafe partial class LogMimicService
{
    private IGameObject? ResolveTarget(uint actionId, out string? reason)
    {
        var player = PluginServices.ObjectTable.LocalPlayer;
        var selected = PluginServices.TargetManager.Target;
        var choice = ReplayManualTarget.Choose(ReplayNativeTiming.Traits(actionId), player?.GameObjectId,
            selected == null ? null : new ReplayManualObject(selected.GameObjectId, selected.IsDead, selected.IsTargetable));
        reason = choice.ObjectId == null ? I18n.Get("manual_target.wait") : null;
        return choice.ObjectId == null ? null : choice.Self ? player : selected;
    }
}
