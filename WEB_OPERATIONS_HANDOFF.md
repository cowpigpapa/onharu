# ONHARU 웹 운영 인수인계

최종 갱신: 2026-08-27

## 운영 환경

- 서비스 주소: `https://onharu.app`
- AWS 서버: `13.125.205.54`
- SSH 사용자: `ubuntu`
- SSH 개인 키: `D:\Downloads\AWS_connectKey.pem`
- 웹 서버: Nginx
- 문서 루트: `/var/www/html`
- 다운로드 폴더: `/var/www/html/downloads`
- 서버 백업 폴더: `/home/ubuntu/onharu-web-backups`
- 프로젝트: `D:\Desktop\AI Project\Desktop Diarys\Onharu_v2.2`
- 로컬 웹 배포본: `D:\Desktop\AI Project\Desktop Diarys\Onharu_v2.2\Publish\ONHARU-Web`

SSH 키 내용이나 다른 인증 비밀값은 이 문서에 기록하지 않는다. 키 파일은 외부 전송·메일 첨부·Git 커밋을 금지한다.

## 접속

```powershell
ssh -i "D:\Downloads\AWS_connectKey.pem" ubuntu@13.125.205.54
```

파일 게시 예시:

```powershell
scp -i "D:\Downloads\AWS_connectKey.pem" "게시할 파일" ubuntu@13.125.205.54:/var/www/html/
```

다운로드 자산 게시 예시:

```powershell
scp -i "D:\Downloads\AWS_connectKey.pem" "설치 파일" ubuntu@13.125.205.54:/var/www/html/downloads/
```

## 현재 공개 페이지

- `index.html`
- `ONHARU-FEATURES.html`
- `ONHARU-PLUS.html`
- `ONHARU-DOWNLOAD.html`
- `ONHARU-GUIDE.html`
- `ONHARU-FAQ.html`
- `ONHARU-RELEASES.html`
- `ONHARU-SUPPORT.html`
- `ONHARU-TERMS.html`
- `ONHARU-PRIVACY.html`

헤더는 `소개 · 다운로드 · 설명서 · FAQ · 릴리스 노트 · 지원·문의`로 통일한다. Free · Plus 비교는 소개 페이지의 `#free-plus` 구역에 통합되어 있다. 푸터는 `이용약관 · 개인정보처리방침`과 저작권만 유지한다. 모바일 헤더 메뉴는 가로 스크롤 방식이다.

## 현재 릴리스

- 공개 버전: `2.2.4`
- 공식 릴리스: `https://github.com/cowpigpapa/onharu/releases/tag/v2.2.4`
- 설치판: `ONHARU-2.2.4-Setup.exe` / 2,388,167 bytes
- 설치판 SHA-256: `A488994AD1579D69E10B738623C2DA5D2ADC428AFEB217BBF505E564B77C6CF6`
- 포터블판: `ONHARU-2.2.4-Portable.zip` / 460,802 bytes
- 포터블판 SHA-256: `CFD51D4C62FB238BF060704E57ADD36334A8ECB4B4917B8A95032E731DB22C65`
- 지원 이메일: `support@onharu.app`

2.1·2.2.0 사용자는 자동 업데이트 통신 보완을 위해 2.2.1을 한 번 직접 설치해야 한다. 이후 자동 업데이트는 정상 작동한다.

## 게시 절차

1. `AGENTS.md`와 `HANDOFF.md`를 먼저 읽는다.
2. 로컬 웹 배포본을 수정하고 10개 페이지의 공통 헤더·푸터가 유지되는지 확인한다.
3. 앱 변경이 포함되면 프로젝트 루트에서 `pwsh -NoProfile -File ./check-v22.ps1`을 실행해 17개 품질 게이트를 통과시킨다.
4. 서버 게시 전에 `/var/www/html` 전체를 `/home/ubuntu/onharu-web-backups`에 타임스탬프가 포함된 `.tar.gz`로 백업한다.
5. 웹 문서와 다운로드 자산을 각각 게시한다. 이전 릴리스 파일은 직접 링크 호환을 위해 임의 삭제하지 않는다.
6. 서버의 `sha256sum`과 `stat`으로 게시 파일을 확인한다.
7. 외부에서 10개 페이지 HTTP 200, 버전 문구, 다운로드 링크·크기·SHA-256을 확인한다.
8. 결과와 백업 파일명을 `HANDOFF.md`에 기록한다.

## 최근 백업과 상태

- 2.2.1 게시 전 백업: `/home/ubuntu/onharu-web-backups/site-before-2.2.1-20260824-001401.tar.gz`
- 2026-08-24 기준 9개 공개 페이지 모두 HTTP 200 확인
- 2.2.1 설치판·포터블판의 서버 해시와 공개 파일 크기 확인 완료
- Nginx 서비스 정상
- Free·Plus 안내 게시 전 백업: `/home/ubuntu/onharu-web-backups/site-before-plus-20260824-004517.tar.gz`
- 2026-08-24 `ONHARU-PLUS.html`을 추가하고 기존 9개 페이지 헤더에 `Free · Plus` 링크를 통일했다.
- 공개 10개 페이지 HTTP 200, 공통 Plus 링크와 viewport 선언을 확인했다. 390px 모바일 폭에서 본문 가로 넘침 0, 2열 비교 카드의 1열 전환, 헤더 가로 스크롤 동작을 확인했다.
- 2026-08-25 2.2.2 수정 설치판과 포터블을 게시했다. 게시 전 백업은 `/home/ubuntu/onharu-web-backups/site-before-2.2.2-republish-20260825-002651.tar.gz`이다.
- onharu.app에서 다시 다운로드한 Setup·Portable의 SHA-256이 로컬 원본과 일치함을 확인했다.
- 2026-08-26 2.2.3 설치판·포터블·다운로드 페이지·릴리스 노트를 게시했다. 게시 전 백업은 `/home/ubuntu/onharu-web-backups/site-before-2.2.3-20260826-005922.tar.gz`이다.
- 공개 10개 페이지 HTTP 200과 onharu.app에서 재다운로드한 Setup·Portable의 SHA-256 일치를 확인했다.
- 2026-08-27 2.2.4 설치판·포터블·다운로드 페이지·릴리스 노트를 게시했다. 게시 전 백업은 `/home/ubuntu/onharu-web-backups/site-before-2.2.4-20260826-213048.tar.gz`이다.
- 공개 10개 페이지 HTTP 200, Nginx active, onharu.app에서 재다운로드한 Setup·Portable의 SHA-256 일치를 확인했다.

## 운영 주의사항

- 게시 전 백업을 생략하지 않는다.
- PEM 파일을 웹 루트, 프로젝트, GitHub 또는 이메일에 올리지 않는다.
- 서버 접속이 실패하면 키를 다시 만들기 전에 IP, 사용자명, 키 경로, AWS 보안 그룹의 SSH 허용 여부부터 확인한다.
- 화면이 이전 내용이면 먼저 `Ctrl + F5`로 캐시를 갱신한다.
- 웹 장애 시 `systemctl is-active nginx`와 Nginx 로그를 먼저 확인하고, 원인을 확인하지 않은 채 설정을 덮어쓰지 않는다.
