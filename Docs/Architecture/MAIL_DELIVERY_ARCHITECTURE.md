# ONHARU 로컬 일정 메일 발송 경계

## 확정 공급자와 발신자 (2026-08-23)

- 메일 공급자: Amazon SES
- 권장 AWS 리전: 서울 `ap-northeast-2` 한 곳으로 고정
- 화면 표시 발신자: `ONHARU <noreply@onharu.app>`
- 권장 Custom MAIL FROM: `mail.onharu.app`
- 첨부 방식: Amazon SES API v2 `SendEmail`

SES 도메인 identity는 `onharu.app` 전체를 검증한다. 이렇게 하면 `noreply@onharu.app`을 별도 이메일 인증 없이 발신자로 사용할 수 있고 Easy DKIM을 적용할 수 있다. Custom MAIL FROM은 실제 수신 메일 주소가 아니라 반송·불만 처리를 위한 전용 하위 도메인이다.

## 결론

SMTP 비밀번호나 메일 API 키는 ONHARU EXE에 넣지 않는다. 공개 데스크톱 바이너리의 비밀값은 추출할 수 있으므로 공개 릴리스가 스팸 발송 도구가 된다.

## 클라이언트 책임

1. 사용자가 JSON 또는 ICS 형식을 고른다.
2. ONHARU가 선택 형식에 맞는 임시 파일을 생성한다. JSON/ICS는 로컬 일정만, CSV는 Google을 포함한 전체 일정이다.
3. 전송 대상, 형식, 항목 수를 사용자에게 보여주고 승인을 받는다.
4. HTTPS로 발송 API를 호출한다.
5. 성공·실패를 표시하고 임시 파일을 즉시 삭제한다.

CSV는 Google을 포함한 전체 일정 보고서다. 사용자가 CSV를 명시적으로 선택했을 때만 메일로 보내며, 발송 확인 화면에서 포함 범위를 분명히 안내한다.

## 서버 책임

- 발송 자격과 수신 주소를 검증한다.
- 요청 크기, 횟수, 파일 형식을 제한한다.
- SMTP/API 비밀값은 서버 비밀 저장소에만 둔다.
- 첨부 파일을 디스크나 데이터베이스에 영구 보관하지 않는다.
- 성공 또는 실패 후 메모리의 첨부 데이터를 폐기한다.
- 로그에는 일정 제목·메모·첨부 본문을 남기지 않는다.
- IAM 권한은 MIME 첨부에 필요한 `ses:SendEmail`, `ses:SendRawEmail`로 제한하고 가능하면 `noreply@onharu.app` 발신 조건도 적용한다.
- SES 액세스 키를 ONHARU EXE나 공개 저장소에 넣지 않는다. Lambda 실행 역할 등 단기 자격증명을 우선 사용한다.

## AWS 구성 순서

1. `ap-northeast-2`의 Amazon SES에서 `onharu.app` 도메인 identity를 만든다.
2. SES가 제시한 Easy DKIM CNAME 레코드를 DNS에 추가한다.
3. `mail.onharu.app` Custom MAIL FROM을 설정하고 SES가 제시한 MX·SPF TXT 레코드를 추가한다.
4. `_dmarc.onharu.app`에 초기 모니터링 정책 `v=DMARC1; p=none;`을 추가하고 수신 결과를 확인한다.
5. 샌드박스에서는 검증한 수신 주소와 SES mailbox simulator로만 시험한다.
6. 실제 사용자에게 보내기 전 SES production access를 요청한다.
7. Lambda/API Gateway 발송 API에 크기·횟수·수신자 검증을 적용한다.
8. `noreply@onharu.app`에서 JSON·ICS·CSV 첨부 발송을 실제 메일함으로 확인한다.

## 현재 구현 상태 (2026-08-23)

- SES 1~5단계는 `noreply@onharu.app` 기준으로 완료됐고 production access는 검토 중이다.
- 데스크톱 앱은 `https://onharu.app/api/v1/backup-email`로 JSON/ICS 로컬 일정 또는 Google 포함 전체 CSV를 보내도록 구현했다.
- 첨부는 1MB로 제한하고 전송 후 임시 파일을 삭제한다. CSV 선택 시 Google 일정 포함 사실을 발송 전에 표시한다.
- JSON과 CSV 실제 첨부 발송은 `200 sent`로 확인했다.
- 7단계 중계 API는 AWS 서울 리전의 Lambda/API Gateway로 배포됐고 `https://onharu.app/api/v1/backup-email`에 연결됐다. 시험 단계는 고정 allowlist 세 주소만 허용하며 API Gateway 초당 1회/버스트 2회, 첨부 1MB 제한을 적용한다.
- 앱은 `openid email`로 받은 Google ID 토큰을 발송 요청에 포함한다. Lambda는 Google `tokeninfo`에서 서명·만료 검증된 토큰의 `aud`가 ONHARU OAuth Client ID인지, 인증된 이메일이 수신 Gmail과 같은지 확인한다. Calendar access token과 refresh token은 메일 서버에 보내지 않는다.
- 2026-08-23 AWS Lambda에 위 검증 코드와 `GOOGLE_CLIENT_ID` 환경 변수를 배포했다. ID 토큰이 없는 위조 요청이 HTTP 401로 거절되는 것을 확인했다. 기존 연결 사용자는 새 기본 신원 범위를 받기 위해 Google 계정을 한 번 재연결해야 한다.
- `juan.hjlee@gmail.com`을 sandbox 수신자로 인증하고 빈 JSON 첨부의 실제 발송 성공(`200 sent`)을 확인했다. SES production access 승인 전까지 다른 실제 수신 주소도 SES 인증이 필요하다.

## 서버 연결 완료 조건

- `onharu.app` 또는 별도 API 서브도메인의 HTTPS 엔드포인트
- 검증된 발신 주소
- 공개 앱을 오픈 릴레이로 만들지 않는 사용자 인증
- 분당·일일 제한과 최대 첨부 크기
- 개인정보처리방침의 메일 발송 데이터 흐름 반영
- 실제 계정 2개로 성공·오류·재시도·중복 발송 시험

## 공개 사용자 운영 전환 추가 조건 (2026-08-24)

- 서버는 Google ID token의 서명, `aud`, `iss`, 만료, `email_verified`, `sub`를 검증한다.
- 수신 주소는 클라이언트가 임의 지정하지 못하게 하고 검증된 Google 계정 이메일로 고정한다.
- 발신자는 `ONHARU <noreply@onharu.app>`로 고정하며 임의 From/CC/BCC/Reply-To를 허용하지 않는다.
- 사용자별 원자적 일일·주간 한도와 idempotency key를 적용하고 API Gateway 제한만 신뢰하지 않는다.
- WAF, Lambda reserved concurrency, SES 내부 한도, AWS Budget 경보로 남용과 비용을 제한한다.
- SES Configuration Set으로 delivery/reject/bounce/complaint/rendering failure를 수집하고 suppression list를 활성화한다.
- 첨부 본문·일정 제목·토큰·원문 이메일을 로그나 영구 저장소에 남기지 않는다.
- 개인정보처리방침에는 첨부가 ONHARU 중계 서버·AWS·수신 메일 사업자를 통과하며 별도 영구 보관하지 않는다고 정확히 고지한다.
- SES mailbox simulator와 실제 계정으로 정상, 잘못된 토큰, 다른 수신자, 중복 요청, 과대 첨부, 연속 요청을 시험한다.
