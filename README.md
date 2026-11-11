# Beneficial-Pea / DalamudPlugins

Public custom plugin repository for FFXIV Dalamud.

## Plugins

| Plugin | Status | Description |
| --- | --- | --- |
| **LogMimic** | Experimental source only | FFLogs action timeline replay based on Oracle; includes GCD/oGCD strict queue, target mapping, independent mitigation, and skill exclusions. |

**Status (2026-11-11):** The experimental LogMimic source **successfully compiled in GitHub Actions**, producing a review-only DLL artifact. It has **not been verified in-game or published as an installable release**. The custom repository manifest remains empty until release. See the [build run](https://github.com/Beneficial-Pea/dalamudplugins/actions/runs/43100774798) and [third-party notices](NOTICE.md).

## Planned Dalamud repository URL

```
https://raw.githubusercontent.com/Beneficial-Pea/dalamudplugins/main/pluginmaster.json
```

The repository may eventually contain multiple plugins. Each needs a separate distinct InternalName and published release; simply adding source files does not make a plugin installable.

## LogMimic

- Icon: `plugins/LogMimic/icon.png`
- Source package: `LogMimic_SourceKit.zip` (experimental source patches, setup and publishing scripts)
- Upstream: [Oracle 1.3.4](https://github.com/exatrines/Oracle); MirageUI submodule.
- License: the Oracle-derived code is subject to [AGPL-3.0-or-later](https://github.com/exatrines/Oracle/blob/main/LICENSE), including attribution and corresponding source obligations. MirageUI has its own MIT license.
- Build workflow: GitHub Actions **Build and publish LogMimic**. Do not publish an untested plugin.

Automated combat may violate FFXIV's terms of service.
