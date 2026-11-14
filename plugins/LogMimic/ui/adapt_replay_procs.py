"""Replace only the requested DNC/BRD proc pairs at execution time, never saved cues."""
import json
import shutil


def apply(root, assets):
    def rep(path, old, new):
        p = root / path
        s = p.read_text(encoding='utf-8')
        if s.count(old) != 1:
            raise RuntimeError(f'Proc pair anchor {path}: {old[:100]} ({s.count(old)})')
        p.write_text(s.replace(old, new), encoding='utf-8')

    for name in ['ReplayProcPairs.cs', 'LogMimicService.Procs.cs']:
        shutil.copyfile(assets / name, root / 'Oracle/Services' / name)
    svc = 'Oracle/Services/LogMimicService.cs'
    rep(svc, '        _engine.ActionUse.ActionConfirmed += OnActionUsed;',
        '        _engine.ActionUse.ActionConfirmed += OnActionUsed;\n        _engine.ReplayOwnsConfirmation = OwnsReplayConfirmation;')
    rep(svc, '    public void Dispose() => _engine.ActionUse.ActionConfirmed -= OnActionUsed;',
        '''    public void Dispose()
    {
        _engine.ActionUse.ActionConfirmed -= OnActionUsed;
        _engine.ReplayOwnsConfirmation = null;
    }''')
    rep(svc, '        LastCooldownSkip = string.Empty;',
        '        LastCooldownSkip = string.Empty;\n        LastProcSubstitution = string.Empty;')
    rep(svc, '        if (actionId == 0) actionId = cue.ActionId;',
        '        if (actionId == 0) actionId = cue.ActionId;\n        var recordedAdjusted = actionId;\n        actionId = ResolveProcAction(manager, cue, actionId);')
    rep(svc, '        if (sent)\n        {',
        '''        if (sent)
        {
            if (actionId != recordedAdjusted)
                LastProcSubstitution = I18n.Format("proc.substitution", ActionLookup.GetName(recordedAdjusted), ActionLookup.GetName(actionId));''')
    rep(svc, '                    Feedback.Confirmed(pending.Id, used, _engine.ElapsedSeconds, Environment.TickCount64);',
        '                    Feedback.Confirmed(pending.Id, used, _engine.ElapsedSeconds, Environment.TickCount64);\n                    _engine.CompleteConfirmedReplayCue(pending.Id);')
    rep(svc, '                    Feedback.Confirmed(step.CueId, used, _engine.ElapsedSeconds, Environment.TickCount64);',
        '                    Feedback.Confirmed(step.CueId, used, _engine.ElapsedSeconds, Environment.TickCount64);\n                    _engine.CompleteConfirmedReplayCue(step.CueId);')
    rep(svc, '                Feedback.Confirmed(_main[_mainIndex].Id, used, _engine.ElapsedSeconds, Environment.TickCount64);',
        '                Feedback.Confirmed(_main[_mainIndex].Id, used, _engine.ElapsedSeconds, Environment.TickCount64);\n                _engine.CompleteConfirmedReplayCue(_main[_mainIndex].Id);')

    engine = 'Oracle/Services/TimelineEngine.cs'
    rep(engine, '    private void TryCompleteCueForUsedAction(uint usedActionId)',
        '''    public Func<uint, bool>? ReplayOwnsConfirmation { get; set; }

    public void CompleteConfirmedReplayCue(string cueId)
    {
        if (_running && _activeDoc is { ReplayEnabled: true }
            && _activeDoc.Cues.Any(c => c.Id == cueId && c.Kind == TimelineCueKind.Action))
            CompleteCue(cueId);
    }

    private void TryCompleteCueForUsedAction(uint usedActionId)''')
    # One accepted replay request owns one exact cue. Otherwise a substituted
    # action could clear a second upcoming cue with that action's recorded ID.
    rep(engine, '        if (usedActionId == 0)\n            return;\n\n        var elapsed = ElapsedSeconds;',
        '        if (usedActionId == 0 || ReplayOwnsConfirmation?.Invoke(usedActionId) == true)\n            return;\n\n        var elapsed = ElapsedSeconds;')
    rep('Oracle/Services/LogMimicDtrEntry.cs', '        if (!string.IsNullOrEmpty(_replay.LastCooldownSkip))',
        '        if (!string.IsNullOrEmpty(_replay.LastProcSubstitution)) tooltip += "\\n" + _replay.LastProcSubstitution;\n        if (!string.IsNullOrEmpty(_replay.LastCooldownSkip))')

    for lang, message in [('en', 'Proc substitution: {0} → {1}'), ('ko', '프록 대응: {0} → {1}')]:
        p = root / f'Oracle/Data/I18n/{lang}.json'
        m = json.loads(p.read_text(encoding='utf-8'))
        m['proc.substitution'] = message
        p.write_text(json.dumps(m, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Applied five live DNC/BRD proc pairs with exact requested-action confirmations and cue completion.')
