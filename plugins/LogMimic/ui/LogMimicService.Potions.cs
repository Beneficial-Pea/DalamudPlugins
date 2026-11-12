using FFXIVClientStructs.FFXIV.Client.Game;
using Oracle.Models;

namespace Oracle.Services;

internal sealed unsafe partial class LogMimicService
{
    private ReplayPotionLane? _potions;
    private ReplayPotionSettings _potionSettings = new();
    public string LastPotionResult { get; private set; } = string.Empty;

    private void PreparePotions(TimelineDocument doc)
    {
        _potionSettings = doc.ReplayPotions.Copy();
        // Recognize foreign/newer log potion IDs, but use only the chosen local-client item.
        _potions = new(doc.Cues.Where(c => c.Kind == TimelineCueKind.Item && ReplayPotionCatalog.IsStatPotion(c.ReplayItemId))
            .Select(c => new ReplayPotionMark(c.Id, c.TimeOffsetSec)));
    }

    private bool UpdatePotions(long now) => _potions?.Dispatch(_engine.ElapsedSeconds, _ =>
    {
        if (!_potionSettings.Enabled) return ReplayPotionAttempt.Skip;
        var chosen = ReplayPotionCatalog.Select(_potionSettings, _doc!.ClassJobId);
        if (chosen is not { } item)
        {
            LastPotionResult = I18n.Get("potions.skipped_missing");
            return ReplayPotionAttempt.Skip;
        }
        var player = PluginServices.ObjectTable.LocalPlayer;
        var manager = ActionManager.Instance();
        if (manager == null || player is not { IsDead: false }) return ReplayPotionAttempt.Retry;
        var window = ReplayNativeTiming.Read(manager);
        // A ready GCD always has priority. Items need a longer animation gap than abilities.
        var noMoreGcd = _adaptiveMode ? _adaptive?.NextGcd == null : _mainIndex >= _main.Count;
        if (!window.CanWeave(noMoreGcd, 1.1f)) return ReplayPotionAttempt.Retry;
        var status = manager->GetActionStatus(ActionType.Item, item.NativeId, player.GameObjectId);
        if (status != 0)
        {
            LastPotionResult = I18n.Format("potions.skipped_status", status);
            return ReplayPotionAttempt.Skip;
        }
        // Same native item path used by RSR. Never call the spell/action API for an item.
        var sent = manager->UseAction(ActionType.Item, item.NativeId, player.GameObjectId, 65535);
        LastPotionResult = sent
            ? I18n.Format("potions.used", ReplayPotionCatalog.ItemName(item.ItemId) + (item.Hq ? " HQ" : string.Empty))
            : I18n.Get("potions.skipped_rejected");
        if (sent) _lastAttempt = now;
        return sent ? ReplayPotionAttempt.Sent : ReplayPotionAttempt.Skip;
    }) ?? false;
}
