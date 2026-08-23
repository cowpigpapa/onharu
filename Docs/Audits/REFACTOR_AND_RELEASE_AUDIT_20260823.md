# ONHARU 2.2 오늘 변경분 리팩토링·배포 감사

## 판정

추가 대형 분리는 필요하지 않다. 오늘 변경은 아래 공통 경계로 정리됐다.

- `OnharuColorPresets`: 추천 색상 원본과 기본 카테고리색
- `CategoryColorSystem`: 일정 배경·글자·체크박스 대비 계산
- `OnharuStateColors`: 오른쪽 선택 상태색
- `OnharuSegmentedSwitch`: 상호배타 보기·정렬 버튼
- `OnharuPopupChrome`: 부가기능 제목·대표 버튼·닫기·이동·스크롤 규칙
- `PlannerSettings.SportsCalendarScale`: KBO 보기 크기 저장

`SettingsWindow.cs`와 `AddItemWindow.cs`는 줄 수가 크지만 대부분 한 화면의 절차적 WPF 구성이다. 지금 partial이나 새 서비스로 나누면 파일 수만 늘어나므로 보류한다. 다음 분리는 독립된 비즈니스 규칙이나 재사용 화면 경계가 실제로 생길 때만 수행한다.

## 정리한 중복·오류

- 스킨과 무관한 `OnharuColorPresets.Palettes`의 불필요한 인수를 제거했다.
- 시간표에 중복으로 남아 있던 전용 그라데이션·제목 브러시를 공통 팝업 스타일로 교체했다.
- 시간표·일기장·KBO 제목과 대표 버튼을 공통 스타일로 통일했다.
- 일기장 정렬·보기 버튼을 공통 슬라이딩 버튼으로 교체했다.
- 메인과 KBO 날짜 이동 버튼 규격을 통일했다.
- 메인 부가기능 아이콘과 팝업 제목 아이콘의 기호를 통일하고 메인 아이콘만 21px로 확대했다.
- `finalize-distribution.ps1`에 남아 있던 2.1 파일명을 2.2로 교정했다.

## 검증

- `check-v22.ps1` 17개 품질 게이트 통과
- 공개 소스에서 로컬 OAuth 자격 파일과 실제 Client Secret 제외 확인
- 설치판은 Inno Setup의 기존 AppId를 유지해 2.1 위에 덮어쓰기 가능
