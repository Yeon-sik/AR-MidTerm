# 강의 기반 이미지 추적 상품정보판

2026-10-25 구현. 범위는 이미지 추적 정보판, 바코드 선택 유지, 영양/가격 탭과 재스캔이다. 사용자 요청에 따라 앱 빌드·APK 생성·설치·실기기 시험은 실행하지 않는다.

## 강의 적용

- Chap4의 Reference Image Library와 ARTrackedImageManager를 사용한다. 참조 이미지의 위치·방향을 World Space Canvas에 반영한다.
- Chap3의 마커 위치·방향 기준 합성과 3D 공간 배치에 대응한다. 정보판을 카메라 정면으로 돌리는 billboard나 화면 좌표에 고정하는 방식은 사용하지 않는다.
- Unity UI Button과 클릭 이벤트로 영양/가격을 전환한다. 새로운 AR SDK, OpenCV/PnP, 실행 중 이미지 등록은 추가하지 않았다.
- 기존 카드 probe에서 실기기 검증한 ARCore FULL_TRACKING 확인 코드를 공유한다. LAST_KNOWN_POSE를 움직이는 상품의 현재 자세로 사용하지 않는다.

원문 위치와 선택 근거: [COURSE-ALIGNED-PLAN.md](COURSE-ALIGNED-PLAN.md).

## 실행 흐름과 표시 규칙

1. 같은 EAN-13을 3회 연속 확인하면 기존 로컬 카탈로그의 상품을 선택한다. 미등록 번호에는 대체 상품을 붙이지 않는다.
2. `Assets/Data/product-images.json`의 바코드로 참조 이미지 이름을 찾는다. 바코드가 안 보이는 동안에도 선택을 유지한다.
3. 연결 이미지가 FULL_TRACKING이고 카메라·AR 세션이 준비되었을 때 정보판을 표시한다. 이미지의 현재 위치·방향을 매 LateUpdate에 반영하며 표면에서 8mm 띄운다. 정보판 너비는 이미지 폭의 1.6배, 14~28cm 범위다. 카드의 미실측 크기는 추정값이므로 정확한 물리 크기 검증으로 해석하지 않는다.
4. 이미지 소실·제한·마지막 자세, 다른 이미지 동시 추적에서는 숨긴다. 일치하는 이미지만 다시 추적되면 복구한다.
5. 유효한 다른 EAN이 읽히면 선택을 바꾸지 않고 숨긴다. 같은 번호가 다시 보여도 경고를 자동 해제하지 않으며 명시적으로 재스캔한다. 촬영 후 1초를 초과한 판독 결과는 이 검사에 사용하지 않는다.
6. pause·비활성화·재스캔은 보관된 자세와 카메라 준비 상태를 무효화한다. 복귀 후 새 카메라 프레임과 이미지 추가/갱신이 있어야 표시한다.
7. 영양/가격 탭은 상품 선택과 자세를 바꾸지 않는다. 추적 복구에서는 탭을 유지하며 재스캔·새 상품 바인딩에서는 영양 탭으로 초기화한다.

## 데이터와 현재 마커

- 기본 씬은 `Assets/Scenes/ProductAR.unity`다. 같은 AR 카메라를 바코드와 이미지 추적에 사용한다. 기존 평면 배치는 비활성화한다.
- 라이브러리는 `Assets/Images/ProductImageLibrary.asset`이다. probe 라이브러리와 분리해 probe 재생성·시험이 상품 설정을 덮어쓰지 않는다.
- 현재 `8801104671507` 더단백 데이터와 `test-card`의 연결은 `developmentOnly: true`다. **카드가 더단백 상품 포장이라는 뜻이 아니다.** 대기 안내와 정보판 모두 개발용 연결임을 표시한다.
- 새 포장 마커는 이 라이브러리와 JSON의 이름을 일치시켜 등록한다. `developmentOnly: false`인 상품 포장은 실측 Physical Size가 없으면 초기화를 거부한다. 미등록 이미지·중복 이름·중복 바코드·카탈로그에 없는 번호·JSON 오타도 거부한다.
- 영양 기준량·수치·단위와 누락값/0의 차이를 보존한다. 가격은 판매처·관측일·출처와 함께 표시하고 현재 가격과 다를 수 있음을 안내한다. 사용자용 `displaySource`와 원문 근거 `source`를 분리해 보존한다.
- 기존 2D `BarcodeProductPanel`은 비교·회귀 시험용으로 비활성 상태로 남긴다. 새 실행 흐름은 바코드 viewport 좌표를 수집하지 않는다.

## 코드 기능 검증

`scripts/Test-EditMode.ps1`로 기존 회귀 검사와 새 기능을 실행한다. 그래픽 출력 없이 Editor 내부에서 실행하며 앱 플레이어를 빌드하지 않는다. RuntimeOnly 버튼 리스너는 시험 인스턴스에서만 EditorAndRuntime으로 실행한다. 원본 프리팹의 리스너 설정은 바꾸지 않는다.

- 상품 선택 유지, 이미지 소실·재획득, 다른 이미지와 다른 바코드 거부.
- 참조 이미지·실측 크기·데이터 연결 검증.
- 실제 ProductARApp의 바코드 선택부터 정보판 재스캔 버튼까지의 이벤트 연결.
- 3D 위치·방향·크기 계산, pause/복귀 시 자세 무효화와 선택·탭 유지.
- World Space Canvas의 GraphicRaycaster·카메라 연결, 실제 UI API의 포인터 영역 판정과 버튼 이벤트 전달, 정상/일부 누락·0/전체 누락 데이터의 재바인딩. 정면과 이미지 평면 방향을 모두 검사한다.
- 기존 바코드 디코더·3회 확인·2D 추종·한국어 정보판·씬 연결 검사.

최종 실행 결과는 [VALIDATION.md](VALIDATION.md)에 기록한다. 원시 결과·에디터 로그·시험 입력 보존본은 Git 제외 경로 `artifacts/editmode/`에 둔다.

코드 검증은 실제 포장 인식률, 이미지 자세의 실물 정합, 휴대폰에서의 가독성·터치 성공을 증명하지 않는다. 실제 포장 사진과 실측 크기를 등록한 뒤 바코드→앞면 전환, 이동·회전·거리 변화, 가림·재획득, pause/복귀, 다른 상품 거부, 탭·재스캔을 실물에서 확인해야 한다.

## API 참고

- [AR Foundation 6.3 ARTrackedImageManager](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.3/manual/features/image-tracking/artrackedimagemanager.html)
- [Unity UI GraphicRaycaster](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-GraphicRaycaster.html)
- [ARCore augmented image tracking method](https://developers.google.com/ar/reference/c/group/ar-augmented-image)
