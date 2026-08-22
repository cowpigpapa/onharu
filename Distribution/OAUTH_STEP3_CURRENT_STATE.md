# ONHARU 2.1 일반 배포 — Google OAuth 현재 상태 감사

감사일: 2026-08-17
대상: Google Cloud 프로젝트 ONHARU

## 확인 결과

- 사용자 유형: 외부
- 게시 상태: 테스트 중
- 테스트 사용자: 3명
- 인증 센터: 테스트 상태이므로 미제출
- 일일 OAuth 토큰 부여 한도: 10,000
- 데스크톱 OAuth 클라이언트: 2개
- 현재 제품 코드가 사용하는 클라이언트: ONHARU Windows
- Google 진단: WebView 미사용, 보안 OAuth 흐름 사용
- 앱 홈페이지: 미설정
- 개인정보처리방침 URL: 미설정
- 서비스 약관 URL: 미설정
- 승인된 도메인: 미설정
- 앱 로고: 미설정

## 범위 등록 상태

제품 코드가 요청하는 범위:

- `https://www.googleapis.com/auth/calendar.events`
- `https://www.googleapis.com/auth/calendar.calendarlist.readonly`

Google Cloud 동의 화면에 등록된 범위(2026-08-17 반영 완료):

- `https://www.googleapis.com/auth/calendar.events`
- `https://www.googleapis.com/auth/calendar.calendarlist.readonly`

제품 코드와 Google Cloud 동의 화면의 요청 범위가 일치한다.

두 범위는 Google Cloud Console에서 민감한 범위로 다뤄지며 제한된 범위는 아니다. 따라서 민감한 범위 인증은 필요하지만 제한된 범위용 연례 보안 평가 대상은 아니다.

## 데스크톱 Client Secret 해석

Google 공식 문서상 설치형 데스크톱 앱은 client secret을 기밀로 유지할 수 없는 public client다. Client ID와 secret이 실행 파일에 포함될 수 있으며, secret 단독 노출을 서버 비밀번호 유출과 동일하게 해석하지 않는다.

ONHARU는 PKCE S256과 무작위 state 검증을 사용한다. 이 구조는 현재 Google 진단에서도 보안 OAuth 흐름으로 판정됐다.

다만 개발/시험과 공개 배포의 변경 영향을 분리하기 위해 별도 프로젝트 또는 최소한 별도 클라이언트를 유지하는 것이 권장된다.

## 공개 전 순서

1. 공개 홈페이지와 개인정보처리방침 준비
2. 앱 로고 및 승인 도메인 등록
3. 범위 사용 사유와 OAuth 흐름 시연 영상 준비
4. 테스트 상태에서 최종 로그인 검증
5. 앱 게시 후 민감한 범위 인증 제출
6. 승인된 배포용 Client ID/Secret으로 최종 설치판 재빌드

## 공식 근거

- Google 데스크톱 OAuth와 PKCE:
  https://developers.google.com/identity/protocols/oauth2/native-app
- Google OAuth 일반 안내:
  https://developers.google.com/identity/protocols/oauth2
- 앱 인증 제출:
  https://support.google.com/cloud/answer/13461325
- 최소 범위 요청:
  https://support.google.com/cloud/answer/13807380
- Google Calendar 범위:
  https://developers.google.com/workspace/calendar/api/auth
