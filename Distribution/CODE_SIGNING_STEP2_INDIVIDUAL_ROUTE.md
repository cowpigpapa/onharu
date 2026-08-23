# ONHARU 2.1 일반 배포 — 2단계 개인 인증서 경로

검토일: 2026-08-17
배포 형태: 개인 명의 프리웨어

## 선택 결과

권장 상품은 **Certum Standard Code Signing in the Cloud**다.

- 개인 개발자 발급을 공식 지원한다.
- Windows EXE, DLL, 설치 프로그램 서명과 타임스탬프를 지원한다.
- 물리 카드·USB 리더 배송과 드라이버 관리가 필요 없다.
- 공식 시작 가격은 약 EUR 209이다. 결제 통화·부가세·유효기간에 따라 최종 금액은 달라질 수 있다.
- 공개 신뢰 서명이므로 자체 서명과 달리 사용자 PC에 루트 인증서를 별도로 설치하지 않는다.

## 제외한 선택

- Open Source Code Signing: ONHARU는 프리웨어이며 공개 오픈소스 프로젝트로 확정되지 않았으므로 사용하지 않는다.
- EV Code Signing: 개인에게 발급되지 않고, 현재 SmartScreen 즉시 통과 혜택도 없다.
- Microsoft Artifact Signing Public Trust: 한국 거주 개인은 현재 개인 발급 대상 지역이 아니다.
- 자체 서명: 일반 배포 사용자 PC에서 기본 신뢰되지 않는다.

## 개인 발급 준비물

- 유효한 신분증
- 신청자 본인 명의 영문 이름
- 신청자 명의 공과금·통신요금 등 주소 확인 서류
- Certum 계정과 자동 신원 확인
- SimplySign 사용을 위한 인증 수단

## 공개 게시자 이름 주의

인증서가 개인 명의로 발급되면 Windows에 표시되는 검증된 게시자 이름은 인증서의 법적 영문 이름을 따른다. 현재 설치 메타데이터의 `JUAN.HJLEE`는 인증서 이름이 확정된 뒤 동일한 표기로 변경해야 한다.

## 발급 후 서명 순서

1. `ONHARU.exe`
2. `Onharu.LayerHost.exe`
3. `Onharu.DesktopHook.dll`
4. 서명된 세 파일로 설치 프로그램 재빌드
5. `ONHARU-2.1.0-Setup.exe` 최종 서명
6. RFC 3161 SHA-256 타임스탬프와 서명 체인 검증

## 공식 자료

- Certum Code Signing 상품:
  https://www.certum.eu/en/code-signing-certificates/
- Certum 개인 Standard 발급 서류:
  https://support.certum.eu/en/code-signing-required-documents/
- Certum Code Signing 구매 비교:
  https://shop.certum.eu/buy-a-code-signing-certyficate
