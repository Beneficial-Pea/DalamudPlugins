namespace Oracle.Services;

// Keep recent scheduler facts across a manual recovery or combat end, so a
// report does not require entering a command during the stalled moment.
internal sealed class ReplayStallHistory
{
    private readonly Queue<string> _recent = new();
    private string? _key;
    private long _started;
    private long _sampled = long.MinValue;
    private bool _reported;
    public string LastSummary { get; private set; } = string.Empty;
    public bool SampleDue(long now) => _sampled == long.MinValue || now - _sampled >= 250;
    public long WaitAge(long now) => _key == null ? 0 : Math.Max(0, now - _started);
    public void RestoreSummary(string summary) => LastSummary = Trim(summary, 700);

    public string? Observe(string key, long now, bool watching, string snapshot)
    {
        if (!watching) { ResetActive(); return null; }
        if (_key != key)
        {
            _key = key; _started = now; _reported = false; _recent.Clear();
        }
        if (!SampleDue(now)) return null;
        _sampled = now;
        snapshot = Trim(snapshot, 1200);
        _recent.Enqueue($"+{WaitAge(now)}ms {snapshot}");
        while (_recent.Count > 24) _recent.Dequeue();
        return !_reported && WaitAge(now) >= 4000 ? Capture("no progress", now, snapshot) : null;
    }

    public string Capture(string reason, long now, string snapshot)
    {
        _reported = true;
        LastSummary = Trim(reason + ": " + snapshot, 700);
        return $"tick={now} {LastSummary}\n" + string.Join("\n", _recent) + "\n";
    }

    public void ResetActive()
    {
        _key = null; _reported = false; _recent.Clear(); _sampled = long.MinValue;
    }

    private static string Trim(string value, int max) => value.Length <= max ? value : value[..max];
}
