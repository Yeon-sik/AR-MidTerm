# AR Product Lens | 바코드와 상품 앞면을 연결하는 Android AR 정보 앱

> 바코드로 상품을 식별하고, 등록된 상품 앞면을 AR로 추적해 영양·가격 정보판을 실제 공간에 표시하는 Unity 기반 Android 프로젝트다.

| 항목 | 내용 |
| --- | --- |
| 프로젝트 형태 | 대학 가상현실/AR 프로젝트 |
| 담당 범위 | 상품 식별, AR tracking, 외부 데이터 연동, 상태 복구, UI, 실기기 검증 |
| 문서 기준 | `feat/ar-foundation-m0-m3@2ebf9ca11bafefe13d716b7b72a6a046f58bc7a1` |
| 주요 기술 | Unity 6, AR Foundation, ARCore, Android, barcode scanning, Open Food Facts, HTTP API |
| Repository | [Yeon-sik/AR-MidTerm](https://github.com/Yeon-sik/AR-MidTerm) |
| 상세 문서 | [Project_Detail.md](./Project_Detail.md) |

## 30초 요약

- **문제**: 실물 상품을 보고 있을 때 바코드나 검색 결과를 별도 화면에서 확인하면 현실의 어떤 상품과 정보가 연결되는지 맥락이 끊긴다.
- **해결**: 바코드로 상품 identity를 확정하고, 사전 등록 또는 현장 등록한 상품 앞면을 AR tracking 대상으로 사용해 3D 정보판을 상품 옆에 표시한다.
- **핵심 구현**: EAN-13 반복 확인, 로컬/원격 상품 조회, 앞면 image tracking, 런타임 현장 등록, 3D 정보판, tracking loss 시 즉시 숨김, 재획득, 상품 충돌 처리, 누락값 표현을 포함한다.
- **데이터 원칙**: 가격·영양 값이 없으면 만들지 않고 출처와 snapshot 경계를 유지한다. 같은 상품은 3D 표시를 우선하고 서로 다른 상품이 동시에 감지되면 잘못된 결합을 피하기 위해 숨긴다.
- **검증 경계**: 저장소에는 Galaxy A25 실기기에서 바코드 인식, AR 배치, 현장 등록·소실·재획득을 확인한 기록과 Unity EditMode 테스트 기록이 있다. 이번 문서 작업에서는 기기 시험이나 Unity test를 재실행하지 않았다.

## 사용자 흐름

```text
카메라 실행
  → 바코드 3회 확인
  → 상품 identity 확정
  → 로컬/PT/Open Food Facts 데이터 조회
  → 사전 등록 앞면 또는 현장 등록
  → image tracking
  → 상품 옆 3D 정보판 표시
  → tracking 소실 시 숨김
  → 재획득 시 복구
```

## 핵심 기능

- EAN-13 반복 확인과 오독 완화
- 상품 identity와 표시 정보 분리
- 바코드 2D 정보판
- 등록된 앞면 image tracking과 3D 정보판
- 실행 중 상품 앞면 현장 등록
- 추적 소실·재획득·상품 변경 상태 처리
- 영양/가격 누락과 출처 표시
- 외부 상품 데이터 fallback
- Android 실기기 검증 기록

## 현재 경계

이 프로젝트는 AR 상품 정보 시연과 상태 정확성에 초점을 둔다. 모든 상품을 자동 인식하는 범용 computer vision 모델, 운영 규모 상품 DB, 장시간 반복 안정성, 상용 앱 수준의 UX를 목표로 하지 않는다.
