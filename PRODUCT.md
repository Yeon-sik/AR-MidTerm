# Product

<!-- impeccable:product-schema 1 -->

## Platform

android

## Users

실물 제품 옆에서 가격·영양정보를 확인하는 사용자와 Android AR 중간 프로젝트의 시연자.

## Product Purpose

바코드로 정확한 상품을 식별하고 바코드 위에 상품정보창 하나를 자동 표시한다. 보이는 바코드의 화면 위치를 계속 갱신한다. 현재 상호작용은 `docs/BARCODE-FOLLOW.md`, 기존 데이터 의미는 `docs/MILESTONES.md`를 따른다.

## Capabilities and Constraints

Unity 6000.3.22f1, AR Foundation·ARCore 6.3.5, 동일 AR 카메라와 ZXing.Net을 사용한다. 첫 상품은 더단백 크런치바 초코 40g이다. 로컬 시연 스냅샷이며 바코드가 보이는 동안 2D 위치를 추종한다. 가림 이후의 상품 전체 3D 추적·이미지 타깃·로그인·상품 등록은 포함하지 않는다. 바코드 연결은 demo-only 매핑이다.

## Operating Context

바코드를 비추면 터치 없이 정보창이 나타나고 움직이는 바코드를 따라간다. 위치를 놓치면 숨기고 같은 바코드를 다시 비추도록 안내한다. 다른 상품 스캔을 제공한다. 미실행 실기기 검증을 PASS로 표시하지 않는다.

## Evidence on Hand

기존 환경·상품·관측 가격·Nutrition 확인 근거는 `docs/M0-M3-validation.md`에 있다. 실행 증거는 `docs/VALIDATION.md`에서 관리한다.
