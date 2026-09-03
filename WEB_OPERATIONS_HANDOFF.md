# ONHARU 웹 운영 인수인계

최종 갱신: 2026-09-03

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

## 게시 기록

- 2026-09-03 **정식 2.2.5를 게시했다.** 게시 전 백업은 `/home/ubuntu/onharu-web-backups/site-before-2.2.5-release-20260903-1643.tar.gz`(18,846,980 bytes)다.
  - 올린 페이지: `index.html`, `ONHARU-FEATURES.html`, `ONHARU-DOWNLOAD.html`, `ONHARU-GUIDE.html`, `ONHARU-FAQ.html`, `ONHARU-RELEASES.html`, `ONHARU-SUPPORT.html`, `ONHARU-TERMS.html`, `ONHARU-PRIVACY.html`, `ONHARU-SITE.css`.
  - 올린 자산: `ONHARU-2.2.5-Setup.exe`(2,416,799 bytes, SHA-256 `36F59E0AA9647933E39C49CC205D9EE89C8B8774BF4940FE4B041E0F859228F4`), `ONHARU-2.2.5-Portable.zip`(489,436 bytes, SHA-256 `879A2BAE13475A41307AA26C0D6780DCA7064124DFEA6444BB00B7A80965215A`).
  - 지운 자산: `ONHARU-2.2.5-Test-Setup.exe`, `ONHARU-2.2.5-Test-Portable.zip`, `ONHARU-2.2.6-Test-Setup.exe`, `ONHARU-2.2.6-Test-Portable.zip`.
  - 확인: 서버 `sha256sum` 일치, 공개 URL 재다운로드 해시 일치, 10개 페이지 HTTP 200, 지운 네 파일 404, `systemctl is-active nginx` = active.

## 현재 릴리스

- 공개 정식 버전: `2.2.5`(2026-09-03 게시 완료). GitHub Latest는 `v2.2.5`, 홈페이지 다운로드도 2.2.5다.
- 게시와 함께 `ONHARU-2.2.5-Test-*`와 `ONHARU-2.2.6-Test-*` 네 파일을 서버에서 지웠다. 앞으로 웹 시험판은 2.2.6으로 다시 올린다.
- **이 사이트는 Cloudflare 뒤에 있다.** 파일을 지워도 캐시가 남아 한동안 200을 돌려줄 수 있다. 확인은 질의 문자열을 붙여 캐시를 우회한다.

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

- 2026-09-03 사용자 제공 기준과 로컬 파일을 대조한 뒤 커밋 `9ba28f5` 시험판을 게시했다. 기준 EXE는 735,232 bytes, 2026-09-03 01:08:23 빌드, SHA-256 `427F5F4F36435ACC9FBF12F78DF00BAAC2F5F5C50B324DDE9FCB5892E9E78105`이며 워킹트리는 게시 시작 시 깨끗했다. 설치판 SHA-256은 `21A165C4C15BCA86D8F3AD9830B07C92C709C97DBB4E884906AA6BC931F4B942`, 포터블은 `7370466D2E28428B9296B20833A537B3A3340A333C03C7A00C43D3E8A6A5E1BC`다. 백업은 `/home/ubuntu/onharu-web-backups/site-before-2.2.6-9ba28f5-20260903-0200.tar.gz`. 공개 재다운로드 해시와 다운로드 페이지의 커밋·해시·`v=20260903-1` 표시를 확인했고 Nginx는 active다.

- 2026-09-02 08:53 현재 공유 작업 트리의 C# 소스에서 시험본을 직접 재빌드해 웹 시험판을 다시 교체했다. 게시 전 백업은 `/home/ubuntu/onharu-web-backups/site-before-2.2.6-rebuilt-test-20260902-0900.tar.gz`이다. 설치판 SHA-256은 `8848BA7D13B617C2E4E1A6D037BDCF5A49ED338A44894B76151C2CE09583CDF9`, 포터블은 `456C9379BD00E01D45BC07C1D42884409B7D87B8716EEED597024E3E9A72B6F1`이다. 외부 재다운로드 해시, 다운로드 페이지의 `v=20260902-2` 캐시 키와 해시 문구, Nginx active를 확인했다. 아래 08:05 게시본은 이 빌드로 대체됐다.

- 2026-09-02 Claude Code의 마지막 시험 코드를 2.2.6 시험판으로 다시 게시했다. 게시 전 백업은 `/home/ubuntu/onharu-web-backups/site-before-2.2.6-latest-test-20260902-0805.tar.gz`이다. 설치판은 2,417,313 bytes, SHA-256 `6108BA63E52B40FF22E5FEE313F008BD808A83CB324C57CF2178091971C85691`; 포터블은 499,397 bytes, SHA-256 `C5F50E0DA9A66B1D25170B92B7B7212455C205290B2E30CD905D264618624D13`이다. 공개 재다운로드 해시 일치, 10개 페이지 HTTP 200, 다운로드 페이지의 해시·캐시 키 표시를 확인했다. 게시 전 확인에서 Nginx가 2026-09-01 06:30 UTC부터 API Gateway 호스트의 일시적 DNS 조회 실패로 중지된 상태임을 발견했다. 현재 DNS와 `nginx -t`가 정상임을 확인하고 서비스를 재시작했으며 설정 파일은 변경하지 않았다.

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
- 2026-09-01 기념일 펼침 상태의 추가 버튼 크기를 고정한 2.2.6 시험판을 다시 게시했다. 게시 전 백업은 `/home/ubuntu/onharu-web-backups/site-before-2.2.6-test-refresh-20260901-0812.tar.gz`이다. 설치판 SHA-256은 `1BE0DA5A30AF11425E43B46F2DD9156C9D22F393D8CFA3D08ED7CE84FD7EB98C`, 포터블은 `4C27F6FC563C112A3FD538A1A123DBF313FC0AA11A94C4D3021197308912BBE4`이며 공개 재다운로드 해시 일치와 10개 페이지 HTTP 200을 확인했다.
- 2026-09-01 Clay 디자인을 시간표에 한정 적용한 2.2.6 시험판을 다시 게시했다. 게시 전 백업은 `/home/ubuntu/onharu-web-backups/site-before-2.2.6-clay-timetable-20260901-1330.tar.gz`이다. 설치판 SHA-256은 `60A0D2E8A27F8BD5E50F2CE85F4E810C4F1167124591EA48E3D2E90D934E25E1`, 포터블은 `5C3EABDFE9EBA619126345973F16125E7D275DA1AADFB92143D39005D6B297D3`이며 공개 재다운로드 해시 일치와 10개 페이지 HTTP 200을 확인했다.

## 운영 주의사항

- 게시 전 백업을 생략하지 않는다.
- PEM 파일을 웹 루트, 프로젝트, GitHub 또는 이메일에 올리지 않는다.
- 서버 접속이 실패하면 키를 다시 만들기 전에 IP, 사용자명, 키 경로, AWS 보안 그룹의 SSH 허용 여부부터 확인한다.
- 화면이 이전 내용이면 먼저 `Ctrl + F5`로 캐시를 갱신한다.
- 웹 장애 시 `systemctl is-active nginx`와 Nginx 로그를 먼저 확인하고, 원인을 확인하지 않은 채 설정을 덮어쓰지 않는다.
