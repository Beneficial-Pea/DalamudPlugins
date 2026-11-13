namespace Oracle.Services;

internal readonly record struct ReplayCooldownSample(int Group, bool Active, float Total, float Elapsed,
    uint CurrentCharges = 0, uint MaxCharges = 1, float OneChargeSeconds = 0);

internal static class ReplayCooldownPolicy
{
    // Sheet group 58 is the shared GCD. RSR uses the additional group when the
    // primary group is 58, so charged GCDs and actions like Drill retain their own CD.
    public static int PersonalGroup(ReplayActionTraits action)
    {
        var group = action.CooldownGroup == 58 ? action.AdditionalCooldownGroup : action.CooldownGroup;
        return group > 0 && group != 58 ? group : 0;
    }

    public static float Remaining(ReplayCooldownSample sample)
    {
        if (sample.Group is <= 0 or 58 || !sample.Active || sample.CurrentCharges > 0
            || !float.IsFinite(sample.Total) || !float.IsFinite(sample.Elapsed)
            || sample.Total <= 0 || sample.Elapsed < 0) return 0;
        if (sample.MaxCharges <= 1) return Math.Max(0, sample.Total - sample.Elapsed);
        if (!float.IsFinite(sample.OneChargeSeconds) || sample.OneChargeSeconds <= 0) return 0;
        // Native recast elapsed is charge credit, not time since the last key press.
        return Math.Max(0, Math.Min(sample.Total, sample.OneChargeSeconds) - sample.Elapsed);
    }

    public static bool ShouldSkip(float remaining, bool nativeGcdQueue) => float.IsFinite(remaining)
        && remaining > (nativeGcdQueue ? 0.4f : 0.001f);
}
