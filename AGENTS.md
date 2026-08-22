# ONHARU project instructions

이 파일은 `Onharu_v2.1` 전체 작업에 적용한다.

## 제품 기준

- ONHARU의 기존 기능, 사용자 데이터, Explorer 아이콘 아래 레이어 안정성을 보존한다.
- 디자인과 기능 개선은 계속하되 승인된 레이어 전환 기준선을 임의로 변경하지 않는다.
- 실험적인 레이어 코드는 정식 소스에 바로 넣지 말고 `Tests/LocalTest/TransitionCandidates`의 별도 시험본으로 검증한다.

## 코드 책임 분리

- `MainWindow.cs`에는 공유 필드, 생성자 골격, 최상위 조립만 둔다.
- 새 기능을 `MainWindow.cs` 또는 하나의 대형 파일에 몰아넣지 않는다.
- MainWindow 기능은 기존 partial 책임에 배치하고 맞는 경계가 없을 때만 명확한 새 partial을 만든다.
- UI 구성은 `MainWindow.Layout.cs`, 시작·종료 수명주기와 데이터 보정은 `MainWindow.Startup.cs`, 팝업 배치는 `MainWindow.Dialogs.cs`, 위치 상태는 `MainWindow.PositionMode.cs`, 색상·크기 도우미는 `MainWindow.Theme.cs`에서 유지한다.
- Google, ExplorerLayer, 반복 일정, 저장·마이그레이션, 시간표, 향후 Localization·Updates·Licensing은 서로 독립된 서비스나 모듈로 유지한다.
- 기존 파일을 분리할 때 먼저 기능 변화 없는 기계적 이동을 하고 빌드·품질 게이트를 통과한 뒤 로직 개선을 별도 단계로 수행한다.
- 한국어 표시 문자열을 영구 데이터 식별자로 새로 추가하지 않는다. 향후 다국어화를 위해 내부 ID와 표시 문자열을 분리한다.

## ONHARU 디자인 원칙

- 새 화면·팝업·버튼은 반드시 기존 ONHARU 스타일과 조화를 이루게 만든다.
- 파스텔 색상, 둥근 모서리, 얇은 테두리, 공통 버튼 반응, 정돈된 여백을 기본으로 사용한다.
- 팝업은 가능하면 `OnharuPopupChrome`과 기존 공통 스타일을 재사용한다.
- 같은 역할의 버튼은 높이·모서리·내부 여백을 통일한다.
- 좌우 끝선, 라벨과 입력칸의 시작선, 행 안의 세로 중앙 정렬을 확인한다.
- 과한 공백을 피하고 섹션 간격과 행 간격을 일관되게 유지한다.
- 작은 노트북 해상도와 한국어·영어의 문자열 길이를 고려해 잘림과 불필요한 스크롤을 확인한다.
- 디자인 판단이 제품 경험을 크게 바꾸거나 기존 화면과 충돌하면 임의 확정하지 말고 사용자에게 한 가지씩 확인한다.

## 검증과 기록

- 코드 변경 후 관련 자동 검사와 `check-v21.ps1` 품질 게이트를 실행한다.
- Explorer 레이어, 이동·고정, 입력, DPI처럼 자동화가 어려운 변경은 수동 시험 항목도 남긴다.
- 변경 파일과 이유, 검증 결과를 `HANDOFF.md`에 기록한다.
- 장기 제품 방향은 `TODO_NEXT.md`, 빌드·배포 방식은 `BUILD_AND_RELEASE.md`, 구조 설명은 `Docs/Architecture`와 일치시킨다.
- 시험 실행 파일은 `Tests`, 정식 배포 파일은 `Release`에만 둔다.

