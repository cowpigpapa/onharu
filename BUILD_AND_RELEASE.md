# ONHARU 2.1 빌드 및 배포 과정

## 1. 왜 빌드가 두 부분인가

ONHARU는 두 프로그램이 협력한다.

1. `App`: C# WPF 애플리케이션. 달력 UI, 일정, 반복, Google 동기화, 설정과 백업을 담당한다.
2. `ExplorerLayer`: x64 C++ DLL과 LayerHost. WPF 화면을 Explorer의 `SysListView32` 그리기 단계에 합성해 배경화면보다 위, 바탕화면 아이콘보다 아래에 표시한다.

고정 상태에서는 App이 32-bit premultiplied BGRA 프레임과 hit-map을 공유 메모리에 게시한다. 네이티브 훅은 `NM_CUSTOMDRAW / CDDS_PREPAINT`에서 Explorer의 깨끗한 배경과 프레임을 한 번 합성한 불투명 최종면을 캐시하고 `BitBlt`한다. 이 방식이 아이콘 이동 시 알파 누적·깜빡임을 해결한 최종 구조다.

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
- `Tests/LocalTest/OnharuV3.DesktopHook.dll`
- `Tests/LocalTest/OnharuV3.LayerHost.exe`

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

최종 파일:

- `Release/ONHARU-2.1.0/ONHARU.exe`
- `Release/ONHARU-2.1.0/OnharuV3.LayerHost.exe`
- `Release/ONHARU-2.1.0/OnharuV3.DesktopHook.dll`
- `Release/Installer/ONHARU-2.1.0-Setup.exe`

## 6. 설치와 업그레이드

- 설치 위치: `%LOCALAPPDATA%\Programs\ONHARU`
- 기존 설치판과 동일한 Inno Setup AppId를 사용하므로 제거 없이 2.1로 업그레이드한다.
- V1 데이터 `%LOCALAPPDATA%\FamilyPlanner`는 수정하지 않는다.
- 기존 데이터 저장소 `%LOCALAPPDATA%\OnharuV3`를 그대로 사용한다.
- 제거 프로그램은 실행 파일과 바로가기를 제거하지만 사용자 일정·설정·백업·Google 토큰은 보존한다.

## 7. 전체 사용자 배포 전 필수 확인

- 설치→실행→제거 왕복 시험
- Windows 재부팅과 Explorer 재시작 후 자동 복구
- 100%, 125%, 150% DPI 및 다중 모니터
- Windows 10과 Windows 11
- Google OAuth 프로덕션 전환 및 검증 승인
- Authenticode 코드 서명은 비용 정책상 2.1.0에서 보류했다. 미서명 설치판은 SmartScreen의 `알 수 없는 게시자` 안내가 나타날 수 있으므로 SHA-256을 함께 제공한다.

2.1 설치 파일은 현재 코드 서명 없이 배포한다. Explorer 훅 DLL 특성상 SmartScreen 또는 백신 경고 가능성이 있으므로 공식 다운로드 위치와 `Release/Installer/SHA256SUMS.txt`의 해시를 함께 안내한다.
