using System.Text.RegularExpressions;

namespace Oracle.Services;

internal readonly record struct CountdownSignal(bool Started, bool Cancelled, float Remaining);

internal sealed class CountdownSignals
{
    private static readonly Regex[] StartPatterns =
    [
        new(@"(?:Battle commencing|Commencing) in\s+(\d+)\s+seconds?", RegexOptions.IgnoreCase),
        new(@"戦闘開始まで\s*(?:あと\s*)?(\d+)\s*秒"),
        new(@"전투\s*(?:시작|개시)(?:까지)?\s*(?:앞으로\s*)?(\d+)\s*초"),
    ];
    private static readonly Regex CancelPattern = new(
        @"(?:countdown.*(?:canceled|cancelled))|(?:초읽기.*취소)|(?:전투.*취소)|(?:戦闘開始.*中止)|(?:カウントダウン.*(?:中止|キャンセル))", RegexOptions.IgnoreCase);
    private long? _endAt;
    private bool _pendingStart;
    private bool _pendingCancel;

    public void Receive(string text, long now)
    {
        if (CancelPattern.IsMatch(text))
        {
            _endAt = null;
            _pendingStart = false;
            _pendingCancel = true;
            return;
        }
        foreach (var pattern in StartPatterns)
        {
            var match = pattern.Match(text);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var seconds) || seconds is <= 0 or > 60) continue;
            var end = now + seconds * 1000L;
            // The 5/4/3/2/1 system messages belong to the same countdown.
            if (_endAt is { } existing && Math.Abs(existing - end) < 1800) return;
            _endAt = end;
            _pendingStart = true;
            _pendingCancel = false;
            return;
        }
    }

    public CountdownSignal Drain(long now)
    {
        var cancelled = _pendingCancel;
        var started = _pendingStart && _endAt is { } end && now <= end + 1500;
        var remaining = _endAt is { } until ? Math.Max(0, (until - now) / 1000f) : 0;
        _pendingStart = false;
        _pendingCancel = false;
        return new(started, cancelled, remaining);
    }
}
