# MainWindow 후속 리팩토링 기록

작성일: 2026-08-20

## 목적

1차 partial 리팩토링 이후 시간표·인쇄·상단 UI 등 기능이 추가되면서 `MainWindow.cs`가 431줄에서 491줄로 증가했다. 기능별 파일 구조는 유지됐지만 약 186줄의 `BuildLayout()`과 시작 데이터 보정·창 수명주기가 다시 본체에 모였다. 향후 다국어, 자동 업데이트, 라이선스 모듈을 넣기 전에 본체를 조립 역할로 되돌렸다.

## 분리 결과

| 파일 | 책임 |
|---|---|
| `MainWindow.cs` | 공유 필드, 생성자, 최상위 조립 |
| `MainWindow.Layout.cs` | 헤더·본문·사이드바·필터 UI와 메인 버튼 생성 |
| `MainWindow.Startup.cs` | Google 계정 복구, 기존 데이터 보정, 초기 창 상태, Loaded/Closing와 타이머 |
| `MainWindow.Dialogs.cs` | 달력 중앙 팝업 배치와 공통 알림 호출 |
| `MainWindow.PositionMode.cs` | 이동 가능·고정됨 버튼 및 트레이 상태 갱신 |
| `MainWindow.Theme.cs` | 파스텔 색상, 글자 배율, Brush 도우미 |

기존 `Calendar`, `Detail`, `Display`, `Items`, `Google`, `ExplorerLayer`, `DesktopInput`, `Settings`, `Tray`, `Timetable` partial의 책임은 변경하지 않았다.

## 적용 원칙

- 이번 단계는 동작을 바꾸기 위한 재설계가 아니라 책임 이동과 생성자 정리다.
- `MainWindow.cs`에 새 화면 전체나 네트워크·라이선스 로직을 추가하지 않는다.
- 향후 Localization, Updates, Licensing은 MainWindow partial이 아니라 독립 모듈로 만든다.
- 디자인 코드는 ONHARU 공통 스타일을 재사용하고 정렬·간격·작은 화면을 함께 검증한다.
- 이 원칙은 프로젝트 루트 `AGENTS.md`에 기록해 이후 작업에도 자동 적용한다.

## 검증

- 분리 직후 `App/build.ps1` 컴파일 통과
- `MainWindow.cs`는 491줄에서 111줄로 줄었고 생성자 외 메서드는 남기지 않았다.
- `check-v21.ps1 -Build`로 App, x64 DesktopHook, LayerHost를 다시 빌드했다.
- version, feature pack, migration, search, UI construction, recurrence, multi-day, export, sync security, window position, error log, OAuth, release config의 13개 품질 게이트가 모두 통과했다.
- 최종 로컬 시험본은 `Tests/LocalTest`의 표준 3파일 세트로 생성했다.
