namespace Oracle.Services;

// Tracks the same countdown agent used by RSR, without depending on chat language/delivery.
internal sealed class NativeCountdownSignals
{
    private bool _active;
    private float _lastRemaining;
    private long _endAt;

    public CountdownSignal Observe(bool active, float remaining, long now)
    {
        if (active && float.IsFinite(remaining) && remaining is > 0 and <= 60)
        {
            var started = !_active || remaining > _lastRemaining + 1f;
            _active = true;
            _lastRemaining = remaining;
            _endAt = now + (long)Math.Ceiling(remaining * 1000);
            return new(started, false, remaining, remaining);
        }
        if (!_active) return default;
        _active = false;
        // A disappearing countdown well before its deadline was cancelled.
        var cancelled = _endAt - now > 200;
        var reachedZero = (float.IsFinite(remaining) && remaining <= 0 && active) || now >= _endAt;
        return new(false, cancelled && !reachedZero, 0, reachedZero ? 0f : null);
    }
}
