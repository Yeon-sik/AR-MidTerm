# AR Product Lens | Project Detail

> 이 문서는 AR-MidTerm의 상품 identity, AR tracking, 외부 데이터, 상태 복구와 검증 경계를 설명한다. 빠른 소개는 [Project_Intro.md](./Project_Intro.md)를 참고한다.

| 항목 | 내용 |
| --- | --- |
| 문서 상태 | Active |
| 기준 source revision | `feat/ar-foundation-m0-m3@7149c68eddf5dcf04905f474a518e445701071d3` |
| 구현 근거 | README, PRODUCT-RECOGNITION-V5, VALIDATION, ONLINE-LOOKUP, tracked Unity source/tests |
| 검증 등급 | Repository + 기존 user-confirmed/device evidence. 이번 문서 작업에서 테스트 재실행 없음 |

## 1. 시스템 목적

AR Product Lens는 "카메라에 보이는 것"과 "상품 데이터"를 안정적으로 연결하는 문제를 다룬다. 핵심은 화려한 AR 연출보다 **어떤 상품을 보고 있는지 identity를 확정하고, tracking 상태가 불확실할 때 잘못된 정보를 공간에 남기지 않는 것**이다.

## 2. Identity와 표시 경로

프로젝트는 두 개의 인식 경로를 분리한다.

```text
Barcode path
  EAN-13 반복 확인
    → product identity 확정
    → local/PT/OFF 데이터 조회
    → 2D 정보판

Front-image path
  사전 등록 또는 런타임 등록
    → AR image tracking
    → tracked product identity
    → 3D 정보판 + 윤곽/연결선
```

앞면 이미지와 바코드는 서로 독립적으로 동작한다. 같은 상품을 가리킬 때는 3D 앞면 tracking을 우선하고, 서로 다른 상품 identity가 동시에 보이면 잘못된 정보 결합을 피하기 위해 표시를 숨긴다.

## 3. 바코드 판독과 오독 완화

단발성 바코드 판독 결과를 바로 확정하지 않는다. 같은 EAN-13이 연속으로 확인되는 조건을 두어 순간 오독으로 미등록 또는 다른 상품 상태가 확정되는 문제를 줄인다.

확정된 identity는 표시 데이터와 분리한다. 따라서 외부 영양·가격 조회가 실패하더라도 "무슨 상품인지"와 "현재 어떤 데이터가 उपलब्ध한지"를 서로 다른 상태로 표현할 수 있다.

## 4. 상품 데이터와 누락 의미

상품 데이터는 로컬 snapshot과 외부 provider를 조합한다. 저장소 문서상 현재 온라인 흐름은 PT 공개 함수에서 연결 여부를 확인하고, 필요한 경우 Open Food Facts를 fallback으로 사용한다.

중요한 규칙은 다음과 같다.

- 값이 없으면 추정해서 만들지 않는다.
- `0`과 `missing`을 구분한다.
- 가격은 관측값의 출처·날짜와 함께 다룬다.
- 영양은 기준량과 출처를 표시한다.
- 외부 조회 실패가 기존 local snapshot을 거짓으로 무효화하지 않는다.

## 5. AR tracking 상태 모델

3D 정보판은 tracking이 신뢰 가능한 상태에서만 표시한다.

- FULL_TRACKING에서 표시한다.
- tracking loss, pause, 제한 상태에서는 잘못된 공간 위치를 유지하지 않고 숨긴다.
- 같은 상품을 재획득하면 기존 UI context를 복구할 수 있다.
- 다른 상품으로 바뀌거나 rescan하면 상품별 UI 상태를 초기화한다.
- 서로 다른 상품의 앞면/바코드가 충돌하면 하나를 임의 선택하지 않고 숨긴다.

이 규칙은 stale async result나 오래된 pose가 다른 상품 정보와 결합되는 위험을 줄이는 역할을 한다.

## 6. 런타임 앞면 등록

확정된 상품에서 `앞면 등록`을 시작하면 등록 대상 identity와 source를 촬영 시작 시점에 고정한다. 품질 조건을 통과한 앞면은 현재 실행 동안 image tracking 대상으로 사용한다.

현장 등록 정보는 앱 종료 뒤 영구 운영 데이터로 간주하지 않는다. 실측 크기가 없는 경우 ARCore 추정 크기를 사용하며, 이 한계를 저장소 문서에 명시한다.

## 7. 검증 상태

### 저장소에 기록된 검증

- Unity EditMode 자동 시험 기록
- Android 카메라 입력과 EAN-13 decoder 확인
- Galaxy A25에서 실제 바코드 상품명 표시 확인
- 실제 평면 감지와 anchor 배치 기록
- 사전 등록 앞면 3D 정보판 확인
- 런타임 상품 앞면 등록 → 인식 → 소실 → 재인식 흐름 기록
- PT/OFF 온라인 조회와 로컬 fallback 경계 기록

일부 실기기 결과는 저장소 문서에 사용자의 당시 확인이 포함된 **user-confirmed evidence**다. 이번 문서 작업에서 동일 실험을 독립 재현하지 않았으므로 새 runtime verified 결과로 승격하지 않는다.

### 미검증 또는 제한

- 모든 상품에 대한 범용 앞면 인식
- 장시간/10회 이상 반복 안정성의 완전한 검증
- 앱 완전 종료 후 런타임 등록 유지
- 대규모 운영 상품 DB와 SLA
- 서로 비슷한 포장의 대규모 충돌률
- 다양한 Android 기기 성능

## 8. 기술적 판단

### Barcode와 AR tracking을 분리한 이유

바코드는 identity에 강하지만 3D pose를 주지 않는다. 앞면 image tracking은 공간 위치를 줄 수 있지만 사전에 어떤 상품인지 연결해야 한다. 두 신호를 한 역할로 섞지 않고 identity와 pose의 서로 다른 evidence로 사용한다.

### 불확실할 때 숨기는 이유

AR에서는 잘못된 정보를 그럴듯한 위치에 계속 보여주는 것이 단순한 빈 화면보다 위험하다. 따라서 stale pose, 다른 상품 충돌, tracking loss에서는 정보판을 유지하지 않는 방향을 선택했다.

## 9. 포트폴리오에서 보여주는 역량

이 프로젝트는 Unity 사용 경험 자체보다 **identity, 외부 데이터, spatial tracking, stale state, conflict를 하나의 상태 기계로 연결한 경험**을 보여준다. 특히 실기기에서 실패 사례를 관찰하고 판독 해상도, 반복 확인, 소실 처리 규칙을 조정한 과정을 설명할 수 있다.
