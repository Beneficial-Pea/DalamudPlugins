# [<img src="https://raw.githubusercontent.com/Beneficial-Pea/DalamudPlugins/main/plugins/LogMimic/icon.png" width="80" height="80" alt="LogMimic icon">](https://github.com/Beneficial-Pea/DalamudPlugins) LogMimic

[![Latest release](https://img.shields.io/github/v/release/Beneficial-Pea/DalamudPlugins?label=release)](https://github.com/Beneficial-Pea/DalamudPlugins/releases)
[![Downloads](https://img.shields.io/github/downloads/Beneficial-Pea/DalamudPlugins/total)](https://github.com/Beneficial-Pea/DalamudPlugins/releases)
[한국어](#한국어) · [English](#english)

## 한국어

LogMimic(로그미믹)은 FFLogs의 스킬 사용 기록을 가져와 게임 안에서 타임라인으로 재생하는 FFXIV Dalamud 플러그인입니다. Oracle을 기반으로 만들었으며, 로그에 기록된 스킬 순서와 사용 대상을 따라가는 기능을 개발하고 있습니다.

현재는 초기 실험 버전입니다. 설치용 빌드는 제공하지만, 실제 전투에서의 동작과 한섭·글섭 호환성은 아직 검증하지 않았습니다.

### 주요 기능

- **FFLogs 타임라인:** 가져온 로그를 직업별·로그별 프리셋으로 저장하고 불러옵니다.
- **GCD / oGCD 순서 유지:** 메인 스킬 대기열은 정해진 순서대로 처리합니다.
- **대상 매핑:** 로그에 기록된 대상을 게임 안의 대상과 연결할 수 있습니다.
- **별도 스킬 타이머:** 지정한 생존기나 보조 스킬은 메인 대기열과 따로 처리합니다.
- **스킬 제외:** 재생에 포함하고 싶지 않은 스킬을 프리셋별로 제외할 수 있습니다.
- **수동 시작·중지:** 명령어로 재생을 켜고 끄며, 현재 상태를 확인할 수 있습니다.

### 화면과 언어

상단의 재생, 라이브러리, 로그 가져오기, 대상 매핑 탭에서 작업할 수 있습니다. 재생 화면에서는 불러온 프리셋과 현재 상태를 확인하고 재생을 켜거나 중지할 수 있습니다. 기존 타임라인 편집기는 라이브러리에 있습니다.

오른쪽 위의 **EN / 한국어** 버튼으로 언어를 바꿀 수 있으며, 선택한 언어는 다음 실행에도 유지됩니다. 기본 명령어는 `/lm`입니다. 오른쪽 아래 모서리를 드래그해 창 크기를 조절할 수 있으며, 바꾼 크기는 다음에 열 때도 유지됩니다.

### 설치

1. 게임에서 `/xlsettings`를 열고 **실험적 기능 / Experimental** 탭으로 이동합니다.
2. 사용자 지정 플러그인 저장소에 아래 주소를 추가하고 활성화한 뒤 저장합니다.

   ```text
   https://raw.githubusercontent.com/Beneficial-Pea/DalamudPlugins/main/pluginmaster.json
   ```

3. `/xlplugins`를 열고 **모든 플러그인**에서 `LogMimic`을 검색해 설치합니다.

저장소를 등록한 뒤 최초 설치는 직접 눌러야 합니다. 이후 새 버전은 Dalamud의 플러그인 업데이트 기능으로 받을 수 있습니다. 자동 업데이트를 사용하려면 런처나 Dalamud에서 해당 설정을 확인해 주세요.

### 자동 불러오기

재생 화면의 프리셋에서 **자동 불러오기**를 켜면 해당 지역과 직업에 맞는 타임라인을 자동으로 불러옵니다. 라이브러리에서 지역·직업을 확인하고, 같은 지역에서 보스나 페이즈를 구분해야 한다면 페이즈 프리셋도 지정하세요.

같은 지역·직업·페이즈에는 하나의 프리셋만 지정할 수 있습니다. 다른 프리셋의 자동 불러오기를 켜면 기존 지정은 해제됩니다. 전투 중에는 자동으로 교체하지 않으며, **초읽기 자동 재생**이 켜져 있으면 초읽기 동안 대기하고 0초가 되면 재생을 시작합니다. 각 스킬은 0초 이후의 원래 기록 시각대로 사용합니다. 초읽기 취소 시 전투 전 재생을 멈추며, 사망해도 재생을 중지합니다. 직접 중지한 경우 같은 초읽기에서는 자동으로 다시 켜지지 않습니다.

### 재생 시작 시점과 대상 표시

초읽기 종료(0초) 시 자동 재생을 켜면 0초부터 시작합니다. 수동으로 재생을 켜도 초읽기 중에는 스킬을 사용하지 않습니다. 새 로그는 전투 시작 이후 기록을 가져오며, 기존 프리셋의 음수 시간 항목도 재생에서 제외합니다. 로그를 다시 가져오지 않아도 기존 0초 이후 항목은 사용할 수 있습니다.

라이브러리의 스킬명 옆에는 로그에 기록된 대상을 표시합니다. 영문 몬스터명은 BNpcName 이름 ID로 변환해 현재 클라이언트의 대상과 비교하며, 표시 이름은 가능하면 설치된 클라이언트의 언어를 사용합니다. 같은 이름 ID의 몬스터가 여러 마리라면 아래의 현재 대상 따라가기 옵션을 쓰거나 대상 매핑에서 직접 지정하세요. 다른 로그의 플레이어 대상은 별도로 연결해야 할 수 있습니다. 이름 인덱스의 출처는 [NPC_DATA_SOURCE.md](plugins/LogMimic/ui/NPC_DATA_SOURCE.md)에 있습니다.

### 현재 선택한 대상 따라가기

재생 또는 대상 매핑 화면에서 **현재 선택한 대상 따라가기**를 켜면, 적 대상 스킬은 로그의 적 매핑 대신 현재 선택한 적에게 사용합니다. 기본값은 꺼짐입니다. 동일한 몬스터가 여러 마리여도 직접 선택한 개체를 따라가며, 대상을 바꾸면 다음 스킬 요청부터 적용합니다.

살아 있고 대상으로 선택 가능한 적이 없으면 해당 공격 스킬은 대기합니다. 자신·아군 대상 스킬은 원래 대상 해석을 유지하고 플러그인이 현재 선택한 타깃을 덮어쓰지 않습니다. 이미 게임에 요청한 스킬은 기존 대상으로 나갈 수 있습니다. 라이브러리에는 계속 로그에 기록된 원래 대상을 표시합니다.

### 기본 명령어

| 명령어 | 설명 |
| --- | --- |
| `/lm` | 플러그인 창 열기 |
| `/lm replay list` | 저장된 프리셋 목록 |
| `/lm replay load <이름>` | 프리셋 불러오기 |
| `/lm replay on` | 수동 재생 허용 — 타임라인을 불러오고 초읽기 또는 전투를 시작한 뒤 사용 |
| `/lm replay off` | 재생 중지 |
| `/lm replay status` | 현재 상태 확인 |

### 한섭 / 글섭

두 클라이언트 지원을 목표로 개발하고 있습니다. 현재 빌드는 **Dalamud API 15**를 대상으로 하며, 사용하는 Dalamud와 API 버전이 다르면 목록에 표시되지 않거나 실행할 수 없습니다. 버전 숫자만 바꿔서는 호환되지 않으므로 클라이언트별 확인이 필요합니다.

문의는 디스코드 **달라살려** 채널에 남겨 주세요. 오류 문의 시 사용 중인 서버, 게임·Dalamud 버전, 재현 방법과 관련 로그를 함께 알려 주시면 확인에 도움이 됩니다. 로그를 올리기 전 개인정보나 인증 정보가 포함되어 있지 않은지 확인해 주세요.

---

## English

LogMimic is a Dalamud plugin for FFXIV that imports action records from FFLogs and replays them as in-game timelines. Built on Oracle, it is being developed to follow the action order and targets recorded in a log.

This is an early experimental release. Installable builds are available, but behavior in actual combat and compatibility with the Korean and global clients have not been verified.

### Features

- **FFLogs timelines:** Save and load separate presets for different jobs and logs.
- **GCD / oGCD ordering:** Process the main action queue in its recorded order.
- **Target mapping:** Bind targets from the log to targets in the game.
- **Independent timers:** Handle selected mitigation and utility actions separately from the main queue.
- **Action exclusions:** Leave specific actions out of a preset.
- **Manual controls:** Enable or pause replay and check its current status through commands.

### Interface and language

Use the top tabs for replay, your library, importing logs, and target mapping. The replay page shows the loaded preset and current status, with controls to enable or pause replay. The timeline editor is available in the library.

Switch between English and Korean with the **EN / 한국어** buttons at the top right. Your selection is saved for the next session. Use `/lm` to open the plugin. Drag the bottom-right corner to resize the window; its size is retained when you reopen it.

### Installation

1. Open `/xlsettings` and go to the **Experimental** tab.
2. Add the following custom plugin repository, enable it, and save:

   ```text
   https://raw.githubusercontent.com/Beneficial-Pea/DalamudPlugins/main/pluginmaster.json
   ```

3. Open `/xlplugins`, search for `LogMimic` under **All Plugins**, and install it.

Adding the repository does not install the plugin automatically; the first installation requires a click. Later releases are available through Dalamud's plugin updater. Automatic updates depend on your launcher and Dalamud settings.

### Auto Load

Enable **Auto Load** beside a preset on the replay page to load it when your zone and job match. Check the zone and job in the library; assign a phase preset when you need to distinguish bosses or phases within the same zone.

Each zone, job and phase combination can have one preferred preset. Enabling another preset replaces that preference. Timelines do not switch automatically during combat, and **countdown auto-start** waits during a fresh countdown and enables replay at 0s. Each action retains its recorded time after 0s. Cancelling the countdown before combat or dying pauses replay. Manually pausing prevents the same countdown from restarting it.

### Replay timing and recorded targets

Automatic replay waits until countdown 0s. Manually enabling replay also waits until 0s before issuing actions. New imports use the selected fight's combat window; negative-time actions in older presets are excluded from replay. Existing nonnegative actions can be used without re-importing.

The library displays the recorded target beside each action. English NPC names resolve through BNpcName row IDs and are compared with live NPC name IDs; display names use the installed client's language where available. For multiple matching NPCs, use the current-target option below or bind one explicitly. Player targets from a different party can still require explicit binding. See [NPC_DATA_SOURCE.md](plugins/LogMimic/ui/NPC_DATA_SOURCE.md) for the index provenance.

### Follow currently selected enemy

Enable **Follow currently selected enemy** on the Replay or Targets page to use your selected enemy for enemy-target actions instead of recorded enemy bindings. This option is off by default. It follows the selected instance even among identical NPCs; switching targets applies to the next action request.

Enemy actions wait if there is no living, targetable enemy selected. Self and ally actions retain their original target resolution, and the plugin does not overwrite your current selection. Already queued actions may still use their previously requested target. The library continues to show the original recorded target.

### Commands

| Command | Description |
| --- | --- |
| `/lm` | Open the plugin window |
| `/lm replay list` | List saved presets |
| `/lm replay load <name>` | Load a preset |
| `/lm replay on` | Manually enable replay after loading a timeline and starting a countdown or combat |
| `/lm replay off` | Pause replay |
| `/lm replay status` | Show the current status |

### Korean and global clients

Support for both clients is a development goal. The current build targets **Dalamud API 15**. A different API version may prevent the plugin from appearing in the list or loading. Changing the manifest's version number alone does not make a build compatible.

For questions and support, please use the **달라살려** channel on Discord. For bug reports, include your client region, game and Dalamud versions, steps to reproduce, and relevant logs. Remove personal information and credentials before posting.

## Credits & License

LogMimic is based on [Oracle v1.3.4](https://github.com/exatrines/Oracle) and uses MirageUI. Oracle-derived code is licensed under **AGPL-3.0-or-later**; MirageUI has its own MIT license. See [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md). Each installable release includes an archive of the corresponding patched source.

Automated combat may violate FFXIV's terms of service.
