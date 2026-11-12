using Oracle.Models;

namespace Oracle.UI;

internal static class ReplayPotionUi
{
    private static string _search = string.Empty;
    private static bool _ownedOnly;

    public static bool Draw(ReplayPotionSettings settings, uint jobId, string id)
    {
        using var scope = ImRaii.PushId(id);
        var enabled = settings.Enabled;
        var changed = ImGui.Checkbox(I18n.Get("potions.include"), ref enabled);
        settings.Enabled = enabled;
        if (!enabled) return changed;

        MirageUi.Text(I18n.Get("potions.help"), MirageUi.Color.Secondary);
        ImGui.SetNextItemWidth(-1);
        var selected = settings.ItemId == 0 ? I18n.Get("potions.auto") : ReplayPotionCatalog.ItemName(settings.ItemId);
        if (ImGui.BeginCombo("##potionItem", selected))
        {
            if (ImGui.Selectable(I18n.Get("potions.auto"), settings.ItemId == 0))
            { settings.ItemId = 0; changed = true; }
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##potionSearch", I18n.Get("potions.search"), ref _search, 100);
            ImGui.Checkbox(I18n.Get("potions.owned_only"), ref _ownedOnly);
            ImGui.Separator();
            using (ImRaii.Child("##potionList", new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 9), false))
            {
                var stat = ReplayPotionCatalog.PrimaryStat(jobId);
                foreach (var entry in ReplayPotionCatalog.Items.Where(i => i.PrimaryStat == stat
                    && (string.IsNullOrWhiteSpace(_search) || i.Name.Contains(_search, StringComparison.OrdinalIgnoreCase))))
                {
                    var nq = ReplayPotionCatalog.Count(entry.Id, false);
                    var hq = ReplayPotionCatalog.Count(entry.Id, true);
                    if (_ownedOnly && nq + hq == 0) continue;
                    var label = $"{entry.Name} (HQ {hq} / NQ {nq})##{entry.Id}";
                    if (ImGui.Selectable(label, settings.ItemId == entry.Id))
                    { settings.ItemId = entry.Id; changed = true; ImGui.CloseCurrentPopup(); }
                }
            }
            ImGui.EndCombo();
        }
        ImGui.SetNextItemWidth(-1);
        var quality = new[] { I18n.Get("potions.prefer_hq"), I18n.Get("potions.hq_only"), I18n.Get("potions.nq_only") };
        var qualityIndex = Math.Clamp((int)settings.Quality, 0, 2);
        if (ImGui.Combo("##potionQuality", ref qualityIndex, quality, quality.Length))
        { settings.Quality = (ReplayPotionQuality)qualityIndex; changed = true; }
        var choice = ReplayPotionCatalog.Select(settings, jobId);
        MirageUi.Text(choice is { } item
            ? I18n.Format("potions.selected", ReplayPotionCatalog.ItemName(item.ItemId) + (item.Hq ? " HQ" : string.Empty))
            : I18n.Get("potions.none_owned"), MirageUi.Color.Secondary);
        return changed;
    }

    public static void DrawCue(TimelineCue cue, ReplayPotionSettings settings)
    {
        MirageUi.GameIcon(ReplayPotionCatalog.ItemIcon(cue.ReplayItemId), 24, 24, framed: true);
        ImGui.SameLine();
        ImGui.TextUnformatted(ReplayPotionCatalog.ItemName(cue.ReplayItemId) + (cue.ReplayItemHq ? " HQ" : string.Empty));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(I18n.Get(settings.Enabled ? "potions.cue_help" : "potions.cue_disabled"));
    }
}
