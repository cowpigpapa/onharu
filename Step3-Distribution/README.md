# Step 3 — ONHARU 1.0.1 배포

이 폴더는 지인 배포용 최종 결과물과 배포 소스를 관리합니다.

## 공식 결과물

- `Release\ONHARU.exe`: 설치 없이 실행하는 단일 파일
- `Installer\Output\ONHARU-Setup-1.0.1.exe`: 권장 설치 파일과 제거 프로그램
- `Release\SHA256SUMS.txt`: 배포 파일 무결성 확인값
- `DISTRIBUTION_GUIDE.md`: Google OAuth와 배포 전 사용자 작업

## 폴더 구조

- `Source`: ONHARU 1.0.1 최종 C# WPF 소스와 자동검사
- `Release`: 최종 실행 파일만 보관
- `Installer`: Inno Setup 설정과 설치 결과물
- `Archive`: 개발 중 생성한 이전 시험판 보관

## 빌드

PowerShell에서 다음을 실행합니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Step3-Distribution\build-release.ps1
```

이 스크립트는 EXE 빌드, 버전·OAuth·반복·로그·내보내기·연속 일정 검사, 설치 설정 검사, 설치 파일 생성과 SHA-256 계산을 순서대로 수행합니다.

설치 제거 시 `%LOCALAPPDATA%\FamilyPlanner`의 일정과 설정은 자동 삭제하지 않습니다. 재설치 복구와 실수로 인한 데이터 손실을 막기 위한 정책입니다.
