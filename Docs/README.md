# 기술 문서 안내

현재 개발 기준은 ONHARU 2.2다. 디자인 스킨 구조는 `Architecture/THEME_SYSTEM_2.2.md`를 먼저 확인하며, 2.1 문서는 Explorer 레이어와 기존 기능의 역사·근거로 보존한다.

- `Architecture/THEME_SYSTEM_2.2.md`: 3종 스킨 팔레트, 안정성 경계와 확장 규칙
- `Design/COLOR_PALETTE_STANDARD.md`: 2.1 기반 추천색, 카테고리 의미색, 스킨별 톤과 색상 검증 표준

- `Architecture/INTERNAL_ARCHITECTURE.md`: App, 공유 프레임, Explorer 훅의 역할
- `Audits/EXPLORER_LAYER_INTERACTION_AND_FUNCTION_AUDIT.md`: 고정 레이어 입력과 기능 감사
- `Audits/V1_V2_FIXED_MODE_DIFFERENCE_AUDIT.md`: 실제 WPF 이동 모드와 Explorer 고정 모드 차이
- `Audits/TEST_REVIEW_AND_ROADMAP_20260821.md`: 2026-08-21 사용자 테스트 정리, 즉시 수정, 후속 로드맵, Rainlendar 비교
- `Audits/RAINLENDAR_DATA_EXCHANGE_GOOGLE_TASKS_REVIEW_20260822.md`: Rainlendar 기능, JSON·ICS·CSV 역할, Google Tasks 분리 검토

구형 프로토타입과 외부 AI 검토 문서는 프로젝트 루트가 아니라 `../Archive`에 보관한다.
