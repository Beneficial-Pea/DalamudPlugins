"""Keep optional FFLogs stat-potion events out of both native action queues."""
import json
import shutil


def apply(root, assets):
    def rep(path, old, new):
        p = root / path
        s = p.read_text(encoding='utf-8')
        if s.count(old) != 1:
            raise RuntimeError(f'Potion anchor {path}: {old[:100]} ({s.count(old)})')
        p.write_text(s.replace(old, new), encoding='utf-8')

    shutil.copyfile(assets / 'ReplayPotionSettings.cs', root / 'Oracle/Models/ReplayPotionSettings.cs')
    for name in ['ReplayPotionPolicy.cs', 'ReplayPotionCatalog.cs', 'ReplayPotionIds.cs', 'LogMimicService.Potions.cs']:
        shutil.copyfile(assets / name, root / 'Oracle/Services' / name)
    shutil.copyfile(assets / 'ReplayPotionUi.cs', root / 'Oracle/UI/ReplayPotionUi.cs')

    rep('Oracle/Configuration.cs', '    public int Version { get; set; } = 1;',
        '    public int Version { get; set; } = 1;\n    public Dictionary<uint, ReplayPotionSettings> ReplayPotionImportDefaults { get; set; } = [];')
    models = 'Oracle/Models/TimelineModels.cs'
    rep(models, '    public bool ReplayEnabled { get; set; }',
        '    public bool ReplayEnabled { get; set; }\n    public ReplayPotionSettings ReplayPotions { get; set; } = new();')
    rep(models, '    Sync = 4,', '    Sync = 4,\n    Item = 5,')
    rep(models, '    public uint ActionId { get; set; }\n\n    // Report-local',
        '    public uint ActionId { get; set; }\n    public uint ReplayItemId { get; set; }\n    public bool ReplayItemHq { get; set; }\n\n    // Report-local')
    rep(models, '            ActionId = Kind == TimelineCueKind.Action || IsClockSync',
        '            ReplayItemId = Kind == TimelineCueKind.Item ? ReplayItemId : 0,\n            ReplayItemHq = Kind == TimelineCueKind.Item && ReplayItemHq,\n            ActionId = Kind is TimelineCueKind.Action or TimelineCueKind.Item || IsClockSync')

    wire = 'Oracle/Services/TimelineJson.cs'
    rep(wire, '        if (!hadVersion || doc.FormatVersion < 1)',
        '        ReplayPotionPolicy.Normalize(doc);\n        if (!hadVersion || doc.FormatVersion < 1)')
    rep(wire, '            TimelineCueKind.Sync => "Sync",',
        '            TimelineCueKind.Sync => "Sync",\n            TimelineCueKind.Item => "Item",')
    rep(wire, '        if (text.Equals("Memo", StringComparison.OrdinalIgnoreCase))',
        '        if (text.Equals("Item", StringComparison.OrdinalIgnoreCase))\n        {\n            kind = TimelineCueKind.Item;\n            return true;\n        }\n        if (text.Equals("Memo", StringComparison.OrdinalIgnoreCase))')
    rep(wire, '            case 4:\n                kind = TimelineCueKind.Sync;',
        '            case 5:\n                kind = TimelineCueKind.Item;\n                return true;\n            case 4:\n                kind = TimelineCueKind.Sync;')
    rep(wire, 'n is 0 or 1 or 4', 'n is 0 or 1 or 4 or 5')
    rep('Oracle/Services/ConfigStore.cs', '        SanitizeCueLabels(document);',
        '        ReplayPotionPolicy.Normalize(document);\n        SanitizeCueLabels(document);')
    rep('Oracle/Services/TimelineStore.cs', '            ReplayEnabled = source.ReplayEnabled,',
        '            ReplayEnabled = source.ReplayEnabled,\n            ReplayPotions = source.ReplayPotions.Copy(),')

    imp = 'Oracle/Services/FFLogs/FFLogsImportService.cs'
    rep(imp, '    public string Name { get; init; } = string.Empty;',
        '    public string Name { get; init; } = string.Empty;\n    public ReplayPotionSettings Potions { get; init; } = new();')
    rep(imp, '            var cast = casts[window.Index];',
        '''            var cast = casts[window.Index];
            var isItem = ReplayPotionPolicy.TryDecodeItem(cast.AbilityGameId, out var item);
            if (!isItem && !ReplayPotionPolicy.IsSkill(cast.AbilityGameId)) continue;''')
    rep(imp, '                Kind = TimelineCueKind.Action,',
        '                Kind = isItem ? TimelineCueKind.Item : TimelineCueKind.Action,\n                ReplayItemId = isItem ? item.ItemId : 0,\n                ReplayItemHq = isItem && item.Hq,')
    rep(imp, '            Name = name,', '            Name = name,\n            ReplayPotions = options.Potions.Copy(),')
    panel = 'Oracle/UI/FFLogsImportPanel.cs'
    rep(panel, '            Name = _title,',
        '            Name = _title,\n            Potions = ReplayPotionCatalog.ImportDefaults(classJobId).Copy(),')
    rep(panel, '            && !replayAll\n            && !_importEnemyHitMemos',
        '            && !replayAll\n            && !ReplayPotionCatalog.ImportDefaults(classJobId).Enabled\n            && !_importEnemyHitMemos')
    rep(panel, 'var castsTask = allowed.Count == 0 && !replayAll',
        'var castsTask = allowed.Count == 0 && !replayAll && !options.Potions.Enabled')
    rep(panel, 'var allCues = allowed.Count == 0 && !replayAll',
        'var allCues = allowed.Count == 0 && !replayAll && !options.Potions.Enabled')
    rep(panel, '                .Where(cue => replayAll || allowed.Contains(cue.ActionId))',
        '''                .Where(cue => ReplayPotionPolicy.Include(cue, replayAll, allowed, options.Potions)
                    && (cue.Kind != TimelineCueKind.Item || ReplayPotionCatalog.IsStatPotion(cue.ReplayItemId)))''')
    rep('Oracle/UI/ConfigWindow.cs', '        FFLogsImportActionsUi.DrawForJob(jobId, idPrefix: idPrefix);',
        '''        if (idPrefix != "autoRecordImportActions"
            && ReplayPotionUi.Draw(ReplayPotionCatalog.ImportDefaults(jobId), jobId, idPrefix + "Potions")) C.Save();
        ImGui.Spacing();
        FFLogsImportActionsUi.DrawForJob(jobId, idPrefix: idPrefix);''')
    rep('Oracle/UI/ConfigWindow.Editor.cs', '        DrawEditorMeta(_editDoc);',
        '''        DrawEditorMeta(_editDoc);
        if (_editDoc.ReplayEnabled || _editDoc.Cues.Any(c => c.Kind == TimelineCueKind.Item))
        {
            MirageUi.SubHeader(I18n.Get("potions.title"));
            if (ReplayPotionUi.Draw(_editDoc.ReplayPotions, _editDoc.ClassJobId, "presetPotions")) PersistDocument(_editDoc);
        }''')
    table = 'Oracle/UI/ConfigWindow.CueTable.cs'
    rep(table, '        var kindLabel = CueKindLabel(cue.Kind);\n        if (MirageUi.Dropdown(\n                string.Empty,\n                ref kindLabel,',
        '''        var kindLabel = CueKindLabel(cue.Kind);
        if (cue.Kind == TimelineCueKind.Item) ImGui.TextUnformatted(I18n.Get("potions.kind"));
        else if (MirageUi.Dropdown(
                string.Empty,
                ref kindLabel,''')
    rep(table, '        if (cue.Kind == TimelineCueKind.Memo)\n        {',
        '''        if (cue.Kind == TimelineCueKind.Item)
            ReplayPotionUi.DrawCue(cue, doc.ReplayPotions);
        else if (cue.Kind == TimelineCueKind.Memo)
        {''')

    svc = 'Oracle/Services/LogMimicService.cs'
    rep(svc, '        _doc = doc;', '        ReplayPotionPolicy.Normalize(doc);\n        _doc = doc;\n        PreparePotions(doc);')
    rep(svc, 'c.Kind == TimelineCueKind.Action && c.ActionId != 0 && ReplayStartGate',
        'c.Kind == TimelineCueKind.Action && ReplayPotionPolicy.IsSkill(c.ActionId) && ReplayStartGate')
    rep(svc, '        if (_adaptiveMode) { UpdateAdaptive(now); return; }',
        '        if (UpdatePotions(now)) return;\n        if (_adaptiveMode) { UpdateAdaptive(now); return; }')
    rep(svc, '        _doc = null;', '        _doc = null;\n        _potions = null;\n        LastPotionResult = string.Empty;')
    rep(svc, '        Feedback.LastGameStatus = 0;',
        '        if (cue.Kind != TimelineCueKind.Action || !ReplayPotionPolicy.IsSkill(cue.ActionId)) return false;\n        Feedback.LastGameStatus = 0;')
    rep('Oracle/Services/ReplayNativeTiming.cs', '        var row = PluginServices.DataManager',
        '        if (!ReplayPotionPolicy.IsSkill(actionId)) return default;\n        var row = PluginServices.DataManager')
    rep('Oracle/Services/LogMimicDtrEntry.cs', '        if (_replay.Feedback.LastConfirmed is { } confirmed)',
        '        if (!string.IsNullOrEmpty(_replay.LastPotionResult)) tooltip += "\\n" + _replay.LastPotionResult;\n        if (_replay.Feedback.LastConfirmed is { } confirmed)')

    texts = {
        'potions.title': ('Potions', '탕약'),
        'potions.kind': ('Potion', '탕약'),
        'potions.include': ('Include stat potions', '탕약 포함'),
        'potions.help': ('Use the selected potion at recorded item times, from 0s onward. Missing, unusable or late items are skipped. Changes apply next replay.', '0초 이후 로그의 탕약 시점에 선택한 종류를 사용합니다. 소지하지 않았거나 사용 불가·시간 초과면 건너뜁니다. 변경은 다음 재생부터 적용됩니다.'),
        'potions.auto': ('Auto: highest item level owned for this job', '자동: 직업에 맞는 소지 탕약 중 높은 등급'),
        'potions.search': ('Search potion name...', '탕약 이름 검색...'),
        'potions.owned_only': ('Owned items only', '소지한 종류만 보기'),
        'potions.prefer_hq': ('Prefer HQ (allow normal quality)', 'HQ 우선 (일반 품질도 사용)'),
        'potions.hq_only': ('HQ only', 'HQ만 사용'),
        'potions.nq_only': ('Normal quality only', '일반 품질만 사용'),
        'potions.selected': ('Currently available: {0}', '현재 사용 가능: {0}'),
        'potions.none_owned': ('No matching potion owned. Potion events will be skipped.', '조건에 맞는 탕약이 없습니다. 탕약 기록은 건너뜁니다.'),
        'potions.item_id': ('Item #{0}', '아이템 #{0}'),
        'potions.cue_help': ('Original log item. Replay uses the potion selected above.', '로그에 기록된 원래 탕약입니다. 재생 시 위에서 선택한 탕약을 사용합니다.'),
        'potions.cue_disabled': ('Potion use is disabled for this preset; this row is skipped.', '이 프리셋은 탕약 사용이 꺼져 있어 이 기록을 건너뜁니다.'),
        'potions.skipped_missing': ('Potion skipped: no matching item owned.', '탕약 건너뜀: 조건에 맞는 소지품이 없습니다.'),
        'potions.skipped_status': ('Potion skipped: unavailable (game status {0}).', '탕약 건너뜀: 사용 불가 (게임 상태 {0}).'),
        'potions.skipped_rejected': ('Potion skipped: the game rejected the request.', '탕약 건너뜀: 게임이 사용 요청을 거절했습니다.'),
        'potions.used': ('Potion use requested: {0}', '탕약 사용 요청: {0}'),
    }
    for lang, index in [('en', 0), ('ko', 1)]:
        p = root / f'Oracle/Data/I18n/{lang}.json'
        m = json.loads(p.read_text(encoding='utf-8'))
        m.update({key: value[index] for key, value in texts.items()})
        p.write_text(json.dumps(m, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Applied optional stat potion import, native item use and nonblocking item lane.')
