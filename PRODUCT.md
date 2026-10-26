# Product

<!-- impeccable:product-schema 1 -->

## Platform

android

## Users

실물 제품 옆에서 가격·영양정보를 확인하는 사용자와 Android AR 중간 프로젝트의 시연자.

## Product Purpose

앞면 이미지와 바코드가 독립적으로 상품을 식별한다. 앞면은 3D 위치·방향에 World Space 정보판, 바코드는 화면 2D 정보판을 표시한다. 같은 상품은 3D 우선, 다른 상품 충돌은 숨김으로 처리한다. 현재 기준은 docs/PRODUCT-RECOGNITION-V5.md다.

## Capabilities and Constraints

Unity 6000.3.22f1, AR Foundation·ARCore 6.3.5, 동일 AR 카메라와 ZXing.Net을 사용한다. 로컬 시연 바코드 매핑은 demo-only다. 테아닌은 사용자 동일 상품 확인 후 PT ID로 연결하고 관측 가격 13,210원 2건의 근거를 보존했다. 승인 공개 영양 결과가 없으면 값을 만들지 않는다. PT 공개 바코드 조회, matched의 승인 영양 조회, not_linked의 OFF v3 조회를 추가했다. 설정 없음·비활성화는 로컬 전용이며 오류와 미발견은 구분한다. 상품 포장 마커에는 Physical Size를 요구한다. 로그인·구매·실시간 가격 수집·바코드 자체의 3D 자세 추정은 포함하지 않는다.

## Operating Context

이미지는 FULL_TRACKING에서 독립적으로 상품을 정한다. EAN은 3회 확인 후 로컬 결과를 즉시 표시하고, 온라인 결과는 현재 확정 번호·요청 세대가 맞을 때만 별도 상태로 반영한다. 같은 상품의 여러 크롭은 허용하고 크롭별 오프셋으로 공통 원점에 표시한다. 상품이 같으면 탭을 유지하고 상품 변경·재스캔 때 초기화한다. pause/복귀 후 새 관측을 요구한다. PT 등록·OFF 출처·스냅샷 날짜·확인 불가를 구분하고 연결 없음으로 상품 자체의 미등록을 단정하지 않는다.

## Evidence on Hand

EditMode 162/162 통과(기존 105 + 신규 57).A25의 0.6.0 (7)에서 PT matched·승인 영양·OFF 직접 조회·Android User-Agent 전달을 확인했다. 사용자가 두 바코드 PT 등록 표시·테아닌 앞면 3D·소실·오프라인 대체 표시를 확인했다. 작업 초반 PT 404/PGRST202는 이후 200으로 바뀌었다. 코드·직접 클라이언트·실물 E2E 결과는 구분한다. 근거는 docs/VALIDATION.md, 설정과 남은 경계는 docs/ONLINE-LOOKUP.md를 따른다. 2026-10-29 제출물 우선이며 10/27 밤 이전 온라인 기기 확인 조건을 충족했다.
