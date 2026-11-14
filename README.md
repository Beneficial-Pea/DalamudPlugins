**LogMimic · 로그미믹**

[![Release](https://img.shields.io/github/v/release/Beneficial-Pea/DalamudPlugins?label=release)](https://github.com/Beneficial-Pea/DalamudPlugins/releases)
[![Downloads](https://img.shields.io/github/downloads/Beneficial-Pea/DalamudPlugins/total)](https://github.com/Beneficial-Pea/DalamudPlugins/releases)

[한국어](#한국어) · [English](#english)

## 한국어

FFLogs의 전투 기록을 불러와 스킬 순서와 타이밍을 재생하는 파이널판타지 XIV 달라무드 플러그인입니다.

### 주요 기능

- **로그 재생:** FFLogs 전투 기록을 가져와 현재 캐릭터의 글쿨에 맞춰 스킬을 재생합니다.
- **프리셋 관리:** 전투별 프리셋을 저장하고 지역·직업에 맞게 자동으로 불러옵니다.
- **재생 설정:** 사용할 스킬과 탕약, 페이즈 처리 방식을 전투에 맞게 조정합니다.
- **직접 제어:** 공격 대상을 직접 선택하고, 게임 정보바에서 재생을 켜거나 중지합니다.

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

- **Log replay:** Import FFLogs combat records and replay actions at your character's current GCD pace.
- **Preset management:** Save presets for each fight and load them automatically by zone and job.
- **Replay settings:** Adjust actions, potion use and phase handling for each fight.
- **Direct control:** Choose targets yourself and start or pause replay from the server info bar.

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
