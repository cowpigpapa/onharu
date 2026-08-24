# ONHARU 빌드 및 배포 과정

## 버전 정책

- 정식 공개 버전은 `MAJOR.MINOR.PATCH` 세 자리로 통일한다.
- GitHub에 한 번 공개한 버전 번호와 태그는 수정본에 재사용하지 않는다.
- 버그 수정·기본 설정 보완은 PATCH를 올린다. 예: `2.2.1 → 2.2.2`.
- 기능 묶음 추가는 MINOR를 올린다. 예: `2.2.x → 2.3.0`.
- 호환성이 크게 바뀌면 MAJOR를 올린다.
- 내부 시험 파일은 `2.2.2-local-test`, 필요하면 배포 후보는 `2.2.2-rc1`로 구분하되 GitHub 최신 정식 Release에는 올리지 않는다.
- Assembly, 화면 버전, Inno Setup, 설치 파일명, Portable ZIP, README, 웹 다운로드, GitHub 태그를 같은 버전으로 맞춘다.

## 사용자 승인형 자동 업데이트

- 앱은 시작 후 하루에 한 번 GitHub의 `cowpigpapa/onharu` 최신 Release를 조회한다.
- 현재 Assembly 버전보다 높은 `vX.Y.Z` 태그만 업데이트로 판단한다.
- 사용자가 `다운로드 후 설치`를 선택하기 전에는 파일을 내려받거나 실행하지 않는다.
- `*-Setup.exe`와 `SHA256SUMS.txt`를 함께 내려받아 SHA-256이 일치할 때만 설치 파일을 실행한다.
- 확인 실패 시 자동 설치하지 않고 GitHub Release 페이지만 연다.
- 새 릴리스에는 Setup EXE와 `SHA256SUMS.txt` 자산이 반드시 있어야 한다.

## 설치 프로그램 언어

Inno Setup은 한국어와 영어를 제공한다. 설치 시작 시 Windows 언어에 맞는 언어가 기본 선택되며 사용자가 바꿀 수 있다. 바로가기, 자동 시작, 실행 문구는 `[CustomMessages]`의 언어별 문구를 사용한다.

## 1. 왜 빌드가 두 부분인가

ONHARU는 두 프로그램이 협력한다.

1. `App`: C# WPF 애플리케이션. 달력 UI, 일정, 반복, Google 동기화, 설정과 백업을 담당한다.
2. `ExplorerLayer`: x64 C++ DLL과 LayerHost. WPF 화면을 Explorer의 `SysListView32` 그리기 단계에 합성해 배경화면보다 위, 바탕화면 아이콘보다 아래에 표시한다.

고정 상태에서는 App이 32-bit premultiplied BGRA 프레임과 hit-map을 공유 메모리에 게시한다. 네이티브 훅은 `NM_CUSTOMDRAW / CDDS_PREPAINT`에서 Explorer의 깨끗한 배경과 프레임을 한 번 합성한 불투명 최종면을 캐시하고 `BitBlt`한다. 이 방식이 아이콘 이동 시 알파 누적·깜빡임을 해결한 최종 구조다.

### 2.1 공식 이동·고정 전환: 후보 17

고정 화면은 Explorer/GDI 표면이고 이동 화면은 WPF/DWM 표면이라 공개 API만으로 두 화면을 한 compositor frame에 원자 교환할 수 없다. 2.1은 사용자 실측에서 가장 안정적이었던 후보 17을 공식 채택한다.

고정→이동의 마지막 단계는 반드시 다음 순서를 유지한다.

```csharp
Opacity = intendedOpacity;
explorerFrame.Disable();
```

WPF를 `Opacity=0`으로 미리 uncloak한 후 다음 Render turn에 정상 Opacity를 요청하고 Explorer 프레임을 제거한다. 이 방식은 이동→고정에서 깜빡임이 없고, 고정→이동의 간헐적 한 프레임 문제를 최소화하면서 입력 지연을 만들지 않았다.

`CompositionTarget.Rendering`을 두 번 기다린 후보 18은 오히려 깜빡임 빈도가 증가하여 폐기했다. `DwmFlush`, `RDW_ERASE`, 페이드, 임의 sleep과 반복 publish도 과거 회귀 때문에 공식 빌드에 넣지 않는다. 전체 근거와 향후 연구 방향은 `Docs/Architecture/LAYER_TRANSITION_FINAL_2.1.md`에 기록했다.

### 2.2 해상도·DPI 변경: 후보 03

고정 Explorer frame과 이동 WPF 창은 하나의 물리 픽셀 RECT를 기준으로 한다. App은 manifest에서 PerMonitorV2를 선언하고 `ONHARU.exe.config`로 WPF DPI 변경을 활성화한다. 최소 창 크기도 DIP가 아니라 820×560 물리 px가 되도록 현재 DPI로 역산하며, `WM_DPICHANGED` 뒤 OS suggested RECT가 현재 frame을 바꾸면 다음 Render turn에 직전 물리 RECT를 복원한다.

따라서 `ONHARU.exe.config`는 선택 파일이 아니라 실행 파일과 함께 배포해야 하는 필수 구성요소다. 상세 실험은 `Tests/LocalTest/PlacementCandidates/03-pmv2-physical-rect-authority/CANDIDATE_03_RESULT.md`에 기록했다.

## 2. 필요한 도구

- Windows 10/11 x64
- .NET Framework 4.x에 포함된 64-bit `csc.exe`
- Visual Studio 2022 C++ Build Tools
- Windows 10/11 SDK
- Inno Setup 6
- 로컬 전용 `App/OAuthCredentials.local.cs`

`OAuthCredentials.local.cs`에는 Google Desktop OAuth client secret이 들어 있으므로 외부에 공개하거나 배포 폴더에 복사하지 않는다. 값은 컴파일된 App에 포함된다.

## 3. 로컬 시험 빌드

프로젝트 루트에서 다음 한 명령만 실행한다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-local-test.ps1
```

생성물:

- `Tests/LocalTest/ONHARU-2.1-local-test.exe`
- `Tests/LocalTest/ONHARU-2.1-local-test.exe.config`
- `Tests/LocalTest/Onharu.DesktopHook.dll`
- `Tests/LocalTest/Onharu.LayerHost.exe`

`App`, `ExplorerLayer`, `Release`에는 재생성 가능한 시험 실행 파일을 남기지 않는다. `App/build.ps1`을 직접 실행해도 App 결과는 같은 `Tests/LocalTest` 폴더에 생성된다.

`V3`는 내부 개발 코드명이다. 2.1에서도 기존 데이터와 네이티브 IPC 호환을 위해 유지한다.

## 4. 핵심 회귀 검사

프로젝트 루트에서 빌드와 자동 검사 13종을 한 번에 실행한다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\check-v21.ps1 -Build
```

고정 레이어는 수동으로 다음을 확인한다.

- 아이콘이 달력 위에 보이고 클릭·드래그되는가
- 아이콘을 달력 안팎으로 옮길 때 달력 투명도와 아이콘 둘레가 깜빡이지 않는가
- 바탕화면 선택 사각형이 달력 위를 지나도 유지되는가
- 달력 클릭 시 선택된 바탕화면 아이콘이 해제되는가
- 고정 상태에서 날짜·일정·설정·Slider·상세 스크롤 입력이 동작하는가
- 이동 가능 상태와 고정 상태의 위치·크기가 일치하는가
- 고정→이동의 매우 간헐적인 한 프레임 변화가 기존 후보보다 악화되지 않았는가

상세 수동 순서는 `V2.1_TEST_CHECKLIST.md`를 따른다. 아이콘 바로 둘레와 깜빡임의 최종 품질은 실화면으로 확인한다.

## 5. 공식 릴리스 빌드

프로젝트 루트에서 한 명령을 실행한다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-release.ps1
```

스크립트의 처리 순서:

1. 실행 중인 개발 App과 LayerHost 종료
2. 공개 이름 `ONHARU.exe`로 WPF App 최적화 빌드
3. 네이티브 DLL/LayerHost를 x64 `/MT`로 빌드
4. `Release/ONHARU-2.1.0` staging 생성
5. staging 파일 SHA-256 생성
6. Inno Setup으로 설치 프로그램 생성
7. 설치 파일 SHA-256 생성
8. App·ExplorerLayer의 재생성 가능한 중간 바이너리 삭제

공식 릴리스 전에 `App/MainWindow.ExplorerLayer.cs`가 후보 17 순서를 유지하는지 `feature-pack-check.ps1`가 검사한다. 전환 실험본을 공식 소스에 남긴 채 설치판을 만들지 않는다.

최종 파일:

- `Release/ONHARU-2.1.0/ONHARU.exe`
- `Release/ONHARU-2.1.0/ONHARU.exe.config`
- `Release/ONHARU-2.1.0/Onharu.LayerHost.exe`
- `Release/ONHARU-2.1.0/Onharu.DesktopHook.dll`
- `Release/Installer/ONHARU-2.2.2-Setup.exe`

## 6. 설치와 업그레이드

- 설치 위치: `%LOCALAPPDATA%\Programs\ONHARU`
- 기존 설치판과 동일한 Inno Setup AppId를 사용하므로 제거 없이 2.1로 업그레이드한다.
- V1 데이터 `%LOCALAPPDATA%\FamilyPlanner`는 수정하지 않는다.
- 공식 데이터 저장소와 자동 백업 루트는 `%LOCALAPPDATA%\Onharu`로 통일한다.
- 제거 프로그램은 실행 파일과 바로가기를 제거하지만 사용자 일정·설정·백업·Google 토큰은 보존한다.

## 7. 전체 사용자 배포 전 필수 확인

- 설치→실행→제거 왕복 시험
- Windows 재부팅과 Explorer 재시작 후 자동 복구
- 100%, 125%, 150% DPI 및 다중 모니터
- Windows 10과 Windows 11
- Google OAuth 프로덕션 전환 및 검증 승인
- Authenticode 코드 서명은 비용 정책상 2.1.0에서 보류했다. 미서명 설치판은 SmartScreen의 `알 수 없는 게시자` 안내가 나타날 수 있으므로 SHA-256을 함께 제공한다.

2.1 설치 파일은 현재 코드 서명 없이 배포한다. Explorer 훅 DLL 특성상 SmartScreen 또는 백신 경고 가능성이 있으므로 공식 다운로드 위치와 `Release/Installer/SHA256SUMS.txt`의 해시를 함께 안내한다.

## 8. GitHub와 onharu.app 배포

- GitHub 공개 소스에는 `App`, `ExplorerLayer`, `Installer`, `Docs`, `Distribution`과 루트 빌드 문서만 포함한다.
- `App/OAuthCredentials.local.cs`, 사용자 데이터, `Release`, `Publish`, `Review`, 과거 시험 바이너리는 공개 소스와 Source ZIP에서 제외한다.
- GitHub Release에는 설치판, Portable ZIP, Source ZIP, `SHA256SUMS.txt`를 올린다.
- 웹 배포 원본은 `Publish/ONHARU-Web`이며 다운로드 파일은 그 아래 `downloads`에 둔다.
- `Distribution/ONHARU-DOWNLOAD.html`과 웹 배포본의 파일 크기·SHA-256을 새 릴리스 값으로 함께 갱신한다.
- 배포 후에는 `https://onharu.app/downloads/`에서 설치판과 포터블을 실제로 다시 내려받아 로컬 릴리스 SHA-256과 비교한다.
