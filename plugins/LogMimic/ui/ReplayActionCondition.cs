namespace Oracle.Services;

// Native status 572 means a gauge, proc or combo requirement is missing. Keep
// a short grace period for an already-used preparation to take effect. Time is
// monotonic: pausing the attack timeline must not pause this recovery window.
internal sealed class ReplayActionCondition
{
    private readonly record struct Wait(uint ActionId, ulong TargetId, long Started, long Seen);
    private readonly Dictionary<string, Wait> _waiting = [];
    public const uint RequirementNotMet = 572;
    public const int GraceMs = 600;

    public bool ShouldSkip(string cueId, uint actionId, ulong targetId, uint status, long now)
    {
        if (status != RequirementNotMet)
        {
            _waiting.Remove(cueId);
            return false;
        }
        if (!_waiting.TryGetValue(cueId, out var wait) || wait.ActionId != actionId
            || wait.TargetId != targetId || now < wait.Seen || now - wait.Seen > 1000)
        {
            _waiting[cueId] = new(actionId, targetId, now, now);
            return false;
        }
        _waiting[cueId] = wait with { Seen = now };
        if (now - wait.Started < GraceMs) return false;
        _waiting.Remove(cueId);
        return true;
    }

    public void Forget(string cueId) => _waiting.Remove(cueId);
    public void Reset() => _waiting.Clear();
}
