namespace Oracle.Services;

// Only schedules visual presses; it never executes a hotbar slot or an action.
internal sealed class ReplayClickSequence<T>
{
    private sealed class Burst(T[] slots, long now)
    {
        public readonly T[] Slots = slots;
        public readonly long Started = now;
        public long Next = now;
        public int Remaining = 2;
    }

    private readonly List<Burst> _bursts = [];
    public void Clear() => _bursts.Clear();

    public void Start(IReadOnlyList<T>? slots, long now)
    {
        if (slots is not { Count: > 0 }) return;
        _bursts.Add(new Burst(slots.ToArray(), now));
    }

    public IReadOnlyList<T> TakeDue(long now)
    {
        var due = new List<T>();
        for (var i = 0; i < _bursts.Count;)
        {
            var burst = _bursts[i];
            // Do not replay stale presses in a burst after a stalled frame.
            if (now < burst.Started || now - burst.Started > 400)
            {
                _bursts.RemoveAt(i);
                continue;
            }
            if (now < burst.Next) { i++; continue; }
            due.AddRange(burst.Slots);
            burst.Next = now + 150;
            if (--burst.Remaining == 0) _bursts.RemoveAt(i);
            else i++;
        }
        return due;
    }
}
