# ONHARU 2.1 · Rainlendar / 데이터 교환 / Google Tasks 검토

검토일: 2026-08-22  
기준: ONHARU 현재 소스와 공식·원문 자료

## 1. 결론

- 일기장은 1차 목표를 완료했다. 작성·수정, 날짜 진입, 검색 가능한 리더, 정렬, 이전·다음, 설정 전체 토글, 로컬 저장·백업까지 구현됐다.
- Rainlendar에서 ONHARU에 우선 추가할 가치는 **ICS 교환**, **빠른 일정 추가**, **일정 템플릿**, **모니터 연결 변경 안정성** 순이다.
- JSON·ICS·CSV는 하나를 고르는 관계가 아니다. JSON은 ONHARU 복원, ICS는 다른 달력과 교환, CSV는 Excel·분석용이다.
- 설정 화면은 `가져오기 / 내보내기` 중심으로 단순화하되 내보내기 팝업 안에서 `ONHARU JSON / 달력 ICS / Excel CSV`를 고르는 구성이 가장 명확하다.
- Google Calendar 일정과 Google Tasks는 별도 API의 별도 데이터다. 화면에서는 둘 다 완료 체크를 제공할 수 있지만, Calendar 일정 완료는 ONHARU 로컬에만 저장하고 실제 Google Task 완료만 Tasks API로 전송해야 한다.

## 2. Rainlendar 비교

Rainlendar 2.24 계열에서 확인되는 강점은 스킨·다국어, 이벤트와 Task 분리, 알림, ICS 기반 교환, 인쇄, 검색, 모니터 연결 변경 대응, Microsoft To Do·Office 365 연동이다. 최근 릴리스에는 대형 스킨 렌더링 개선, 다크 모드, 추가 모니터 연결·해제 위치 처리 개선, HTML 기반 인쇄, Microsoft To Do 지원 등이 포함됐다.

근거: [Rainlendar 공식 리소스 릴리스](https://github.com/rainlendar/Rainlendar-Resources/releases), [Rainlendar 구 매뉴얼](https://www.ipi.fi/~rainy/Rainlendar/Manual.html)

### ONHARU가 이미 더 강한 부분

- Windows 배경화면과 아이콘 사이 Explorer 레이어
- 이동 가능·고정 전환과 아이콘 입력 보존
- 한국형 음력·24절기·주차·쉬는 날
- Google Calendar와 로컬 일정의 출처 분리
- D-Day·기념일·중요한 날 카드
- 1~6주 범위와 이번 주 위치 설정
- 시간표와 로컬 일기장
- ONHARU 스타일의 일관된 등록·검색·백업 UI

### 추가 가치가 큰 기능

1. **ICS 가져오기·내보내기**: 다른 달력 제품과의 이사·공유에 가장 직접적이다.
2. **빠른 일정 추가**: 전역 단축키보다 먼저, 달력 상단의 작은 빠른 입력 또는 트레이 메뉴로 시작한다.
3. **일정 템플릿**: 자주 쓰는 회의·병원·수업 일정의 제목, 시간, 카테고리, 알림을 재사용한다.
4. **모니터 연결 변경 안정성**: 현재 PMv2 개선 이후 혼합 DPI 실제 장비 검증을 계속한다.
5. **다국어·자동 업데이트**: 이미 장기 공개 배포 계획의 핵심이며 Rainlendar와 비교해 가장 큰 배포 격차다.

### 우선순위가 낮은 기능

- 자유 스킨 엔진: ONHARU 디자인 일관성과 충돌하고 유지비가 크다.
- Lua 플러그인: 일반 사용자 제품 목표보다 복잡성이 크다.
- Office 365·Microsoft To Do: Google Tasks 안정화와 일반 공개가 끝난 뒤 검토한다.
- 바탕화면 사진 배경: Windows 배경화면과 역할이 겹친다.

## 3. JSON / ICS / CSV 역할

| 형식 | 목적 | 보존 범위 | ONHARU 정책 |
|---|---|---|---|
| ONHARU JSON | 백업·복원·PC 이전 | 로컬 전용 필드, D-Day, 기념일, 반복, 알림 등 가장 완전함 | 계속 기본 백업 형식으로 유지 |
| ICS | Google·Outlook·Apple·Rainlendar 등과 달력 교환 | 표준 `VEVENT`, 일부 `VTODO`; ONHARU 전용 표현은 제한됨 | 가져오기·내보내기 추가 |
| CSV | Excel 열람·정리·감사 | 평면 표; 반복·예외·알림의 완전 복원에는 부적합 | 복원용이 아닌 보고서용으로 유지 |

iCalendar는 서비스와 무관하게 이벤트·할 일·일기 정보를 교환하는 표준이며 `VEVENT`, `VTODO`, `VJOURNAL`을 정의한다. 여러 날 하루 종일 일정의 `DTEND`는 마지막 날 다음 날인 비포함 종료일이다. 근거: [RFC 5545](https://www.rfc-editor.org/info/rfc5545/)

### 권장 화면 구조

- `일정 가져오기`: `.json`, `.ics`를 선택하고 미리보기에서 신규·변경 항목을 체크한다.
- `일정 내보내기`: 팝업에서 `ONHARU 백업(JSON)`, `달력 교환(ICS)`, `Excel 표(CSV)`를 선택한다.
- `백업 복원`: 자동 생성된 ONHARU JSON 백업만 취급한다.
- Google 원본 일정은 JSON 복원 대상에서 계속 제외한다.

따라서 Excel 아이콘 하나를 없애는 것은 가능하지만 CSV 기능 자체는 유지한다. 별도 아이콘 대신 하나의 내보내기 팝업 안으로 이동하는 것이 적절하다.

## 4. Google Calendar 일정과 Google Tasks

Google Calendar API의 이벤트는 `default`, `birthday`, `fromGmail`, `focusTime`, `outOfOffice`, `workingLocation` 등의 `eventType`으로 구분된다. Google Tasks는 Calendar 이벤트가 아니라 별도 Tasks API의 Task이며 `tasklist`, `status`, `completed`, `due` 필드를 사용한다.

근거: [Google Calendar 이벤트 유형](https://developers.google.com/workspace/calendar/api/guides/event-types), [Google Tasks REST API](https://developers.google.com/workspace/tasks/reference/rest), [Tasks 리소스](https://developers.google.com/tasks/reference/rest/v1/tasks), [tasks.patch](https://developers.google.com/workspace/tasks/reference/rest/v1/tasks/patch)

| 항목 | 화면 완료 체크 | 완료 저장 위치 | Google 전송 |
|---|---:|---|---:|
| ONHARU 로컬 일정 | 가능 | ONHARU 로컬 JSON | 없음 |
| Google Calendar 일반 시간 일정 | 가능 | ONHARU 로컬 메타데이터 | 완료 여부는 전송하지 않음 |
| Google Task | 가능 | ONHARU 캐시 + Google Task 상태 | Tasks API로 `needsAction/completed` 전송 |
| Google 특수 이벤트 | 기본적으로 불가 | Google 원본 | 읽기 전용 |

Google Tasks API의 `due`는 날짜만 보존하고 시간 부분은 API에서 버려진다. 따라서 1차 연동에서는 날짜가 있는 Task를 하루 종일 항목으로 표시하고, 완료 상태만 양방향 동기화한다. Task 목록 전체 편집·반복·시간 편집은 별도 후속 범위다.

## 5. 구현 순서

1. Google 일반 일정 완료 상태의 Calendar API 전송 중단
2. `GoogleTasksService`를 Calendar 서비스와 분리
3. OAuth에 Tasks 범위 추가 후 계정 재연결 안내
4. 날짜가 있는 Task 목록·완료 상태 동기화
5. Calendar의 특수 `eventType`을 읽기 전용으로 명확히 표시
6. JSON/ICS/CSV 통합 가져오기·내보내기 팝업

