namespace Oracle.Services;

// One decision per genuine countdown. Manual pause remains effective until a new countdown.
internal sealed class ReplayStartGate
{
    private long _handled;
    public bool Observe(long sequence, bool countdownSession, bool ready, bool enabled)
    {
        if (!countdownSession || sequence <= 0 || sequence == _handled) return false;
        _handled = sequence;
        return ready && enabled;
    }
    public void Pause(long sequence) => _handled = sequence;
}

internal static class ReplayPreparation
{
    public static bool IsMissed(float actionTime, float elapsed) => actionTime < 0 && actionTime < elapsed - 0.75f;
    public static double ImportStart(double fightStart, double previousFightEnd) =>
        Math.Max(0, Math.Max(fightStart - 30_000, previousFightEnd));
}
