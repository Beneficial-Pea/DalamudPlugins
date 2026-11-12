namespace Oracle.Services;

// English labels map to stable BNpcName rows; runtime entity IDs are never reused across pulls.
internal sealed class NpcNameIndex
{
    private readonly Dictionary<string, HashSet<uint>> _ids = new(StringComparer.OrdinalIgnoreCase);
    public NpcNameIndex(IReadOnlyDictionary<uint, string> rows)
    {
        foreach (var (id, name) in rows)
        {
            if (id == 0 || string.IsNullOrWhiteSpace(name)) continue;
            var key = name.Trim();
            if (!_ids.TryGetValue(key, out var ids)) _ids[key] = ids = [];
            ids.Add(id);
        }
    }
    public IReadOnlyCollection<uint> GetIds(string name) =>
        _ids.TryGetValue(name.Trim(), out var ids) ? ids : Array.Empty<uint>();
    public bool Matches(string name, uint nameId) => nameId != 0 && GetIds(name).Contains(nameId);
}
