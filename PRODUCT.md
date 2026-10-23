# Product

<!-- impeccable:product-schema 1 -->

## Platform

android

## Users

실물 제품 옆에서 가격·영양정보를 확인하는 사용자와 Android AR 중간 프로젝트의 시연자.

## Product Purpose

바코드로 정확한 상품을 식별하고 사용자가 터치한 현실 평면에 상품정보 하나를 배치한다. 확정 기획과 데이터 의미는 `docs/MILESTONES.md`를 따른다.

## Capabilities and Constraints

Unity 6000.3.22f1, AR Foundation·ARCore 6.3.5, 동일 AR 카메라와 ZXing.Net을 사용한다. 첫 상품은 더단백 크런치바 초코 40g이다. 로컬 시연 스냅샷이며 자동 상품 추적·이미지 인식·로그인·상품 등록은 포함하지 않는다. 바코드 연결은 사용자 제공 근거의 demo-only 매핑이며 실물 디코딩은 별도 시험한다.

## Operating Context

사용자가 바코드를 비춘 뒤 상품 옆의 수평 표면을 한 번 터치한다. 정보판은 공간에 고정되고 재배치·삭제·다시 스캔할 수 있다. 사용자 요청에 따라 독립 구현·로컬 검증은 기기/실물 시험을 기다리지 않고 진행하며 미실행 검증을 PASS로 표시하지 않는다.

## Evidence on Hand

기존 환경·상품·관측 가격·Nutrition 확인 근거는 `docs/M0-M3-validation.md`에 있다. 실행 증거는 `docs/VALIDATION.md`에서 관리한다.
