namespace Oracle.Services;

internal static class ReplayGcdReadiness
{
    public static float Remaining(float total, float elapsed) =>
        float.IsFinite(total) && float.IsFinite(elapsed) && total >= 0 && elapsed >= 0
            ? Math.Max(0, total - elapsed) : float.NaN;

    // A full native status check (including recast and casting) can prove that
    // the next GCD is usable even when a cached timer still reports a delay.
    // Change only our scheduling snapshot; never write the game's timers,
    // discard a record or treat this proof as an action confirmation.
    public static ReplayNativeWindow Reconcile(ReplayNativeWindow window, uint? fullNativeStatus)
    {
        if (fullNativeStatus != 0 || !float.IsFinite(window.CastRemaining)
            || !float.IsFinite(window.AnimationLock) || window.CastRemaining > 0.001f
            || window.AnimationLock > 0.001f || window.CastRemaining < 0 || window.AnimationLock < 0)
            return window;
        return window.GcdRemaining > 0.4f || !float.IsFinite(window.GcdRemaining)
            ? window with { GcdRemaining = 0 } : window;
    }
}
