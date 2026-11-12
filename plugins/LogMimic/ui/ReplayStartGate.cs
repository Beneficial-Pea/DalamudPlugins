namespace Oracle.Services;

// One automatic start per genuine countdown, once the timeline reaches zero.
internal sealed class ReplayStartGate
{
    private long _handled;
    public bool Observe(long sequence, bool countdownSession, bool ready, bool enabled, float elapsed)
    {
        if (!countdownSession || sequence <= 0 || sequence == _handled) return false;
        if (!ready || !enabled)
        {
            _handled = sequence;
            return false;
        }
        if (!CanExecuteAt(elapsed)) return false;
        _handled = sequence;
        return true;
    }
    public bool IsHandled(long sequence) => sequence > 0 && _handled == sequence;
    public void Pause(long sequence) => _handled = sequence;
    public static bool CanExecuteAt(float elapsed) => float.IsFinite(elapsed) && elapsed >= 0;
    public static bool IsCombatAction(float time) => float.IsFinite(time) && time >= 0;
}
