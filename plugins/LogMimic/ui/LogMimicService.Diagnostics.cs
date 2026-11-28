using FFXIVClientStructs.FFXIV.Client.Game;

namespace Oracle.Services;

internal sealed unsafe partial class LogMimicService
{
    private readonly ReplayStallHistory _stallHistory = new();
    private bool? _lastNativeAccepted;
    private long _lastDiagnosticWrite = long.MinValue;
    private const long DiagnosticLimitBytes = 512 * 1024;

    private string ReplayDiagnosticSnapshot()
    {
        var manager = ActionManager.Instance();
        if (manager == null) return "action manager unavailable";
        var raw = ReplayNativeTiming.Read(manager);
        var live = ReadReplayWindow(manager, out var status);
        var cue = NextMainCue;
        var pending = _adaptive?.PendingGcd ?? _adaptive?.PendingOgcd;
        var selected = PluginServices.TargetManager.Target;
        return FormattableString.Invariant($"clock={_engine.ElapsedSeconds:0.000} cue={cue?.Id} action={cue?.ActionId} progress={_mainIndex}/{_main.Count} rawGcd={raw.GcdRemaining:0.000} liveGcd={live.GcdRemaining:0.000} cast={raw.CastRemaining:0.000} lock={raw.AnimationLock:0.000} queued={raw.ActionQueued}:{raw.QueuedActionType}:{raw.QueuedActionId}:{raw.QueuedTargetId} seq={raw.LastUsedSequence} status={status} selected={selected?.GameObjectId} pending={pending?.Step.CueId}:{pending?.ActualActionId}:{pending?.Deadline} owned={_queuedReplay?.CueId}:{_queuedReplay?.FollowSelection} lastAttempt={_lastAttempt} accepted={_lastNativeAccepted} lastRequest={Feedback.LastRequest?.ActionId}:{Feedback.LastRequest?.RequestTick} lastConfirmed={Feedback.LastConfirmed?.ActionId}:{Feedback.LastConfirmed?.ConfirmTick} error={_lastReplayUpdateError}");
    }

    private void ObserveReplayStall()
    {
        try
        {
            var now = Environment.TickCount64;
            if (!_stallHistory.SampleDue(now)) return;
            var selected = PluginServices.TargetManager.Target;
            var watching = _doc != null && _engine.IsRunning && !_engine.IsPreview && !_suspended
                && _engine.ElapsedSeconds >= 0 && PluginServices.ObjectTable.LocalPlayer is { IsDead: false }
                && selected is { IsDead: false, IsTargetable: true } && NextMainCue != null;
            if (!watching) { _stallHistory.ResetActive(); return; }
            var report = _stallHistory.Observe(NextMainCue!.Id, now, true, ReplayDiagnosticSnapshot());
            if (report != null) WriteReplayDiagnostic(report);
        }
        catch { /* Diagnostics must never block gameplay or the scheduler. */ }
    }

    private void SaveReplayDiagnostic(string reason)
    {
        try
        {
            WriteReplayDiagnostic(_stallHistory.Capture(reason, Environment.TickCount64, ReplayDiagnosticSnapshot()));
        }
        catch { /* A diagnostic failure cannot stop action use. */ }
    }

    private void RecordLateReplayConfirmation(uint actionId)
    {
        var now = Environment.TickCount64;
        if (_stallHistory.WaitAge(now) >= 2000 && (_lastNativeAccepted == false || now - _lastAttempt >= 1000))
            SaveReplayDiagnostic($"confirmation {actionId} after input wait");
    }

    private void RestoreReplayHistory()
    {
        if (!string.IsNullOrEmpty(_stallHistory.LastSummary)) return;
        try
        {
            var path = Path.Combine(PluginServices.PluginInterface.ConfigDirectory.FullName, "replay-diagnostics.log");
            if (!File.Exists(path) || new FileInfo(path).Length > DiagnosticLimitBytes + 64 * 1024) return;
            var summary = File.ReadLines(path).LastOrDefault(line => line.StartsWith("tick=", StringComparison.Ordinal));
            if (summary != null) _stallHistory.RestoreSummary(summary);
        }
        catch { }
    }

    public void RecordEngineUpdateError(Exception ex)
        => RecordReplayUpdateError(ex, "engine");

    private void RecordReplayUpdateError(Exception ex, string stage = "replay")
    {
        _lastReplayUpdateError = ex.GetType().Name + ": " + ex.Message;
        try { WriteReplayDiagnostic(_stallHistory.Capture(stage + " update error", Environment.TickCount64, ex.ToString())); }
        catch { }
    }

    private void WriteReplayDiagnostic(string report)
    {
        var now = Environment.TickCount64;
        if (_lastDiagnosticWrite != long.MinValue && now - _lastDiagnosticWrite < 1000) return;
        _lastDiagnosticWrite = now;
        try
        {
            var directory = PluginServices.PluginInterface.ConfigDirectory.FullName;
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "replay-diagnostics.log");
            if (File.Exists(path) && new FileInfo(path).Length > DiagnosticLimitBytes)
                File.WriteAllText(path, string.Empty);
            File.AppendAllText(path, $"\n{DateTime.UtcNow:O} LogMimic {LogMimicVersion.Display}\n{report}");
            PluginServices.Log.Warning("LogMimic replay diagnostic saved: {Summary}", _stallHistory.LastSummary);
        }
        catch { /* Logging is best-effort and entirely local. */ }
    }
}
