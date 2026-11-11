# Beneficial-Pea / DalamudPlugins

Public custom plugin repository for FFXIV Dalamud.

## Plugins

| Plugin | Status | Description |
| --- | --- | --- |
| **LogMimic** | Experimental source only | FFLogs action timeline replay based on Oracle; includes GCD/oGCD strict queue, target mapping, independent mitigation, and skill exclusions. |

**Important:** LogMimic is **not yet built or tested in-game**. There is no verified installable DLL or released plugin available right now. The custom repository manifest stays empty until a build succeeds.

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
