# ONHARU 1.0.0 배포 가이드

## 공식 배포 파일

- 설치 파일: `Installer\Output\ONHARU-Setup-1.0.0.exe`
- 단일 실행 파일: `Release\ONHARU.exe`
- 무결성 확인: `Release\SHA256SUMS.txt`

일반 사용자에게는 설치·제거와 바로가기 구성이 포함된 설치 파일을 전달한다. 단일 EXE는 설치 없이 시험할 때만 사용한다.

## 설치와 제거

- 관리자 권한 없이 `%LOCALAPPDATA%\Programs\ONHARU`에 설치한다.
- 시작 메뉴와 Windows의 설치된 앱 목록에 ONHARU 및 제거 프로그램이 등록된다.
- 바탕화면 바로가기와 Windows 자동 실행은 설치 중 선택한다.
- 제거해도 `%LOCALAPPDATA%\FamilyPlanner`의 일정, 설정, 백업과 Google 연결 데이터는 보존한다.

## 배포 전 확인

1. `build-release.ps1`을 실행한다.
2. `Release\ONHARU.exe`의 신규·수정·삭제, 여러 날 일정, 반복과 재실행을 확인한다.
3. 설치 파일로 설치한 뒤 시작 메뉴, 트레이 아이콘, 자동 실행 선택과 제거를 확인한다.
4. Google OAuth 게시 상태를 결정한다.
5. 코드 서명이 없다면 받는 사람에게 Windows SmartScreen 경고 가능성을 미리 알린다.

## Google OAuth 선택

- 소수 지인만 시험: Google Cloud의 OAuth 대상에서 각 Gmail 주소를 테스트 사용자로 추가한다. Testing 상태는 최대 100명이며 승인은 7일 후 만료될 수 있다.
- 누구나 사용: 앱을 In production으로 게시하고 필요한 브랜딩·민감 범위 검증을 진행한다.
- 현재 요청 범위는 Calendar 일정 읽기·쓰기와 Calendar List 읽기다. 검증 신청 시 실제 기능과 동일하게 설명한다.

## 아직 포함되지 않은 항목

- Windows 코드 서명
- 자동 업데이트
- 바탕화면 아이콘보다 아래에 표시되는 네이티브 렌더링

이 세 항목은 ONHARU 1.0.0의 설치·사용을 막지는 않지만, 불특정 다수 공개 배포 전에 별도 작업을 권장한다.
