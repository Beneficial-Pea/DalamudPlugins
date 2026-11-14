**LogMimic · 로그미믹**

[![Release](https://img.shields.io/github/v/release/Beneficial-Pea/DalamudPlugins?label=release)](https://github.com/Beneficial-Pea/DalamudPlugins/releases)
[![Downloads](https://img.shields.io/github/downloads/Beneficial-Pea/DalamudPlugins/total)](https://github.com/Beneficial-Pea/DalamudPlugins/releases)

[한국어](#한국어) · [English](#english)

## 한국어

FFLogs의 전투 기록을 불러와 스킬 순서와 타이밍을 재생하는 파이널판타지 XIV 달라무드 플러그인입니다.

### 주요 기능

- **로그 재생:** FFLogs에서 가져온 스킬 기록을 프리셋으로 저장하고 재생합니다.
- **자동 불러오기:** 지역·직업·페이즈에 맞는 프리셋을 불러옵니다.
- **초읽기 자동 시작:** 0초부터 현재 캐릭터의 글쿨에 맞춰 스킬을 재생합니다.
- **직접 타깃 선택:** 공격할 대상은 직접 선택합니다. 새로 사용하는 스킬은 현재 선택한 대상에게 사용하며, 대상이 없으면 기다립니다.
- **대상 전환:** 보스에서 쫄로 바꿀 때 아직 실행되지 않은 공격 예약도 새 대상에 맞춥니다. 취소된 예약은 같은 기록부터 다시 시도하며, 이미 전송했거나 시전 중인 스킬은 기존 대상을 유지합니다.
- **탕약 선택:** 가져올 때 탕약 포함 여부와 종류·품질을 고릅니다. 자동 선택은 직업에 맞는 소지 탕약을 사용하며, 없거나 사용할 수 없으면 건너뜁니다.
- **스킬 설정:** 특정 스킬을 제외하거나 생존기·보조 스킬을 별도 타이머로 재생할 수 있습니다.
- **글쿨·논글쿨 분리:** 글쿨 순서는 유지하고, 사용 가능한 논글쿨을 사이에 넣습니다. 생존기 하나가 대기해도 다른 스킬을 막지 않습니다.
- **쿨타임 건너뛰기:** 스킬 자체의 쿨타임이 남았으면 해당 기록을 건너뛰고 다음 스킬을 시도합니다. 공용 글쿨은 기다리며, 충전이 남은 스킬은 사용합니다.
- **프록 대응:** 무도가의 캐스케이드·파운틴·윈드밀·블레이드 샤워와 각 프록기, 음유시인의 파열 사격·찬란한 화살을 현재 프록에 맞춰 대치합니다. 기록과 순서는 그대로 두고, 실행할 때 사용할 기술을 고릅니다.
- **페이즈 처리 선택:** 프리셋마다 대기한 스킬부터 이어 쓸지, 실제 페이즈 신호에 맞춰 해당 구간부터 재생할지 선택합니다.
- **재생 표시:** 단축바 눌림 모션과 게임 정보바의 `LM: ON/OFF`로 재생 상태를 확인합니다.

### 설치

1. `/xlsettings` → **실험적 기능 / Experimental**을 엽니다.
2. 사용자 지정 플러그인 저장소에 아래 주소를 추가하고, 체크한 뒤 저장합니다.

   ```text
   https://raw.githubusercontent.com/Beneficial-Pea/DalamudPlugins/main/pluginmaster.json
   ```

3. `/xlplugins`에서 **LogMimic**을 검색해 설치합니다.

저장소를 추가한 뒤 최초 설치는 직접 눌러야 합니다. 이후에는 달라무드의 플러그인 업데이트로 받을 수 있습니다.

### 사용 방법

1. `/lm`을 열고 **설정 → FFLogs 연결**에 Client ID와 Client Secret을 입력합니다. [FFLogs API 설정](https://www.fflogs.com/api/clients)에서 발급받을 수 있습니다.
2. **로그 가져오기**에서 보고서 URL을 넣고 전투와 플레이어를 선택해 가져옵니다.
3. **라이브러리**에서 프리셋의 지역·직업과 자동 불러오기 설정을 확인합니다. **초읽기 종료(0초) 시 자동 재생**을 켜면 초읽기 0초부터 재생을 시작합니다.

**자동 재생은 초읽기 0초부터 시작합니다. 0초 이전에 쓰는 오프닝은 직접 사용해야 합니다.**

**페이즈 처리**는 재생 화면이나 라이브러리에서 전투 전에 고릅니다. 기본값은 **대기한 스킬부터 이어서**이며, 프리셋마다 저장됩니다. **페이즈 전환에 맞춰 해당 구간부터**는 실제 전투 동기화 신호가 필요합니다. 라이브러리의 동기화 행을 우클릭해 **페이즈 시작으로 사용**을 지정할 수 있고, 긴 대기 구간의 기존 동기화도 사용합니다. 신호가 없으면 시간만 보고 임의로 구간을 건너뛰지 않습니다.

전체 자동 불러오기, 단축바 클릭 모션, **현재 글쿨에 맞춰 재생**은 **설정 → LogMimic**에서 바꿀 수 있습니다. 글쿨 맞춤을 끄면 원래 기록 시각대로 재생합니다. 별도 타이머로 지정한 생존기·보조 스킬은 전투 시간을 유지하며, 사망하면 재생이 중지됩니다.

게임 정보바의 **LM: ON/OFF**를 왼쪽 클릭하면 재생을 켜거나 중지합니다. 오른쪽 클릭하면 로그미믹 창이 열립니다. 마우스를 올리면 대기 중인 스킬과 마지막 사용 확인을 볼 수 있습니다. 재생을 켜려면 프리셋을 불러오고 초읽기 또는 전투를 시작한 상태여야 합니다.

### 명령어

| 명령어 | 기능 |
| --- | --- |
| `/lm` | 메인 창 열기 |
| `/lm config` | 설정 열기 |
| `/lm replay on` | 수동 재생 켜기 — 타임라인이 시작된 상태에서 사용 |
| `/lm replay off` | 재생 중지 |

### 문의

디스코드 **달라살려** 채널에 남겨 주세요. 오류가 나면 사용 중인 서버와 달라무드 버전, 재현 상황을 함께 알려 주세요.

현재 **Dalamud API 15** 기준의 테스트 버전입니다. 한섭·글섭 지원을 목표로 개발 중이며, 실제 전투 동작과 클라이언트별 호환성은 확인이 필요합니다.

---

## English

A Dalamud plugin for Final Fantasy XIV that imports FFLogs combat records and replays their action order and timing.

### Features

- **Log replay:** Save imported action records as presets and replay them.
- **Auto Load:** Load presets that match your zone, job and phase.
- **Countdown start:** Begin at 0s and follow your character's current GCD.
- **Manual targeting:** Select targets yourself. New action requests use your current selection and wait when no target is selected.
- **Target changes:** Unsent replay attack queues follow your selected boss or add. Cancelled input retries the same record; actions already sent or casting retain their committed target.
- **Potion selection:** Choose whether to include potions, plus the item and quality. Auto selection uses an owned potion for your job; missing or unusable items are skipped.
- **Action settings:** Exclude individual actions or run mitigation and utility actions on independent timers.
- **Separate GCDs and abilities:** Preserve GCD order and weave usable abilities between them. One unavailable mitigation does not block other actions.
- **Skip personal cooldowns:** Skip an occurrence that is still on its own cooldown and try the next action. Shared GCD waits remain, and charged actions are usable while at least one charge is available.
- **Live proc pairs:** Choose the base or proc action for Dancer's four Cascade/Fountain/Windmill/Bladeshower pairs and Bard's Burst Shot/Refulgent Arrow pair using current proc buffs. Saved records and their order remain unchanged.
- **Phase handling:** Each preset can continue from its waiting action or resume from a new section on an actual phase signal.
- **Replay feedback:** Show native hotbar presses and `LM: ON/OFF` in the server info bar.

### Installation

1. Open `/xlsettings` → **Experimental**.
2. Add this custom plugin repository, enable it, and save:

   ```text
   https://raw.githubusercontent.com/Beneficial-Pea/DalamudPlugins/main/pluginmaster.json
   ```

3. Open `/xlplugins`, search for **LogMimic**, and install it.

The first installation requires a click after adding the repository. Later versions are available through Dalamud's plugin updater.

### Getting started

1. Open `/lm` and enter your Client ID and Client Secret in the FFLogs settings. Create them in [FFLogs API settings](https://www.fflogs.com/api/clients).
2. Under **Import logs**, enter a report URL and select the fight and player to import.
3. Check the preset's zone, job and Auto Load settings in the **Library**. Enable **Automatically replay at countdown 0s** to begin replay at 0s.

**Automatic replay starts at countdown 0s. Use any opener or preparation actions before 0s manually.**

Choose **Phase handling** before combat on the Replay page or in the Library. Each preset saves its own choice; the default is **Continue from the waiting action**. **Resume from the new phase section** requires an actual encounter sync. Right-click a Sync row in the Library and select **Use as phase start**. Existing syncs in long recorded gaps also apply. Without a signal, elapsed time alone never skips a section.

The global Auto Load switch, hotbar button presses and **Follow my current GCD** are under **Settings → LogMimic**. Disable GCD matching to use original timestamps. Explicit independent utility and mitigation timers retain encounter time. Death pauses replay.

Left-click **LM: ON/OFF** in the server info bar to enable or pause replay. Right-click it to open LogMimic, or hover to see waiting actions and the last confirmed use. Enabling replay requires a loaded replay preset and an active countdown or combat timeline.

### Commands

| Command | Function |
| --- | --- |
| `/lm` | Open the main window |
| `/lm config` | Open settings |
| `/lm replay on` | Manually enable replay after the timeline has started |
| `/lm replay off` | Pause replay |

### Support

Please use the **달라살려** channel on Discord. For bug reports, include your client region, Dalamud version and steps to reproduce.

This is a test release targeting **Dalamud API 15**. Support for Korean and global clients is in development; actual combat behavior and compatibility with each client still need verification.

## Credits & License

Based on [Oracle v1.3.4](https://github.com/exatrines/Oracle), licensed under **AGPL-3.0-or-later**, with [MirageUI](https://github.com/exatrines/MirageUI) under MIT. Action use and hotbar effects reference [Rotation Solver Reborn](https://github.com/FFXIV-CombatReborn/RotationSolverReborn).

See [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md). Each release includes its corresponding source archive.
