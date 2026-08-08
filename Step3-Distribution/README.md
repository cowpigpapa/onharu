# Step 3 — 배포 준비 단계

지인 배포를 위한 결과물을 보관할 폴더입니다.

앞으로 다음 파일이 들어갑니다.

- 배포용 ONHARU 실행 파일
- `ONHARU-Setup.exe` 설치 프로그램
- 제거 및 업데이트 확인용 자료
- 배포 전 점검 결과

## 현재 진행 상태

- `Source`: Step2 안정판에서 복사한 배포용 작업 소스
- `ONHARU-ver1.0.0-desktop-layer-preview.exe`: 바탕화면 아이콘 뒤·일반 창 아래 계층과 독립 일정창을 적용한 시험판
- `Installer\ONHARU.iss`: 설치·삭제, 바로가기와 선택형 자동 실행을 구성하는 Inno Setup 설정
- `Installer\build-installer.ps1`: `Installer\Output\ONHARU-Setup-1.0.0.exe` 생성 스크립트

설치 제거 시 `%LOCALAPPDATA%\FamilyPlanner`의 사용자 일정과 설정은 자동 삭제하지 않는다. 재설치 복구와 실수로 인한 데이터 손실을 막기 위한 정책이다.
- `Source\oauth-check.ps1`: client secret 제거와 PKCE 필수 항목 검사

아직 설치 프로그램이 아닌 내부 시험판입니다. Google 로그인과 계정 전환을 확인한 뒤 다음 배포 작업을 진행합니다.
