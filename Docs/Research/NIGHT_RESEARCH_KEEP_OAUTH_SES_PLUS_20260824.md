# ONHARU 야간 조사 — Keep·OAuth·SES·Free/Plus

작성일: 2026-08-24  
범위: 구현 작업이 아닌 제품·배포 가능성 조사

## 1. 결론

- Google Keep 직접 연동은 제품 범위에서 제외한다. 공식 API는 일반 개인 Gmail 앱용이 아니라 Google Workspace 관리자와 도메인 전체 위임을 전제로 하며, 기존 메모 수정 API도 없어 정상적인 양방향 동기화를 만들 수 없다.
- Google OAuth 공개 검증은 현재 사용하는 Calendar와 Tasks 범위를 실제 기능으로 충분히 설명할 수 있다. 다만 홈페이지·개인정보처리방침·서비스 약관·로고·시연 영상이 최종 상태가 된 뒤 제출한다.
- 메일 백업은 공개 EXE에 AWS 자격증명을 넣는 기능이 아니라, 인증·사용량 제한·반송 감시를 갖춘 작은 공개 백엔드 서비스다. 현재 Lambda/API Gateway/SES 구조가 맞다.
- 한국어·영어 다국어는 2.2 안정화와 OAuth 제출 이후 `2.3`의 대표 작업으로 진행한다.
- Plus의 핵심 가치는 Google Calendar 연결과 양방향 동기화로 한정한다. 로컬 기능과 부가 기능은 Free로 유지한다.
- KBO 상태 데이터 검증은 공급 API가 제공할 때만 재개하고, 혼합 DPI는 사용자의 실제 장비 시험 결과가 있을 때 재개한다.

## 2. Google Keep 공식 연동 가능성

### 판정: 직접 연동 제외

Google Keep REST API는 존재하지만 일반 소비자 Gmail 계정용 공개 앱 API가 아니다. Google Workspace 조직에서 관리자가 도메인 전체 위임을 승인하는 방식이 중심이므로 개인 `@gmail.com` 사용자는 같은 흐름을 사용할 수 없다. 또한 Notes API에는 생성·조회·목록·삭제는 있지만 기존 메모 내용을 갱신하는 일반 update/patch가 없다. 색상·라벨·리마인더 같은 소비자 Keep 기능도 충분히 노출되지 않는다.

따라서 ONHARU는 다음 경계를 유지한다.

- 메모·일기: ONHARU 로컬 기능
- 일정: Google Calendar
- 체크리스트·할 일: 선택형 Google Tasks
- 외부 전달: TXT/Markdown/클립보드 내보내기를 필요할 때 검토

근거: [Google Keep API 안내](https://developers.google.com/workspace/keep/api/guides), [Notes REST 참조](https://developers.google.com/workspace/keep/api/reference/rest/v1/notes), [Google Tasks 개요](https://developers.google.com/workspace/tasks/overview)

## 3. Google OAuth 공개 검증

### 현재 범위

- `calendar.events`: 접근 가능한 캘린더의 일정 조회·수정
- `calendar.calendarlist.readonly`: 사용자가 구독한 캘린더 목록과 선택 UI
- `tasks`: 선택형 Google Tasks 생성·수정·완료·삭제

현재 기능을 유지한다면 위 범위는 설명 가능하다. Tasks 범위는 사용자가 Google Tasks 기능을 켤 때만 요청하는 증분 동의 방식이 가장 좋다. Google Cloud Console의 Data Access 분류가 최종 기준이며, Restricted 범위로 표시되면 별도 보안 평가 비용과 절차가 추가되므로 제출 전에 반드시 확인한다.

### 제출 순서

1. 개발·시험 GCP 프로젝트와 공개 배포 프로젝트를 분리한다.
2. 앱 이름, 로고, 지원 이메일, 개발자 연락처를 확정한다.
3. 로그인 없이 접근 가능한 홈페이지, 개인정보처리방침, 서비스 약관을 최종 게시한다.
4. Search Console DNS Domain 속성으로 루트 도메인과 Authorized domains를 검증한다.
5. 앱이 실제 요청하는 범위와 Data Access 등록 범위를 1:1로 맞춘다.
6. 각 범위가 필요한 이유와 더 좁은 범위로 불가능한 이유를 작성한다.
7. 실제 배포본으로 영문 동의 화면부터 Calendar/Tasks 생성·수정·완료·삭제까지 시연 영상을 만든다.
8. 앱을 In production으로 게시하고 Prepare for Verification에서 제출한다.

검증 전 공개 앱은 미검증 경고가 나타나고 프로젝트 전체 신규 사용자 수에 제한이 생길 수 있다. Testing 상태의 토큰은 조건에 따라 7일 만료 문제가 생길 수 있으므로 실제 공개 시험은 Production 상태에서 한다.

근거: [OAuth 앱 검증 개요](https://support.google.com/cloud/answer/13464321?hl=en), [브랜딩 검증](https://support.google.com/cloud/answer/15549135?hl=en), [Calendar 인증 범위](https://developers.google.com/workspace/calendar/api/auth), [Tasks 인증](https://developers.google.com/workspace/tasks/auth), [OAuth 정책 준수](https://developers.google.com/identity/protocols/oauth2/production-readiness/policy-compliance)

## 4. 메일 백업 공개 사용자 보안과 SES 운영 전환

### 의미

SES 운영 전환은 단순히 Sandbox 해제를 뜻하지 않는다. 불특정 사용자가 ONHARU를 설치해도 서버가 오픈 릴레이·스팸 발송기·비용 폭탄이 되지 않도록 공개 서비스 경계를 완성하는 일이다.

### 확정 구조

`ONHARU → HTTPS API Gateway → Google ID Token 검증 → Lambda → SES`

- AWS 키, SES SMTP 암호, Google 비밀값은 EXE에 넣지 않는다.
- 서버가 Google ID token의 서명, `aud`, `iss`, 만료, `email_verified`, `sub`를 검증한다.
- 수신 주소는 클라이언트 입력을 믿지 않고 검증된 Google 계정 이메일로 서버가 확정한다.
- 발신자는 `ONHARU <noreply@onharu.app>`로 고정하고 CC/BCC/임의 Reply-To를 받지 않는다.
- 사용자별 원자적 사용량 제한을 적용한다. Free 기준 수동 발송 1일 1회, 7일 최대 3회 정도가 적절하다.
- 첨부 크기·형식·요청 중복 키를 검증하고 WAF, Lambda reserved concurrency, AWS Budget 경보를 둔다.
- 첨부 본문·일정 제목·메일 주소·토큰을 로그나 DB에 저장하지 않는다. 로그는 해시 사용자 ID, 크기, 상태, 요청 ID만 남긴다.
- SES Configuration Set으로 delivery, reject, bounce, complaint, rendering failure 이벤트를 수집한다.
- Bounce와 Complaint suppression을 켜고 반송률 5%, 불만률 0.1% 수준에서 조기 경보한다.
- 메일 장애 시에도 PC 로컬 백업과 직접 내보내기는 항상 가능해야 한다.

개인정보처리방침에는 첨부가 ONHARU 중계 서버와 AWS, 수신 메일 사업자를 통과한다는 점, 별도 영구 보관하지 않는다는 점, 보관·삭제 경계를 정확히 적는다.

근거: [SES Production access](https://docs.aws.amazon.com/ses/latest/dg/request-production-access.html), [SES 권한 제어](https://docs.aws.amazon.com/ses/latest/dg/control-user-access.html), [Google ID token 서버 검증](https://developers.google.com/identity/sign-in/web/backend-auth), [SES suppression list](https://docs.aws.amazon.com/ses/latest/dg/sending-email-suppression-list.html), [SES 평판 경보](https://docs.aws.amazon.com/ses/latest/dg/reputationdashboard-cloudwatch-alarm.html)

## 5. Free와 Plus 경계

### Free

- 로컬 달력과 업무·개인 일정
- 기념일, D-Day, 일기, 시간표, KBO BYOK
- 검색, 알림, Todo, 반복 일정
- PC 백업·복원, JSON·ICS·CSV 가져오기·내보내기
- 제한된 수동 메일 백업
- 스킨, 다국어, 자동 업데이트
- 선택형 Google Tasks는 당분간 Free 부가 연동으로 유지하되 Calendar와 별도의 증분 OAuth로 분리

### Plus

- Google Calendar 계정 연결
- 초기 가져오기와 양방향 생성·수정·삭제 동기화
- 수동·자동·백그라운드 동기화
- 여러 Google Calendar 선택
- 충돌 처리와 미동기화 큐 복구

수동 Google Calendar 동기화를 Free로 남기면 Plus 가치가 모호해지므로 Calendar 연결 자체를 Plus 경계로 삼는다. Plus 만료 시 동기화만 멈추고 기존 로컬 데이터와 이전에 내려받은 일정은 계속 열람·수정·내보내기할 수 있어야 한다. 클라이언트의 단순 bool이 아니라 서버가 Google `sub` 또는 ONHARU 계정 기준으로 이용권을 검증하고, 일시적 네트워크 장애를 위한 7~14일 오프라인 유예를 둔다.

제품 문구: **“ONHARU Plus는 Google Calendar 연결과 자동 동기화를 제공합니다. Free 기능과 내 PC의 로컬 데이터는 구독 종료 후에도 유지됩니다.”**

## 6. 일정 결정

- 다국어: `2.3`에서 문자열 리소스 분리, 한국어 기본, 영어 추가 순으로 진행
- KBO 상태 검증: 공급 API가 관련 필드를 제공할 때만 재개
- 혼합 DPI: 2026-08-25 사용자 회사 장비 시험 후 필요 시 재개
- 홈페이지: 별도 작업에서 관리하며, 앱 정식 배포 직후 홈페이지 다운로드 버전·SHA-256·변경사항 갱신을 사용자에게 알림

