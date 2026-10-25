# 기존 상품 바코드를 AR 마커로 사용하는 방안 조사

조사일: **2026-10-25 KST**. 대상: Unity 6000.3.22f1, AR Foundation·ARCore 6.3.5, Galaxy A25 / Android 16.

## 1. 결론과 범위

**상품마다 별도 물리 마커를 붙일 필요는 없다.** 기존 EAN 바코드로 상품을 식별하고 화면 위치를 따라가는 기능은 현재 v3에도 있다. 다만 카드 검증처럼 카메라 회전·거리 변화에 맞춰 3D 정보를 붙이려면 바코드의 위치·방향을 추정하는 기능이 추가로 필요하다.

확인한 공개 프로젝트들은 판독·영역 검출·자세 계산에 필요한 구성 요소를 제공한다. 조사한 범위에서는 일반 EAN 상품을 별도 조건 없이 현재 이미지 추적처럼 대체하는 완성형 구현은 확인하지 못했다. 반복 선 무늬, 실제 인쇄 크기의 차이, 구겨지거나 휘어진 포장이 주요 제약이다.

이 보고서는 기술 검토다. 바코드 3D 추적이나 자동 동적 이미지 등록을 구현·검증했다는 의미가 아니며, 후속 구현 방향의 사용자 확정을 대신하지 않는다.

## 2. 프로젝트 문서와 현재 성과

- [BARCODE-FOLLOW.md](BARCODE-FOLLOW.md): 기존 바코드 번호로 상품을 조회하고 판독 선분의 화면 위치에 2D 정보창을 표시한다. 깊이·3D 방향 추적은 포함하지 않는다.
- [REPORT-DRAFT.md Part B](REPORT-DRAFT.md#b-1-강의-요구사항과-현재-구현의-차이): 강의 요약의 3차원 정합 및 마커의 위치·방향 사용과 현재 2D UI의 차이를 기록한다.
- [IMAGE-TRACKING-PROBE.md](IMAGE-TRACKING-PROBE.md): 브이젤 참조 사진과 실물 카드로 이미지 자세 추적을 검증했다. 사용자가 큐브 표시, 카메라 회전·거리 변화 중 카드 위 위치 유지를 확인했다. 이 성공을 EAN 바코드의 3D 추적 성공으로 확대하지 않는다.
- [BarcodeDecoder.cs](../Assets/Scripts/Barcode/BarcodeDecoder.cs): ZXing.Net의 `ResultPoints` 중 처음 두 점을 `ImageStart`, `ImageEnd`로 전달한다. 이 두 점은 바코드 사각형의 네 모서리가 아니다.

현재 체크아웃에는 `lecture/` 원본이 없다. 과제 적합성은 저장소의 강의 요약을 기준으로 판단했다. 특정 라이브러리나 `ARTrackedImageManager` 사용 자체가 필수 채점 조건인지는 단정할 수 없다.

## 3. 구분해야 하는 기능

| 기능 | 얻는 정보 | 현재 상태 | 과제와의 관계 |
|---|---|---|---|
| 바코드 판독 | EAN 번호와 상품 식별 | 기존 ZXing.Net 구현 | 상품 조회의 근거 |
| 화면 추종 | 영상상의 위치·각도·영역 | v3는 판독 선분 위치 사용 | 실시간 합성 가능, 3D 정합은 부분 충족 |
| 3D 자세 추적 | 카메라/월드 좌표의 위치·방향과 거리 | 바코드 방식은 미구현 | 실제 위치·방향 정합을 보여줄 수 있는 확장 |

판독 성공, 네 점 반환, World Space Canvas 사용만으로 3D 정합이 성립하지 않는다. 실물과 영상 좌표의 대응, 카메라 정보, 크기/거리에 대한 근거가 필요하다.

## 4. 확인한 공개 프로젝트

| 프로젝트 | 확인한 기능 | 활용과 한계 |
|---|---|---|
| [ZXing.Net](https://github.com/micjahn/ZXing.Net) | UPC/EAN 등 판독, Unity 지원 | 현재 상품 식별 코드를 유지할 수 있다. [0.16.11 UPC/EAN 구현](https://github.com/micjahn/ZXing.Net/blob/v0.16.11.0/Source/lib/oned/UPCEANReader.cs)은 기본 결과로 판독 행의 좌·우 점을 만든다. 3D 자세를 반환하지 않는다. |
| [ZXing-C++](https://github.com/zxing-cpp/zxing-cpp) | EAN/UPC 판독, [네 점의 위치 정보](https://github.com/zxing-cpp/zxing-cpp/blob/master/core/src/Barcode.h) | 영역 추종 후보. [1D 구현](https://github.com/zxing-cpp/zxing-cpp/blob/master/core/src/oned/ODReader.cpp)은 판독 선들의 좌표를 합친다. 이를 실물 바 영역의 정확한 네 모서리로 가정하지 않는다. |
| [OpenCV](https://github.com/opencv/opencv) | [EAN/UPC 검출·판독](https://docs.opencv.org/4.x/d6/d25/tutorial_barcode_detect_and_decode.html), [PnP 자세 추정](https://docs.opencv.org/4.x/d5/d1f/calib3d_solvePnP.html) | 기본 검출기는 `minAreaRect`로 ROI를 둘러싼다. 원근이 반영된 실제 경계 추출·보정은 별도로 검증해야 한다. |
| [Quagga2](https://github.com/ericblade/quagga2) | 실시간 위치·판독, `box`, `line`, `angle` | 화면 추종 알고리즘 참고용. JavaScript 중심이며 Unity Android에 바로 넣는 3D 추적기는 아니다. |
| [AprilTag](https://github.com/AprilRobotics/apriltag) | 전용 태그 검출·자세 추정 | 마커 크기와 카메라 정보로 자세를 계산하는 비교 사례. 상품 EAN을 그대로 읽는 기능은 아니다. |
| [AR.js](https://github.com/AR-js-org/AR.js) | 전용 pattern·barcode 마커 추적 | [여기서 barcode는 3×3·4×4 등의 행렬 마커](https://ar-js-org.github.io/AR.js-Docs/marker-based/)다. 소매 상품의 EAN과 구분한다. |

공개 기능을 확인한 것이며 이 프로젝트의 Android ARM64·IL2CPP 통합 성능을 측정한 것은 아니다. 도입 시 네이티브 플러그인 경계, 메모리 수명, 프레임 지연, 라이선스·고지 파일을 검토한다. 별도 상용 Unity 래퍼가 필수라는 가정은 하지 않는다.

## 5. 방안 A — 바코드 자체의 경계로 자세 계산

예상 처리 흐름:

1. 동일 AR 카메라 CPU 영상에서 바코드를 읽고 상품을 선택한다.
2. 판독 위치를 초기 영역으로 사용해 실제 바 영역의 경계와 대응점을 찾는다.
3. 해당 영상의 카메라 내부 파라미터와 실물 기하 정보로 자세를 계산한다.
4. 카메라 좌표의 자세를 같은 촬영 시점의 AR 월드 좌표로 변환한다.
5. 유효한 자세에만 큐브 또는 정보판을 표시하고, 소실·오래된 결과·불확실한 자세는 숨긴다.

OpenCV `solvePnP`는 실물 점, 대응 영상 점, 카메라 내부 파라미터와 왜곡 계수를 입력으로 받는다. 직사각형 평면에는 일반 평면용 `SOLVEPNP_IPPE` 등을 검토하며, 정사각형 전용 `IPPE_SQUARE`를 EAN에 그대로 적용하지 않는다.

### 필요한 조건

- **경계 정의:** 숫자·흰 여백을 포함한 라벨과 검은 바 영역은 다르다. 실측값은 검출한 경계와 같은 영역을 기준으로 해야 한다.
- **크기 또는 거리 정보:** GS1은 인쇄 크기를 허용 범위로 규정하므로 EAN 번호만으로 실제 폭을 고정할 수 없다. [GS1 크기 안내](https://support.gs1.org/support/solutions/articles/43000734080-do-barcodes-have-to-be-printed-at-the-nominal-size-)
- **평면 가정:** 구겨진 비닐이나 원통에 휘어진 바코드에서는 번호가 읽혀도 평면 자세 추정은 실패할 수 있다.
- **좌표·시간 대응:** CPU 영상의 회전·잘림과 내부 파라미터 해상도, OpenCV/Unity 좌표축, 촬영 시각의 카메라 자세를 일치시킨다.
- **품질 검사:** 재투영 오차, 모서리 순서, 유효 깊이, 급격한 자세 변화 등을 검사한다. 실패를 임의 거리의 3D 표시로 대체하지 않는다.

최소 검증에서는 평평한 바코드가 있는 상품 2~3종의 바 영역 크기를 실측할 수 있다. 사진 마커 부착·사전 사진 등록은 필요 없지만, 크기 메타데이터는 필요하다. 모든 미등록 상품의 정확한 거리를 자동으로 얻는 범용 해결책으로 설명하지 않는다.

실측 없는 확장에는 지원되는 Depth 또는 정지한 상품을 여러 시점에서 관찰하는 초기 기하 추정 등을 검토할 수 있다. 이는 추가 개발 항목이다. [현재 ARCore 공식 기기 목록](https://developers.google.com/ar/devices)에서 Galaxy A25 5G에는 Depth API 지원 표시가 없으므로, 이 기기에서 Depth로 해결된다는 전제를 기본 설계로 삼지 않는다.

## 6. 방안 B — 바코드 주변 포장 영역의 동적 이미지 등록

추가 물리 마커와 상품별 사전 사진 등록을 줄이면서 카드 추적 코드를 재사용하는 대안이다.

1. 바코드로 상품 번호와 화면 영역을 얻는다.
2. 정면에 가까운, 평평하고 선명한 바코드 주변의 글자·그림 영역을 촬영한다.
3. 필요 시 원근을 보정하고 품질을 검사한 뒤 실행 중 참조 이미지 라이브러리에 추가한다.
4. 해당 이미지의 현재 자세를 추적하고 상품정보판을 표시한다.

[AR Foundation 6.3.5의 mutable library](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.3/manual/features/image-tracking/mutable-libraries.html)는 실행 중 이미지 추가를 지원한다. [공식 이미지 추적 샘플 설명](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.3/manual/samples/features/image-tracking.html)의 동적 등록 기능을 참고할 수 있다. 자동 영역 선택·원근 보정·품질 검사·상품과 참조 이미지의 연결은 추가 구현 항목이다.

자세 추적 대상은 **바코드 주변 포장 이미지**이며 순수 EAN 패턴을 사용하는 방안 A와 다르다. [ARCore 지침](https://developers.google.com/ar/develop/augmented-images)은 바코드 같은 선 무늬 및 반복 패턴이 이미지 추적에 불리하다고 설명한다. 바코드만 잘라 등록한다고 카드 수준의 추적이 보장되지 않는다. 흐림, 반사, 구김, 부정확한 원근 보정도 위험이다.

## 7. 과제 방향과 후속 검증 제안

기존 바코드의 위치·방향을 기준으로 실제 3D 정보를 합성하면 저장소에 정리된 마커 기반 AR 및 3차원 정합 방향과 맞는다. 화면 위치만 따라가면 기존 v3의 부분 충족 상태가 유지된다. 책상에 고정한 앵커가 손에 든 상품을 따라가는 것과 같다고 설명해서도 안 된다.

**다음 최소 검증은 정보판 통합보다 바코드 기준 큐브 추적을 먼저 확인하는 것이다.**

| 단계 | 검증 내용 | 통과 전에 주장하지 않을 것 |
|---|---|---|
| 바코드 영역 | 서로 다른 상품의 번호와 경계가 맞는지 화면에 표시 | 검출 사각형이 곧 정확한 3D 대응점이라는 주장 |
| 자세 추정 | 바코드에 맞춘 큐브와 카메라·상품의 이동, 회전, 거리 변화 관찰 | 임의 거리나 화면 회전만으로 3D 정합 성공 주장 |
| 소실·재획득 | 가림·화면 이탈·다른 번호·복귀 시 잘못된 위치에 남지 않는지 확인 | 오래된 앵커가 움직이는 상품의 현재 위치라는 주장 |
| 상품정보 연결 | 유효한 자세와 정확한 EAN 조회 결과에만 기존 정보판 연결 | 미등록 상품을 임의 시연 상품에 연결 |

기존 [실기기 완료 기준](BARCODE-FOLLOW.md#실기기-완료-기준)의 반복 인식·오연결·소실 시험에 회전·거리 변화의 3D 정합 검증을 더한다. 구현 성공 여부는 실제 상품과 기기에서 기록한다.

## 8. 현재 완료와 미구현 범위

현재 완료된 것은 카드 이미지 추적 앱의 빌드·51개 자동 시험·Galaxy A25 설치·실물 카드 FULL_TRACKING 관찰이다. 빌드 식별자, 설치 오류와 복구, 사용자 확인은 [VALIDATION.md](VALIDATION.md)와 [빌드·기기 검증 요약](validation/card-probe-2026-10-25.json)을 따른다.

바코드 자체의 네 모서리 검출, PnP 자세 추정, 포장 영역의 자동 동적 등록, 상품정보판과 3D 자세 연결은 아직 구현하지 않았다. 위 방안의 가능성과 현재 앱에서 확인한 성과를 분리한다.
