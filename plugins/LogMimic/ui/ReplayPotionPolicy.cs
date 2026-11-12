using Oracle.Models;

namespace Oracle.Services;

internal readonly record struct ReplayPotionCandidate(uint ItemId, int PrimaryStat, int ItemLevel, int NormalCount, int HqCount);
internal readonly record struct ReplayPotionChoice(uint ItemId, bool Hq)
{
    public uint NativeId => ItemId + (Hq ? 1_000_000u : 0);
}

internal static class ReplayPotionPolicy
{
    public static bool IsSkill(uint id) => id is > 0 and < 0x01000000;

    public static bool TryDecodeItem(uint loggedId, out ReplayPotionChoice item)
    {
        item = default;
        if (loggedId >> 24 != 2) return false;
        var nativeId = loggedId & 0x00ffffff;
        var hq = nativeId >= 1_000_000;
        var baseId = nativeId - (hq ? 1_000_000u : 0);
        if (baseId is 0 or >= 1_000_000) return false;
        item = new(baseId, hq);
        return true;
    }

    public static ReplayPotionChoice? Select(ReplayPotionSettings settings, int primaryStat,
        IEnumerable<ReplayPotionCandidate> inventory)
    {
        if (!settings.Enabled || primaryStat is < 1 or > 5) return null;
        var candidates = inventory.Where(i => i.PrimaryStat == primaryStat
            && (settings.ItemId == 0 || i.ItemId == settings.ItemId))
            .OrderByDescending(i => i.ItemLevel).ThenByDescending(i => i.ItemId);
        foreach (var item in candidates)
        {
            if (settings.Quality != ReplayPotionQuality.NormalOnly && item.HqCount > 0) return new(item.ItemId, true);
            if (settings.Quality != ReplayPotionQuality.HqOnly && item.NormalCount > 0) return new(item.ItemId, false);
        }
        return null;
    }

    public static void Normalize(TimelineDocument document)
    {
        document.ReplayPotions ??= new();
        foreach (var cue in document.Cues)
        {
            if (cue.Kind != TimelineCueKind.Action || !TryDecodeItem(cue.ActionId, out var item)) continue;
            cue.Kind = TimelineCueKind.Item;
            cue.ReplayItemId = item.ItemId;
            cue.ReplayItemHq = item.Hq;
            cue.ReplayStartOffsetSec = null;
            cue.ReplayTargetKind = "self";
            cue.ReplayTargetLogId = 0;
            cue.ReplayTargetName = string.Empty;
            cue.TargetKind = CueTargetKind.None;
            cue.TargetJobId = 0;
            cue.TargetRole = CueTargetRole.None;
        }
    }

    public static bool Include(TimelineCue cue, bool allSkills, ISet<uint> allowed, ReplayPotionSettings settings) =>
        cue.Kind == TimelineCueKind.Item ? settings.Enabled
        : cue.Kind == TimelineCueKind.Action && IsSkill(cue.ActionId) && (allSkills || allowed.Contains(cue.ActionId));
}

internal enum ReplayPotionAttempt { Retry, Skip, Sent }
internal readonly record struct ReplayPotionMark(string CueId, float Time);

// Items have encounter-time scheduling and no place in either action confirmation queue.
internal sealed class ReplayPotionLane(IEnumerable<ReplayPotionMark> marks)
{
    private readonly ReplayPotionMark[] _marks = marks.Where(m => float.IsFinite(m.Time) && m.Time >= 0)
        .OrderBy(m => m.Time).ToArray();
    private int _index;
    public int CompletedCount => _index;

    public bool Dispatch(float elapsed, Func<ReplayPotionMark, ReplayPotionAttempt> attempt)
    {
        if (!float.IsFinite(elapsed)) return false;
        while (_index < _marks.Length && elapsed >= _marks[_index].Time)
        {
            if (elapsed > _marks[_index].Time + 5f) { _index++; continue; }
            var result = attempt(_marks[_index]);
            if (result == ReplayPotionAttempt.Retry) return false;
            _index++;
            if (result == ReplayPotionAttempt.Sent) return true;
        }
        return false;
    }
}
