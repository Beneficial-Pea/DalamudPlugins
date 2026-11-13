"""Only change replay target selection; keep pacing, confirmations and potion behavior intact."""
import json
import shutil


def apply(root, assets):
    def rep(path, old, new):
        p = root / path
        s = p.read_text(encoding='utf-8')
        if s.count(old) != 1:
            raise RuntimeError(f'Manual target anchor {path}: {old[:100]} ({s.count(old)})')
        p.write_text(s.replace(old, new), encoding='utf-8')

    for name in ['ReplayManualTarget.cs', 'LogMimicService.ManualTarget.cs']:
        shutil.copyfile(assets / name, root / 'Oracle/Services' / name)
    svc = 'Oracle/Services/LogMimicService.cs'
    rep(svc, '    private readonly Dictionary<int, ulong> _boundTargets = new();\n', '')
    rep(svc, '        _boundTargets.Clear();\n', '')
    rep(svc, '''        var selfTarget = ReplayNativeTiming.UsesSelf(actionId, cue.ReplayTargetLogId);
        string? reason = null;
        var target = selfTarget ? PluginServices.ObjectTable.LocalPlayer : ResolveTarget(cue, out reason);''',
        '        var target = ResolveTarget(actionId, out var reason);')
    rep(svc, '            _state = I18n.Format("replay.target", cue.ReplayTargetLogId, cue.ReplayTargetName, reason);',
        '            _state = reason;')
    rep(svc, '        var targetId = target?.GameObjectId ?? PluginServices.TargetManager.Target?.GameObjectId ?? 0xE0000000UL;',
        '        var targetId = target!.GameObjectId;')
    rep(svc, '''        if (target != null && !selfTarget && cue.ReplayTargetKind == "enemy")
            PluginServices.TargetManager.Target = target;
''', '')
    rep(svc, '''            case "bind":
                if (args.Length < 2 || !int.TryParse(args[1], out var logId)) break;
                var selected = PluginServices.TargetManager.Target;
                if (selected == null) { Message(I18n.Get("replay.select_target")); break; }
                _boundTargets[logId] = selected.GameObjectId;
                Message(I18n.Format("replay.bound", logId, selected.Name.TextValue));
                break;''', '''            case "bind":
                Message(I18n.Get("manual_target.help"));
                break;''')
    p = root / svc
    s = p.read_text(encoding='utf-8')
    start = s.index('    private IGameObject? ResolveTarget(TimelineCue cue, out string? reason)')
    end = s.index('    public void Command(string[] args)', start)
    s = s[:start] + s[end:]
    s = s.replace('// Does not control movement; a missing/ambiguous target blocks rather than guessing.',
                  '// Uses the player-selected target for new requests and never changes the hard target.')
    p.write_text(s, encoding='utf-8')

    adaptive = 'Oracle/Services/LogMimicService.Adaptive.cs'
    rep(adaptive, '''            if (!ReplayNativeTiming.UsesSelf(next.ActionId, next.ReplayTargetLogId))
            {
                var candidate = ResolveTarget(next, out var targetReason);
                targetAvailable = targetReason == null;
                if (targetAvailable && ReplayNativeTiming.Traits(next.ActionId).Hostile && candidate == null)
                    targetAvailable = PluginServices.TargetManager.Target is { IsDead: false, IsTargetable: true };
                if (!targetAvailable) reason = I18n.Format("replay.target", next.ReplayTargetLogId, next.ReplayTargetName,
                    targetReason ?? I18n.Get("replay.not_spawned"));
            }''', '''            var candidate = ResolveTarget(next.ActionId, out var targetReason);
            targetAvailable = candidate != null && targetReason == null;
            if (!targetAvailable) reason = targetReason ?? I18n.Get("manual_target.wait");''')

    ui = 'Oracle/UI/ConfigWindow.LogMimic.cs'
    rep(ui, '    private int _targetActorId;\n', '')
    rep(ui, '''        var follow = C.ReplayUseSelectedForAmbiguousTargets;
        if (ImGui.Checkbox(L("follow_target"), ref follow))
        {
            C.ReplayUseSelectedForAmbiguousTargets = follow;
            C.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(L("follow_target_help"));
        ImGui.TextWrapped(L("follow_target_summary"));''',
        '        ImGui.TextWrapped(I18n.Get("manual_target.help"));')
    rep(ui, '''        ImGui.InputInt(L("actor_id"), ref _targetActorId);
        using (ImRaii.Disabled(Replay == null || !Replay.HasReplayDocument || _targetActorId <= 0 || PluginServices.TargetManager.Target == null))
        {
            if (ImGui.Button(L("bind"))) Replay?.Command(["bind", _targetActorId.ToString()]);
        }
        ImGui.SameLine();
''', '''        ImGui.TextWrapped(I18n.Format("manual_target.selected",
            PluginServices.TargetManager.Target?.Name.TextValue ?? L("none")));
''')

    texts = {
        'manual_target.help': ('Choose targets in game. Targeted actions use your current selection; self-only actions still use you. Without a target, attacks wait. Already queued actions can retain their original target.', '대상은 게임에서 직접 선택하세요. 대상 지정 스킬은 현재 선택한 대상에게, 자신 전용 스킬은 자신에게 사용합니다. 대상이 없으면 공격을 기다립니다. 이미 예약된 스킬은 이전 대상으로 나갈 수 있습니다.'),
        'manual_target.wait': ('Select a living, targetable target in game to continue.', '게임에서 대상을 선택하면 공격을 이어갑니다. 사망했거나 선택할 수 없는 대상은 사용할 수 없습니다.'),
        'manual_target.selected': ('Current selection: {0}', '현재 선택한 대상: {0}'),
        'lm.targets': ('Target info', '대상 정보'),
        'lm.target_help': ('The list below shows original FFLogs targets for reference. Replay uses your current in-game selection.', '아래 목록은 로그에 기록된 대상 정보입니다. 재생 시에는 게임에서 직접 선택한 대상을 사용합니다.'),
        'lm.recorded_target_tooltip': ('Original FFLogs target: {0}\nActor ID: {1}\nReference only; replay uses your current in-game selection.', '로그에 기록된 대상: {0}\nFFLogs 대상 ID: {1}\n참고용 정보이며, 재생 시에는 직접 선택한 대상을 사용합니다.'),
        'replay.commands': ('/lm replay: list, load <name>, on, off, status, skip, targets, exclude <action ID>, independent <action ID>', '/lm replay: list, load <이름>, on, off, status, skip, targets, exclude <스킬 ID>, independent <스킬 ID>'),
    }
    for lang, index in [('en', 0), ('ko', 1)]:
        p = root / f'Oracle/Data/I18n/{lang}.json'
        m = json.loads(p.read_text(encoding='utf-8'))
        m.update({key: value[index] for key, value in texts.items()})
        p.write_text(json.dumps(m, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    p = root / 'Oracle/LogMimic.json'
    manifest = json.loads(p.read_text(encoding='utf-8'))
    manifest['Description'] = manifest['Description'].replace('target matching', 'manual target selection')
    p.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    # Guard every replay lane against accidental reintroduction of target writes or log resolution.
    for p in (root / 'Oracle/Services').glob('LogMimicService*.cs'):
        source = p.read_text(encoding='utf-8')
        for forbidden in ['TargetManager.Target =', '_boundTargets', 'ReplayTargetChoice.Choose(', 'TargetNameLookup.Matches(']:
            if forbidden in source:
                raise RuntimeError(f'Automatic target control remains in {p}: {forbidden}')
    print('Applied manual target selection without changing replay pacing or potion handling.')
