namespace Oracle.Services;

internal sealed record ReplayActionFeedback(string CueId, uint ActionId, string Target, float PlannedAt,
    float RequestedAt, long RequestTick, float? ConfirmedAt = null, long ConfirmTick = 0);

internal sealed class ReplayFeedbackLog
{
    private readonly Dictionary<string, ReplayActionFeedback> _pending = new();
    private readonly List<ReplayActionFeedback> _history = [];
    public ReplayActionFeedback? LastRequest { get; private set; }
    public ReplayActionFeedback? LastConfirmed { get; private set; }
    public IReadOnlyList<ReplayActionFeedback> History => _history;
    public uint LastGameStatus { get; set; }
    public bool MatchesRequest(string cueId, uint actionId) =>
        _pending.TryGetValue(cueId, out var request) && request.ActionId == actionId;
    public void Reset()
    {
        _pending.Clear(); _history.Clear(); LastRequest = null; LastConfirmed = null; LastGameStatus = 0;
    }
    public void Requested(string cueId, uint actionId, string target, float planned, float requested, long tick)
    {
        LastRequest = new(cueId, actionId, target, planned, requested, tick);
        _pending[cueId] = LastRequest;
        LastGameStatus = 0;
    }
    public void Confirmed(string cueId, uint actionId, float confirmed, long tick)
    {
        if (!_pending.Remove(cueId, out var request)) return;
        var item = request with { ActionId = actionId, ConfirmedAt = confirmed, ConfirmTick = tick };
        LastConfirmed = item;
        _history.Insert(0, item);
        if (_history.Count > 5) _history.RemoveAt(_history.Count - 1);
    }
}
