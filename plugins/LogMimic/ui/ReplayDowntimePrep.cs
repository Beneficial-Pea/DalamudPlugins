namespace Oracle.Services;

internal readonly record struct ReplayPrepMark(string CueId, float At, bool SafeSelfAbility);

internal static class ReplayDowntimePrep
{
    public static bool IsSafeSelfAbility(ReplayActionTraits traits, uint category, uint attackType) =>
        category == 4 && attackType == 0 && !traits.IsGcd && ReplayManualTarget.IsSelfOnly(traits);

    public static HashSet<string> Find(IEnumerable<ReplayPhaseMark> damageGcds, IEnumerable<ReplayPrepMark> preparations)
    {
        var gcds = damageGcds.Where(m => float.IsFinite(m.At) && m.At >= 0).OrderBy(m => m.At).ToArray();
        var prep = preparations.Where(m => m.SafeSelfAbility && float.IsFinite(m.At) && m.At >= 0).ToArray();
        var result = new HashSet<string>();
        for (var i = 1; i < gcds.Length; i++)
        {
            var before = gcds[i-1]; var after = gcds[i];
            if (after.At - before.At <= Math.Max(5, before.ExpectedDuration + 1)) continue;
            foreach (var mark in prep)
                if (mark.At > before.At && mark.At <= after.At) result.Add(mark.CueId);
        }
        return result;
    }
}
