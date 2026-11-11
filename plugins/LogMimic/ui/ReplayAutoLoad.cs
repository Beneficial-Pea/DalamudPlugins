using Oracle.Models;

namespace Oracle.Services;

internal static class ReplayAutoLoad
{
    private static bool SameSlot(TimelineDocument a, TimelineDocument b) =>
        a.TerritoryTypeId == b.TerritoryTypeId && a.ClassJobId == b.ClassJobId &&
        string.Equals(a.AutoLoadPresetId ?? "", b.AutoLoadPresetId ?? "", StringComparison.OrdinalIgnoreCase);

    public static void Select(TimelineStore store, TimelineDocument selected, bool enabled)
    {
        if (enabled && (selected.TerritoryTypeId == 0 || selected.ClassJobId == 0)) return;
        if (enabled)
        {
            foreach (var other in store.Documents.Where(d => !ReferenceEquals(d, selected) && d.AutoLoadEnabled && SameSlot(d, selected)).ToList())
            {
                other.AutoLoadEnabled = false;
                store.SaveDocument(other);
            }
        }
        selected.AutoLoadEnabled = enabled;
        store.SaveDocument(selected);
    }

    public static void MigrateOnce(TimelineStore store)
    {
        if (C.LogMimicAutoLoadMigrated) return;
        foreach (var doc in store.Documents.Where(d => d.ReplayEnabled && d.TerritoryTypeId != 0 && d.ClassJobId != 0).ToList())
        {
            if (store.Documents.Any(d => d.AutoLoadEnabled && SameSlot(d, doc))) continue;
            var preferred = store.ActiveDocument;
            Select(store, preferred != null && preferred.ReplayEnabled && SameSlot(preferred, doc) ? preferred : doc, true);
        }
        C.LogMimicAutoLoadMigrated = true;
        C.Save();
    }
}
