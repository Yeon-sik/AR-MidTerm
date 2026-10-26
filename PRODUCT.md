# Product

<!-- impeccable:product-schema 1 -->

## Platform

android

## Users

실물 제품 옆에서 가격·영양정보를 확인하는 사용자와 Android AR 중간 프로젝트의 시연자.

## Product Purpose

앞면 이미지와 바코드가 독립적으로 상품을 식별한다. 앞면은 3D 위치·방향에 World Space 정보판, 바코드는 화면 2D 정보판을 표시한다. 같은 상품은 3D 우선, 다른 상품 충돌은 숨김으로 처리한다. 현재 기준은 docs/PRODUCT-RECOGNITION-V5.md다.

## Capabilities and Constraints

Unity 6000.3.22f1, AR Foundation·ARCore 6.3.5, 동일 AR 카메라와 ZXing.Net을 사용한다. 더단백과 테아닌 밀크씨슬의 로컬 시연 항목을 제공하며 바코드 매핑은 demo-only다. 테아닌은 사용자 제공 EAN 8809375984692·약 58mm와 상/하단 크롭을 연결했다. 테아닌 상품 ID는 원격 확인 전 로컬 UUID이며 가격·영양값은 없다. 카드와 더단백의 개발용 연결도 구분 표시한다. 실물 박스 검증은 남아 있다. 상품 포장 마커에는 Physical Size를 요구한다. 로그인·구매·실시간 가격 수집·바코드 자체의 3D 자세 추정은 포함하지 않는다.

## Operating Context

이미지는 FULL_TRACKING에서 독립적으로 상품을 정하고, EAN은 별도 프레임에서 3회 확인한 뒤 상품/로컬 미등록 상태를 판정한다. 같은 상품의 여러 크롭은 허용하고 크롭별 오프셋으로 공통 원점에 표시한다. 상품이 같으면 탭을 유지하고 상품 변경·재스캔 때 초기화한다. pause/복귀 후 새 관측을 요구한다. 등록 필요 안내는 로컬 스냅샷 날짜와 읽은 번호를 함께 표시한다.

## Evidence on Hand

v5 EditMode 105/105 통과. 사용자가 초기 v5의 앞면·바코드 정상 동작을 보고했고 갤러리의 3D 출력을 확인했다. 정보판 60% 축소·소실 상태 해제를 반영한 `0.5.1 (6)`을 Galaxy A25에 호환 업데이트했다. 카메라·SessionTracking·설치 해시는 확인했으며 수정 후 실물 재시험은 남아 있다. 근거는 docs/VALIDATION.md와 docs/validation/v5-rescan-panel-fix-2026-10-26.json이다.
