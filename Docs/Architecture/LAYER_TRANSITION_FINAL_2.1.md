# ONHARU 2.1 공식 이동·고정 전환 방식

## 최종 결정

ONHARU 2.1의 공식 소스는 전환 후보 17을 사용한다.

- 이동→고정: WPF 창이 보이는 동안 Explorer 공유 프레임을 동기 게시한 뒤 WPF HWND를 DWM cloak한다.
- 고정→이동: WPF HWND를 `Opacity=0` 상태로 먼저 uncloak해 합성 트리에 참여시키고, 다음 Render turn에서 정상 Opacity를 지정한 직후 Explorer 공유 프레임을 비활성화한다.

최종 두 줄의 순서는 다음과 같다.

```csharp
Opacity = intendedOpacity;
explorerFrame.Disable();
```

## 이 방식을 선택한 이유

고정 화면은 Explorer의 `SysListView32 / NM_CUSTOMDRAW / CDDS_PREPAINT` 경로에서 GDI로 합성되고, 이동 화면은 WPF/DWM 표면이다. Windows는 이 두 표면을 같은 compositor frame에서 원자적으로 교체하는 공개 API를 제공하지 않는다.

후보 17은 완전한 DWM present ACK를 만들지는 않지만 다음 장점이 확인됐다.

1. 이동→고정은 사용자 실측에서 안정적으로 깜빡임이 없다.
2. 고정→이동도 대부분 안정적이며 잔여 문제는 간헐적인 한 프레임 밝기 변화로 제한된다.
3. 별도 타이머, 동기 대기, 애니메이션이 없어 클릭과 이동 반응을 해치지 않는다.
4. 아이콘 합성·드래그·선택 해제·고정 상태 입력이라는 이미 안정화된 네이티브 경로를 변경하지 않는다.
5. 문제가 생기면 두 줄의 순서만 되돌릴 수 있어 회귀 범위가 작다.

## 실패한 후보와 재도입 금지 항목

### 후보 18 — `CompositionTarget.Rendering` 2회 대기

정상 Opacity 지정 후 Rendering 콜백을 두 번 기다리고 Explorer 프레임을 제거했다. 자동 검사에는 통과했지만 사용자 실측에서 고정→이동 깜빡임이 후보 17보다 더 자주 발생했다.

원인은 Rendering 이벤트가 DWM present ACK가 아니고, 대기하는 동안 반투명 WPF와 Explorer 표면이 함께 존재하는 경쟁 구간만 길어졌기 때문으로 판단한다. 2.1 공식 소스에서 제거했다.

### 재도입 금지

- `DwmFlush()`: 과거 실험에서 UI stall, 클릭 지연과 추가 번쩍임을 만들었다.
- `RDW_ERASE`: 달력 합성 전 wallpaper-only 프레임을 노출했다.
- 긴 WPF opacity fade: 구조적 경쟁을 해결하지 못하고 사용자가 전환을 어색하게 느꼈다.
- 반복 bitmap publish 및 임의 sleep/timer: 전환 시간을 늘리고 입력 반응을 악화했다.
- `g_baseValid`, `g_finalGeneration`, 아이콘 페인트 캐시 수정: 현재 아이콘 이동·선택 안정성을 깨뜨릴 위험이 크다.

## 알려진 제한

후보 17도 Opacity 속성 지정과 DWM 실제 표시 사이의 비동기성을 제거하지는 못한다. 따라서 고정→이동에서 매우 간헐적인 한 프레임 깜빡임이 남을 수 있다. 이는 2.1의 알려진 제한으로 기록하되, 빈도가 낮고 다른 입력·합성 기능을 훼손하지 않는 현재 방식을 공식 채택한다.

## 2026-08-29 회귀 방지

고정 상태 `Ctrl+Z` 지원을 위해 Explorer 루트에 `SetForegroundWindow()`를 호출하면 모드 전환 클릭 순간 바탕화면 전체 재도색이 발생한다. 고정 레이어 입력은 필요한 경우 `SysListView32`에 `SetFocus()`만 부여하며 Explorer 루트를 전경 창으로 올리지 않는다. 품질 게이트는 이 호출의 재도입을 실패로 처리한다.

ONHARU 2.2에서는 후보 17의 최종 교체 순서를 유지하되, WPF HWND를 cloak한 상태에서 정상 Opacity 표면을 두 Render turn 준비한 후 `uncloak → Explorer frame disable`을 실행한다. 과거 후보 18처럼 opacity-zero WPF를 화면에 노출한 상태로 기다리지 않으므로 wallpaper-only 프레임의 노출 빈도를 줄이면서 위치·크기를 보존한다.

## 향후 해결 방향

앞으로 이 문제를 계속 연구하되 2.1 공식 경로를 직접 덮어쓰지 않고 항상 별도 `TransitionCandidates` 후보로 시험한다.

우선순위:

1. 화면 교체를 없애고 Explorer 달력은 계속 유지한 채 투명 이동·크기 조절 컨트롤만 올리는 구조적 프로토타입
2. WPF/GDI/DWM 전환 generation과 고해상도 영상 프레임을 함께 기록하는 계측
3. 짧은 시각 마스킹이 필요하면 전체 fade가 아닌 단일 프레임용 보조 surface를 별도 후보로 검토
4. Windows 빌드·주사율·DPI별 재현 빈도 측정

## 회귀 검사

- 고정→이동 및 이동→고정을 각각 최소 100회 반복한다.
- 투명도 45%, 75%, 95%에서 검사한다.
- 달력 위에 바탕화면 아이콘이 있는 경우와 없는 경우를 모두 검사한다.
- 빠른 왕복 토글 후 첫 클릭, 마우스 커서, 선택 날짜가 정상인지 확인한다.
- `RDW_ERASE`, `DwmFlush`, `CompositionTarget.Rendering` 전환 게이트가 공식 소스에 다시 들어오지 않았는지 품질 게이트로 확인한다.
