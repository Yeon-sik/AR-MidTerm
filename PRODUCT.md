# Product

<!-- impeccable:product-schema 1 -->

## Platform

android

## Users

실물 제품 옆에서 가격·영양정보를 확인하는 사용자와 Android AR 중간 프로젝트의 시연자.

## Product Purpose

바코드로 정확한 상품을 선택하고, 사전 등록 이미지의 3D 위치·방향에 World Space 정보판 하나를 표시한다. 영양/가격 탭과 다른 상품 스캔을 제공한다. 현재 기준은 docs/PRODUCT-IMAGE-TRACKING.md와 docs/MILESTONES.md다.

## Capabilities and Constraints

Unity 6000.3.22f1, AR Foundation·ARCore 6.3.5, 동일 AR 카메라와 ZXing.Net을 사용한다. 더단백 크런치바 초코 40g의 로컬 시연 데이터를 제공하며, 바코드 매핑은 demo-only다. 현재 마커는 브이젤 카드와 상품 데이터의 개발용 연결이다. 실제 포장 이미지·실측 크기와 새 통합의 기기 검증은 남아 있다. 상품 포장 마커에는 Physical Size를 요구한다. 로그인·구매·실시간 가격 수집·바코드 자체의 3D 자세 추정은 포함하지 않는다.

## Operating Context

EAN-13 3회 확인 후 상품을 선택한다. 앞면으로 돌리며 바코드가 사라져도 선택을 유지하고, 일치 이미지가 FULL_TRACKING일 때 표시한다. 이미지 소실·제한·다른 이미지·pause에서는 숨긴다. 다른 유효한 번호를 읽으면 재스캔 전까지 표시를 막는다. 탭은 영양/가격을 전환하며, 재스캔은 선택·자세·본문·탭을 초기화한다.

## Evidence on Hand

현재 기능·회귀 시험은 EditMode 76/76 통과다. 과거 카드 위 큐브의 실물 추적 성공과 새 정보판의 코드 기능 검증을 구분한다. 이번 통합은 앱 빌드·APK 생성·설치·실기기 시험을 수행하지 않았다. 근거는 docs/VALIDATION.md와 docs/validation/product-image-panel-2026-10-25.json에 보관한다.
