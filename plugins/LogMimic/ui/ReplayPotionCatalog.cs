using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using Oracle.Models;

namespace Oracle.Services;

internal sealed record ReplayPotionEntry(uint Id, string Name, uint Icon, int PrimaryStat, int ItemLevel);

internal static unsafe class ReplayPotionCatalog
{
    private static IReadOnlyList<ReplayPotionEntry>? _items;
    public static IReadOnlyList<ReplayPotionEntry> Items => _items ??= Load();

    private static IReadOnlyList<ReplayPotionEntry> Load()
    {
        var sheet = PluginServices.DataManager.GetExcelSheet<Item>();
        if (sheet == null) return [];
        return sheet.Where(i => i.ItemSearchCategory.RowId == 43 && i.FilterGroup == 6)
            .Select(i => new ReplayPotionEntry(i.RowId, i.Name.ExtractText(), i.Icon, i.Unknown4 switch
            {
                10120 => 1, 10140 => 2, 10160 => 3, 10180 => 4, 10200 => 5, _ => 0,
            }, (int)i.LevelItem.RowId))
            .Where(i => i.PrimaryStat != 0).OrderByDescending(i => i.ItemLevel).ThenByDescending(i => i.Id).ToArray();
    }

    public static int PrimaryStat(uint jobId) => (int)(PluginServices.DataManager.GetExcelSheet<ClassJob>()?
        .GetRowOrDefault(jobId)?.PrimaryStat ?? 0);
    public static int Count(uint id, bool hq)
    {
        var inventory = InventoryManager.Instance();
        return inventory == null ? 0 : inventory->GetInventoryItemCount(id, hq);
    }

    public static ReplayPotionChoice? Select(ReplayPotionSettings settings, uint jobId) =>
        ReplayPotionPolicy.Select(settings, PrimaryStat(jobId), Items
            .Where(i => settings.ItemId == 0 || i.Id == settings.ItemId)
            .Select(i => new ReplayPotionCandidate(i.Id, i.PrimaryStat, i.ItemLevel, Count(i.Id, false), Count(i.Id, true))));

    public static ReplayPotionSettings ImportDefaults(uint jobId)
    {
        C.ReplayPotionImportDefaults ??= [];
        if (!C.ReplayPotionImportDefaults.TryGetValue(jobId, out var settings) || settings == null)
            C.ReplayPotionImportDefaults[jobId] = settings = new();
        return settings;
    }

    public static bool IsStatPotion(uint id) => ReplayPotionIds.Global.ContainsKey(id) || Items.Any(i => i.Id == id);
    public static string ItemName(uint id) => PluginServices.DataManager.GetExcelSheet<Item>()?
        .GetRowOrDefault(id)?.Name.ExtractText() ?? (ReplayPotionIds.Global.TryGetValue(id, out var known)
            ? known.Name : I18n.Format("potions.item_id", id));
    public static uint ItemIcon(uint id) => PluginServices.DataManager.GetExcelSheet<Item>()?
        .GetRowOrDefault(id)?.Icon ?? 0;
}
