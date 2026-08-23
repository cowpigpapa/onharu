# ONHARU 2.1 내부 구조

> `V3`는 V1 기반 재구현 당시 사용한 개발 코드명이며, 현재 개발 제품 버전은 ONHARU 2.1이다. 기존 2.0 사용자 데이터와 Explorer IPC 호환을 위해 일부 내부 식별자에만 `V3`가 남아 있다.

## 기준

- `App`: V1 `Step3-Distribution/Source` 완성본을 그대로 복제한 기능·디자인 기준선
- `ExplorerLayer`: 구형 V2 프로토타입에서 시작해 최종 안정화한 `SysListView32` custom-draw 엔진
- `FamilyPlanner`의 V1 최종 소스는 기능·디자인 비교 기준으로 보존한다.

## 저장 격리

- 프로세스 mutex: `Local\\Onharu.SingleInstance`
- 데이터 mutex: `Local\\Onharu.DataFileLock`
- 사용자 데이터와 백업: `%LOCALAPPDATA%\\Onharu`
- 최초 실행 때 `%LOCALAPPDATA%\\FamilyPlanner`를 읽기 전용 원본으로 한 번 복사한다.
- V1 위치·크기·디자인 설정과 일정, 백업, Google 토큰은 2.0부터 분리된 내부 저장소 복사본에서만 이후 수정된다.

## 레이어 전환 목표

1. 이동 가능: V1 WPF `MainWindow`를 그대로 표시하고 V1의 이동·리사이즈 동작을 사용한다.
2. 고정: 같은 WPF visual을 32-bit premultiplied BGRA 프레임으로 게시한다.
3. Explorer hook은 `NM_CUSTOMDRAW/CDDS_PREPAINT`에서 프레임을 합성한 뒤 아이콘을 그리게 한다.
4. 고정 화면의 입력은 좌표 재구현보다 WPF hit-test 결과를 이용하는 방향으로 제한한다.
5. Explorer 재시작 시 LayerHost가 다시 hook을 연결한다.

## 현재 상태

- V1 앱 복제, 2.0 저장 격리, 2.1 전진 호환 마이그레이션, App/Native 레이어 독립 빌드를 완료했다.
- WPF Visual을 Pbgra32 공유 프레임으로 게시하고 이동/고정 모드를 전환하는 1차 연결을 완료했다.
- 실제 화면 표시 확인 뒤 고정 레이어 입력 라우팅을 연결한다.
