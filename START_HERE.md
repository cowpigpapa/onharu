# ONHARU 작업 시작 문서

이 문서는 Codex, Hermes, Telegram 작업과 새 채팅이 ONHARU를 수정하기 전에 가장 먼저 읽는 기준 문서다.

## 1. 반드시 읽는 순서

1. `START_HERE.md` — 작업 규칙과 문서 체계
2. `CURRENT_STATUS.md` — 현재 버전, 배포 상태와 검증 결과
3. `AGENTS.md`(Codex) 또는 `CLAUDE.md`(Claude) — 코드·디자인·검증 규칙. 두 파일의 공통 규칙 본문은 같게 유지한다.
4. `HANDOFF.md` 맨 위의 최신 기록 — 다른 채팅과 작업자의 최근 변경
5. 작업에 해당하는 기준 문서
   - 기능과 사용자 동작: `README.md`, `PRD.md`, `TODO_NEXT.md`
   - 구조: `PROJECT_INDEX.md`, `Docs/Architecture`
   - 빌드·배포: `BUILD_AND_RELEASE.md`, `WEB_OPERATIONS_HANDOFF.md`

문서가 서로 다르면 **사용자가 확인한 최신 실기기 결과 → `CURRENT_STATUS.md` → `HANDOFF.md` 최신 기록 → 현재 소스와 자동검사 → 계획·역사 문서** 순으로 판단한다.

## 2. 문서 동기화는 작업 완료 조건이다

코드나 사용자 동작을 변경하고 문서를 갱신하지 않은 작업은 완료로 보지 않는다. 매 작업에서 다음을 함께 처리한다.

- 모든 파일 변경: `HANDOFF.md` 맨 위에 요청, 변경 파일, 내용, 검사 결과, 미해결 사항과 다음 작업을 기록한다.
- 작업 규칙·지침 변경: `AGENTS.md`와 `CLAUDE.md`를 같은 작업에서 같은 문장으로 갱신한다. 한쪽만 갱신한 작업은 완료로 보지 않는다.
- 현재 버전·배포·실기기 결과 변경: `CURRENT_STATUS.md`를 갱신한다.
- 사용자 기능·화면·사용법 변경: `README.md`를 갱신한다.
- 제품 요구사항·데이터·동기화 규칙 변경: `PRD.md`를 갱신한다.
- 남은 일의 완료·추가·보류: `TODO_NEXT.md` 체크 상태를 갱신한다.
- 파일 구조·책임 경계 변경: `PROJECT_INDEX.md`와 해당 `Docs/Architecture`를 갱신한다.
- 버전·빌드·설치·업데이트·배포 변경: `BUILD_AND_RELEASE.md`, `WEB_OPERATIONS_HANDOFF.md`와 관련 릴리스 문서를 갱신한다.
- 작업 원칙·현재 버전·배포 상태·핵심 구조가 바뀌면 옵시디언 `D:\Desktop\AI Project\Obsidian Vault\10-Projects\ONHARU`의 `ONHARU 작업 원칙.md`, `ONHARU 프로젝트 현황.md`, `ONHARU 개발 기록 인덱스.md` 중 해당 문서를 같은 작업에서 갱신한다.
- 버전을 올리면 Assembly, 화면 표기, 설치 스크립트, 파일명, README, CURRENT_STATUS, 다운로드/릴리스 문서의 버전을 한 작업에서 맞춘다.

과거 `HANDOFF.md`, `DEVELOPMENT_HISTORY.md`, 버전별 릴리스 노트는 기록이므로 당시 내용을 최신 버전으로 일괄 치환하지 않는다.
옵시디언은 요약·회고용 거울이며 개발 원본은 이 프로젝트다. 내용이 충돌하면 위 판단 우선순위를 따르고 옵시디언의 오래된 요약을 바로잡는다.

## 3. Git 운영

- 이 프로젝트 루트의 Git은 최신 개발 소스와 문서의 로컬 기준 이력이다.
- `App/OAuthCredentials.local.cs`, 사용자 데이터, 실행 파일, 설치 파일, `Release`, `Publish`는 추적하지 않는다.
- 공개 GitHub 업로드는 사용자가 명시적으로 요청한 정식 배포 때만 별도 공개 스테이지에서 수행한다.
- 작업 시작 시 `git status`, 종료 전 `git diff`, 관련 자동검사를 확인한다.
- 다른 작업자의 변경을 임의로 되돌리거나 과거 시험 코드를 공식 경로에 복구하지 않는다.

## 4. 현재 제품의 핵심 보호 대상

- Explorer 아이콘 아래 고정 레이어와 이동·고정 전환 기준선
- 로컬·Google 계정 데이터 분리, 원자 저장과 백업
- Google Calendar/Tasks 권한과 오프라인 동기화 경계
- 기존 설정과 일정 데이터의 하위 호환성
- 팝업 정책, 파스텔·블랙 디자인과 접근성 대비
- 사용자 승인 없는 공개 업로드·배포 금지
