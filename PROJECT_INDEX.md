# ONHARU 2.1 프로젝트 인덱스

## 한눈에 보는 구조

```text
Onharu_v2.1
├─ App               ① 달력 프로그램 개발 소스
├─ ExplorerLayer     ② 아이콘 아래 표시 기술 개발 소스
├─ Installer         ③ 설치 파일 제작 설정
├─ Tests             ④ 로컬 시험본과 외부 검토 패키지
├─ Release           ⑤ 정식 배포 파일만
└─ Docs              ⑥ 현재 기술 설명·감사 문서
```

## 1. 실제 제품을 만드는 개발 파일

### `App` — ONHARU 본체

가장 중요한 C# 소스 폴더다. 화면 디자인, 달력, 일정 등록·수정, 반복 일정, 설정, 백업, Google 동기화, 알림을 담당한다.

- `*.cs`: 실제 프로그램 코드
- `Assets`: 아이콘 등 화면 자원
- `build.ps1`: App만 개발 빌드
- `*-check.ps1`: 일정·버전·동기화 등의 자동 검사

### `ExplorerLayer` — 바탕화면 아이콘 아래 레이어

ONHARU의 핵심 특수 기술을 담당하는 C++ 소스다. App이 만든 달력 화면을 Windows Explorer에 합성해 `배경화면 < 달력 < 바탕화면 아이콘` 순서로 보여준다.

- `DesktopHook.cpp`: Explorer의 아이콘 그리기 단계에 달력을 합성
- `LayerHost.cpp`: Explorer 훅을 시작·감시·복구
- `SharedFrame.h`, `LayerShared.h`: App과 네이티브 레이어 사이의 화면·입력 약속
- `build.ps1`: DLL과 LayerHost 개발 빌드

`App`과 `ExplorerLayer`가 함께 있어야 완전한 ONHARU가 된다.

### `Installer` — 설치 프로그램 설계도

프로그램 소스가 아니라 Inno Setup용 설치 설정이다. 설치 위치, 시작 메뉴, 바탕화면 바로가기, 기존 V1 덮어쓰기, 제거 방법을 정의한다.

- `ONHARU.iss`: 설치 프로그램 제작 설정

## 2. 시험과 검증

### `Tests` — 모든 시험 산출물

- `Tests/LocalTest`: 현재 소스로 다시 만든 로컬 시험본 3개 파일
- `Tests/Review`: 외부 AI 검증에 전달한 압축 패키지

구버전 후보 EXE와 빌드 임시 파일은 보관하지 않는다. 필요하면 소스와 빌드 스크립트로 다시 만든다.

## 3. 완성 결과물

### `Release` — 생성 결과물

개발자가 아닌 사용자에게 전달하는 최종 결과다.

- `Release/ONHARU-2.1.0`: 정식 빌드 때 생성되는 무설치 검증 세트
- `Release/Installer/ONHARU-2.1.0-Setup.exe`: 정식 빌드 때 생성되는 일반 사용자용 설치 파일
- `Release/Installer/SHA256SUMS.txt`: 정식 빌드 때 생성되는 무결성 확인값

현재 2.1.0 정식 배포 스테이지와 설치판이 생성되어 있다. 일반 배포에는 `Release/Installer/ONHARU-2.1.0-Setup.exe`와 필요 시 같은 폴더의 `SHA256SUMS.txt`를 전달한다. `Release`에는 임시 시험본을 두지 않는다.

## 4. 문제 해결과 기록

### `Docs` — 현재 참고 문서

- `Architecture`: App과 Explorer 레이어가 협력하는 현재 구조
- `Architecture/LAYER_TRANSITION_FINAL_2.1.md`: 2.1 공식 후보 17 전환 방식, 실패 후보와 향후 연구 방향
- `Architecture/MAINWINDOW_REFACTOR_20260820.md`: 후속 partial 분리와 유지 원칙
- `Audits`: V1/V2 차이와 고정 모드 입력 검토 결과

## 5. 루트 문서와 빌드 명령

- `README.md`: 사용자 기능과 사용법
- `PRD.md`: 제품 요구사항
- `BUILD_AND_RELEASE.md`: 소스에서 설치 파일을 만드는 전체 과정
- `V2.1_ROADMAP.md`: 2.1 기능 목록과 진행 상태
- `V2.1_TEST_CHECKLIST.md`: 실제 화면·Explorer·Google 수동 시험 순서
- `TODO_NEXT.md`: `onharu.net`, 다국어, 자동 업데이트, 일반 공개 배포와 향후 Google Plus 전환을 위한 확정 작업 목록
- `AGENTS.md`: 코드 책임 분리, ONHARU 디자인, 검증·기록을 이후 작업에도 유지하는 프로젝트 전용 지침
- `HANDOFF.md`: 실제 문제와 해결 과정의 시간순 기록
- `DEVELOPMENT_HISTORY.md`: V1부터 이어진 제품 결정
- `build-release.ps1`: App + ExplorerLayer + Installer를 한 번에 만드는 공식 빌드 명령
- `build-local-test.ps1`: 최신 App + ExplorerLayer 로컬 시험본을 `Tests/LocalTest` 한 곳에 생성
- `check-v21.ps1`: 빌드와 자동 검사 13종을 한 번에 실행

시험 산출물은 `Tests`, 사용자 배포물은 `Release`로 분리한다. 경로를 바꿀 때는 빌드·검사 스크립트도 함께 수정해야 한다.

내부 파일명·mutex·공유 메모리·데이터 경로에 남은 `V3`는 기존 개발 코드명이다. 2.0 사용자 데이터와 네이티브 IPC 호환을 위해 2.1에서도 의도적으로 유지한다.
