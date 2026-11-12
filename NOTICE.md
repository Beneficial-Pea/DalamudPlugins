# Third-party notices and origin

## Oracle

**LogMimic** is a derivative of **Oracle** by **mirage** (exatrines), based on Oracle version 1.3.4.

- Original source: https://github.com/exatrines/Oracle
- Source version: https://github.com/exatrines/Oracle/tree/v1.3.4
- Original license: AGPL-3.0-or-later, preserved in `LICENSE`.
- Modifications: FFLogs full-cast replay, target tracking and mapping, skill exclusion lists, independent utility/mitigation scheduling, separate `LogMimic` identity, and custom branding.

The original Oracle project is independent from LogMimic. This derivative is not endorsed or supported by the original developers.

## MirageUI

Oracle uses MirageUI by exatrines under the MIT license. The original repository and license are available at:
https://github.com/exatrines/MirageUI

## Development status

The experimental source compiled in GitHub Actions on November 11, 2026, but the plugin has **not been verified in-game**. Do not claim production readiness.

The `LogMimic_SourceKit.zip` patch source plus Oracle v1.3.4 checkout (including MirageUI) reconstructs the derivative for review and rebuilding.

## English NPC name index

The compressed BNpcName index is derived from xivapi/ffxiv-datamining English game data. Game names belong to Square Enix. See Data/NPC_DATA_SOURCE.md in the plugin package and plugins/LogMimic/ui/NPC_DATA_SOURCE.md in this repository for the pinned source and transformation.

## Stat-potion identification

`ReplayPotionIds.cs` contains item IDs and English names from the XIVAPI v2 Item sheet, queried with `ItemSearchCategory=43 FilterGroup=6` and restricted to the five primary-stat medicines. It identifies recorded potions absent from an older client. Display names, selectable items and inventory use come from the installed client's sheets. Potion classification and native item use reference Rotation Solver Reborn's `MedicineItem` and `BaseItem` implementations.
