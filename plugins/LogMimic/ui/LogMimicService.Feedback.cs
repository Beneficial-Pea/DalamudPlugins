using Oracle.Models;
namespace Oracle.Services;

internal sealed unsafe partial class LogMimicService
{
    private float _playbackOffset;
    public HotbarHighlightService? HotbarFeedback { get; set; }
    public ReplayFeedbackLog Feedback { get; } = new();
    public bool HasAnyReplayPreset => _store.Documents.Any(d => d.ReplayEnabled);
    public bool ReplayClockRunning => _engine.IsRunning && !_engine.IsPreview;
    public string LoadedPresetName => _engine.ActiveDocument?.Name ?? I18n.Get("lm.none");
    public float ClockSeconds => _engine.ElapsedSeconds;
    private float PlaybackElapsed => _engine.ElapsedSeconds + _playbackOffset;
    public TimelineCue? NextMainCue => _mainIndex < _main.Count ? _main[_mainIndex] : PreviewCue();
    public float NextPlannedAt => NextMainCue is { } cue ? Scheduled(cue) - (_doc != null ? _playbackOffset : PreviewOffset()) : 0;
    private static float Scheduled(TimelineCue cue) => ReplayPacing.Scheduled(cue.TimeOffsetSec, cue.ReplayStartOffsetSec);
    private TimelineCue? PreviewCue()
    {
        if (_doc != null) return null;
        var selected = _engine.ActiveDocument ?? _store.ActiveDocument;
        return selected?.Cues.Where(c => c.Kind == TimelineCueKind.Action && c.ActionId != 0 && c.TimeOffsetSec >= 0
            && !selected.ReplayExcludedActionIds.Contains(c.ActionId)).MinBy(Scheduled);
    }
    private float PreviewOffset() => C.ReplayAlignFirstActionToZero && PreviewCue() is { } first ? Scheduled(first) : 0;
    private void PrepareFeedback()
    {
        _playbackOffset = ReplayPacing.AlignmentOffset(C.ReplayAlignFirstActionToZero,
            _main.Concat(_independent).Select(Scheduled));
        Feedback.Reset();
        HotbarFeedback?.ClearReplayClicks();
        _lastAttempt = 0;
    }
    private string IdleReplayState()
    {
        if (_engine.IsPreview) return I18n.Get("feedback.preview");
        if (_engine.ActiveDocument is not { ReplayEnabled: true } doc) return I18n.Get("feedback.no_match");
        if (!_engine.MatchesLiveJob(doc)) return I18n.Format("replay.job", doc.ClassJobId,
            PluginServices.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0);
        return I18n.Get("feedback.ready");
    }
}
