using System.IO.Compression;
using System.Text.Json;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;

namespace Oracle.Services;

internal static class TargetNameLookup
{
    private static readonly Lazy<NpcNameIndex> Index = new(Load);
    private static readonly Dictionary<string, string> DisplayCache = new(StringComparer.OrdinalIgnoreCase);

    private static NpcNameIndex Load()
    {
        try
        {
            var directory = PluginServices.PluginInterface.AssemblyLocation.DirectoryName ?? AppContext.BaseDirectory;
            using var file = File.OpenRead(Path.Combine(directory, "Data", "BNpcNames.en.json.gz"));
            using var zip = new GZipStream(file, CompressionMode.Decompress);
            return new(JsonSerializer.Deserialize<Dictionary<uint, string>>(zip) ?? new());
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "Failed to load English NPC name index; explicit target binding remains available.");
            return new(new Dictionary<uint, string>());
        }
    }

    public static bool Matches(string loggedName, IGameObject obj)
    {
        if (obj is not IBattleNpc npc) return false;
        var ids = Index.Value.GetIds(loggedName);
        return ids.Count > 0 ? ids.Contains(npc.NameId)
            : string.Equals(loggedName.Trim(), obj.Name.TextValue, StringComparison.OrdinalIgnoreCase);
    }

    public static string DisplayName(string loggedName)
    {
        if (string.IsNullOrWhiteSpace(loggedName)) return loggedName;
        if (DisplayCache.TryGetValue(loggedName, out var cached)) return cached;
        var localNames = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var sheet = PluginServices.DataManager.GetExcelSheet<BNpcName>();
            foreach (var id in Index.Value.GetIds(loggedName))
            {
                var row = sheet?.GetRowOrDefault(id);
                if (row is { } value)
                {
                    var name = value.Singular.ExtractText();
                    if (!string.IsNullOrWhiteSpace(name)) localNames.Add(name);
                }
            }
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "NPC localized name lookup failed.");
        }
        var result = localNames.Count == 1 ? localNames.First() : loggedName;
        DisplayCache[loggedName] = result;
        return result;
    }
}
