"""Skip unavailable personal cooldown cues without bypassing the game's action checks."""
import json
import shutil


def apply(root, assets):
    def rep(path, old, new):
        p = root / path
        s = p.read_text(encoding='utf-8')
        if s.count(old) != 1:
            raise RuntimeError(f'Cooldown skip anchor {path}: {old[:100]} ({s.count(old)})')
        p.write_text(s.replace(old, new), encoding='utf-8')

    for name in ['ReplayCooldownPolicy.cs', 'ReplayNativeCooldown.cs']:
        shutil.copyfile(assets / name, root / 'Oracle/Services' / name)
    svc = 'Oracle/Services/LogMimicService.cs'
    rep(svc, '    public bool Suspended => _suspended;',
        '    public bool Suspended => _suspended;\n    public string LastCooldownSkip { get; private set; } = string.Empty;')
    rep(svc, '        LastPotionResult = string.Empty;',
        '        LastPotionResult = string.Empty;\n        LastCooldownSkip = string.Empty;')
    rep(svc, '    private bool Attempt(TimelineCue cue, long now, bool independent)\n    {',
        '    private bool Attempt(TimelineCue cue, long now, bool independent, out bool skippedCooldown)\n    {\n        skippedCooldown = false;')
    rep(svc, '        var gameStatus = manager->GetActionStatus(ActionType.Action, actionId, targetId, !queueGcd, !queueGcd);',
        '''        var remainingCooldown = ReplayNativeCooldown.Remaining(manager, actionId);
        if (ReplayCooldownPolicy.ShouldSkip(remainingCooldown, queueGcd))
        {
            skippedCooldown = true;
            LastCooldownSkip = I18n.Format("cooldown.skipped", ActionLookup.GetName(actionId), remainingCooldown);
            _state = LastCooldownSkip;
            return false;
        }
        var gameStatus = manager->GetActionStatus(ActionType.Action, actionId, targetId, !queueGcd, !queueGcd);''')
    rep(svc, '            if (Attempt(cue, now, independent: true)) return;',
        '            if (Attempt(cue, now, independent: true, out var skippedCooldown)) return;\n            if (skippedCooldown) _independentDone.Add(cue.Id);')
    rep(svc, '''        if (_mainIndex >= _main.Count)
        {
            _state = I18n.Get("replay.finished");
            return;
        }
        var next = _main[_mainIndex];
        if (elapsed < Scheduled(next))
        {
            _state = I18n.Format("replay.waiting", Scheduled(next) - elapsed, ActionLookup.GetName(next.ActionId));
            return;
        }
        if (_pendingMainId == next.Id)
        {
            _state = I18n.Format("replay.confirm", ActionLookup.GetName(next.ActionId));
            return;
        }
        Attempt(next, now, independent: false);''', '''        while (_mainIndex < _main.Count)
        {
            var next = _main[_mainIndex];
            if (elapsed < Scheduled(next))
            {
                _state = I18n.Format("replay.waiting", Scheduled(next) - elapsed, ActionLookup.GetName(next.ActionId));
                return;
            }
            if (_pendingMainId == next.Id)
            {
                _state = I18n.Format("replay.confirm", ActionLookup.GetName(next.ActionId));
                return;
            }
            Attempt(next, now, independent: false, out var skippedCooldown);
            if (!skippedCooldown) return;
            _mainIndex++;
        }
        _state = I18n.Get("replay.finished");''')

    adaptive = 'Oracle/Services/LogMimicService.Adaptive.cs'
    rep(adaptive, '            if (canTry && Attempt(cue, now, true)) return;',
        '''            if (canTry)
            {
                if (Attempt(cue, now, true, out var skippedCooldown)) return;
                if (skippedCooldown) _independentDone.Add(cue.Id);
            }''')
    rep(adaptive, '            var sent = Attempt(cue, now, false);',
        '            var sent = Attempt(cue, now, false, out var skippedCooldown);\n            if (skippedCooldown) return new(false, SkipCooldown: true);')
    rep(adaptive, '                if (!ReplayNativeTiming.IsGcd(cue.ActionId) && Attempt(cue, now, true)) return;',
        '''                if (!ReplayNativeTiming.IsGcd(cue.ActionId))
                {
                    if (Attempt(cue, now, true, out var skippedCooldown)) return;
                    if (skippedCooldown) _independentDone.Add(cue.Id);
                }''')
    rep('Oracle/Services/LogMimicDtrEntry.cs',
        '        if (!string.IsNullOrEmpty(_replay.LastPotionResult))',
        '        if (!string.IsNullOrEmpty(_replay.LastCooldownSkip)) tooltip += "\\n" + _replay.LastCooldownSkip;\n        if (!string.IsNullOrEmpty(_replay.LastPotionResult))')
    for lang, message in [
        ('en', 'Skipped cooldown: {0} ({1:0.00}s remaining when skipped).'),
        ('ko', '쿨타임으로 건너뜀: {0} (건너뛸 때 남은 쿨 {1:0.00}초).'),
    ]:
        p = root / f'Oracle/Data/I18n/{lang}.json'
        m = json.loads(p.read_text(encoding='utf-8'))
        m['cooldown.skipped'] = message
        p.write_text(json.dumps(m, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Applied personal cooldown skips; shared GCD, charges, target checks and genuine confirmations preserved.')
