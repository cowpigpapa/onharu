# ONHARU 2.1 일반 배포 — 1단계 코드 서명 감사

검사일: 2026-08-17

## 현재 상태

- Windows SDK SignTool: 설치됨
  - `C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe`
- 현재 사용자 코드 서명 인증서: 0개
- 설치 파일: `Release\Installer\ONHARU-2.1.0-Setup.exe`
- Authenticode 상태: `NotSigned`
- 설치 프로그램 게시자 메타데이터: `JUAN.HJLEE`
- 제품 버전: `2.1.0`

## 권장 경로

1. 한국의 개인 개발자가 ONHARU EXE 설치판을 직접 배포한다면 공인 CA의 OV 코드 서명 인증서를 사용한다.
2. EV 인증서는 2024년 이후 SmartScreen 즉시 신뢰 혜택이 없어 SmartScreen만을 목적으로 선택하지 않는다.
3. 자체 서명 인증서는 일반 사용자 PC에서 신뢰되지 않으므로 공개 배포에 사용하지 않는다.
4. Microsoft Store의 MSIX 경로는 서명 비용을 줄일 수 있지만, ONHARU의 Explorer 프로세스 연동·네이티브 훅 구조와 설치 제약을 먼저 별도 검증해야 하므로 현재 EXE 배포의 즉시 대체 경로로 간주하지 않는다.

## 다음 단계 입력

- 배포 주체: 개인 또는 사업자/법인
- 인증서에 표시할 영문 법적 이름
- 공인 OV 인증서 발급 방식 선택

인증서를 발급받기 전에는 실제 공개 신뢰 서명을 만들 수 없다. 인증서가 준비되면 ONHARU.exe, OnharuV3.LayerHost.exe, OnharuV3.DesktopHook.dll을 먼저 서명하고, 이 파일을 포함해 설치판을 다시 만든 다음 설치판 자체를 마지막으로 서명하고 RFC 3161 타임스탬프를 검증한다.

## 공식 근거

- Microsoft, Code signing options for Windows app developers:
  https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options
- Microsoft, SmartScreen reputation for Windows app developers:
  https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation
- Microsoft, SignTool:
  https://learn.microsoft.com/en-us/dotnet/framework/tools/signtool-exe
