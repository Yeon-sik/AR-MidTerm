# 구기획 M0–M3 구현 및 검증 기록

> **SUPERSEDED — 이미지 추적 중심 기획의 과거 기록.** 2026-10-23 사용자가 기획 v2를 확정했다. 현재 기준은 [바코드 기반 상품정보 AR 기획 및 마일스톤](MILESTONES.md)이다. 아래 M0~M3 번호·BLOCKED 판정·이미지 재촬영/실측 조건은 당시 기획에만 적용한다. 새 기획에서는 바코드로 식별하고 사용자가 터치한 현실 평면에 정보를 배치한다. 이미지 품질 때문에 새 기획의 진행을 막지 않는다. 기존 환경·Identity·가격·Nutrition 조사 결과는 검증 범위를 유지해 재사용한다.

확인일: 2026-10-23 KST. 이 기록의 PASS는 실제로 수행한 범위에만 적용한다.

## 판정

| 단계 | 판정 | 근거 |
|---|---|---|
| M0 — Environment & Demo Product | **PARTIAL** | Unity 환경·프로젝트 임포트 성공. 이미지 v1 0점·최초 v2 45점·수정 v2 0점. 실측 약 150×65mm 확보, 최종 참조 영역과의 대응 및 운영 바코드 매핑 미확인 |
| M1 — Android AR Baseline | **PARTIAL — NOT RUN** | M0 미통과로 미착수. Android 기기 미연결 |
| M2 — Product Image Tracking | **PARTIAL — NOT RUN** | Reference Image Library·마커·추적 스크립트 미구현, 실기기 시험 0회 |
| M3 — Barcode Decode | **PARTIAL — NOT RUN** | ZXing.Net 미설치, CPU image 변환·디코더 미구현, 실기기 시험 0회 |

Architecture Status: **BLOCKED**. 이 판정은 선행 입력과 검증의 부족을 뜻한다. AR Foundation + ZXing.Net 조합의 기술적 실패를 증명한 것은 아니다.

## 강의 재확인과 프로젝트 생성

- `lecture/Chap1-Install Unity3D.pptx` 10–12쪽: Unity **6000.3.22f1 LTS**, Android Build Support, SDK/NDK, OpenJDK 지정 확인.
- `lecture/Chap4-Practice.pptx` 2·4·5·13·14쪽: AR Mobile Template, Android 전환, AR Session/XR Origin, 실기기 Build & Run 확인.
- 같은 자료 21·27–30쪽과 `Chap4-Summary.pdf`: Reference Image Library, 실물 물리 크기, 불필요한 Plane/Raycast Manager 비활성화, Tracking/Limited/None, `arcoreimg` 품질 평가 확인. 강의의 X:1은 1m 예제이며 상품 크기로 복사하지 않는다.
- `lecture/Chap5-Practice.pptx` 15쪽: `trackablesChanged.AddListener` 이벤트 사용 확인.
- `lecture/Chap6-Practice.pptx` 12·26쪽: Vuforia 별도 실습과 설정 차이 확인. 이 프로젝트에 Vuforia는 설치하지 않았다.
- 강의자료에는 AR Foundation·ARCore **6.3.5** 패치 버전을 지정한 근거가 없다. 아래 버전은 공식 레지스트리와 Unity 6 호환 문서에 근거한 프로젝트 선택이다.

공식 AR Mobile Template **2.1.2**의 `ProjectData~`에서 Assets/Packages/ProjectSettings를 가져왔다. 템플릿의 Library 캐시와 기존 lockfile은 가져오지 않았고 현재 에디터가 의존성을 다시 해결했다. 템플릿 최신 2.1.3은 AR 6.5 계열이므로, 이번에는 6.3 계열의 2.1.2를 기반으로 6.3.5 패치를 고정했다.

템플릿 SHA-256: `647d66b15bbb820b7cbf8aca6205d8d2577d714fc97d47422e438294d0311a1f`. 다운로드 SHA-1도 공식 레지스트리 값 `f2dba895316ce68b0dbf416c08a5354e52be05c0`과 일치했다. 라이선스와 출처는 `ThirdParty/UnityARMobileTemplate/`에 보존했다.

첫 임포트에서 템플릿의 에디터 도움말 패키지 `com.unity.learn.iet-framework@4.0.4`가 `TutorialWindow.cs:114`의 잘못된 `SerializeField` 적용으로 CS0592를 발생시켰다. 런타임 코드가 참조하지 않는 해당 패키지와 함께 가져온 `Assets/MobileARTemplateAssets/Tutorial/`만 제거했다. PackageCache 소스를 직접 고치지 않았다. 다시 실행한 에디터 임포트는 **return code 0**, C# 컴파일 오류 0건으로 종료됐다.

## M0 — Environment & Demo Product

### 환경

| 항목 | 확인 결과 |
|---|---|
| Unity | 6000.3.22f1, revision `1c726e1fb402`; 실행 파일과 ProjectVersion 일치 |
| Android Build Support | AndroidPlayer 빌드 모듈과 도구 디렉터리 존재 |
| SDK platforms | 34, 35, 36, 37.0 |
| SDK build tools | 36.0.0 |
| NDK | r27c / 27.2.12479018 |
| OpenJDK | Temurin 17.0.18+8; `java -version` 실행 성공 |
| AR Foundation | 6.3.5 — manifest, lockfile, 설치 패키지 일치 |
| ARCore XR Plugin | 6.3.5 — manifest, lockfile, 설치 패키지 일치 |
| ARKit | 템플릿에서 상속된 6.3.5; iOS 구현·검증 없음 |
| XR Management / XRI | 4.5.3 / 3.3.0 |
| URP | 에디터의 builtin 17.3.0으로 해결되어 manifest도 17.3.0으로 맞춤 |
| Android 설정 | 템플릿 값: minimum API 30, target Automatic, IL2CPP, architecture mask 3(ARMv7+ARM64) |
| ARCore 로더 | 템플릿의 Android XR 설정이 AR Core Loader를 참조함; 기기에서 로딩되었다는 증거는 없음 |
| Scene List | 템플릿 `Assets/Scenes/SampleScene.unity` 1개 |
| package identifier | 템플릿 `com.unity.template.ar_mobile`; M1에서 프로젝트 고유 값과 ARM64 전용 설정 필요 |
| test device | **UNVERIFIED** — `adb devices -l` 결과 연결 기기 0대 |
| 에디터 검증 | 배치 모드 임포트·C# 컴파일 성공, 종료 코드 0 |
| Android 빌드·설치 | **NOT RUN** |

Android 설정은 상속 상태를 조사한 것이며 M1의 최종 설정이나 빌드 통과를 뜻하지 않는다. `Assets/Scripts/AR`, `Assets/Scripts/Barcode`, `Assets/Prefabs`, `Assets/Images`, `Assets/Data`는 준비만 했고 상품 런타임 코드는 없다.

### First Demo Product

| 필드 | 값과 검증 범위 |
|---|---|
| Product | 더단백 크런치바 초코 40g — 사용자 제공 실물 후보; 이미지 적합성은 미통과 |
| targetName | `demo_the_danbaek_crunch_choco_40g_front` — 예약명, 아직 Library에 등록하지 않음 |
| catalogProductId | `1516c3eb-3dfb-4e11-8040-d23fadcdaad0` — PriceTrace 공개 정확 규격 조회 결과 |
| barcode | `8801104671507` — 사용자 제공 `8 801104 671507`에서 공백만 제거한 문자열; leading zero 처리 없음 |
| barcode format | EAN-13 길이·체크섬 적합. ZXing 실물 판독은 **UNVERIFIED** |
| checksum | 계산한 check digit 7 = 제공된 마지막 숫자 7 |
| 운영 barcode mapping | **UNVERIFIED** — `catalog_product_identifiers` 공개 조회 HTTP 401 / SQLSTATE 42501, 읽기 권한 없음. 매핑 없음으로 해석하지 않음 |
| brand | 빙그레 — 사용자 정보와 공개 exact product의 브랜드 일치 |
| specification | contentAmount 40, contentUnit g, packageCount 1; specificationText는 null 그대로 보존 |
| latest observed price | 확인한 **로컬 공개 스냅샷 내** 개당 1,400원. 운영 전체의 최신 관측가인지는 **UNVERIFIED** |
| seller/location | 국군복지단 바다마을마트; 별도 상세 location은 **UNVERIFIED** |
| observedAt | 2026-05-08, 날짜 정밀도. 시각·시간대는 **UNVERIFIED** |
| history available | **no** — 이 스냅샷에서 동일 정확 판매처·상품코드로 확인한 관측은 1건 |
| Nutrition basis | basis_amount 40, basis_unit g, prep_state `unspecified` |
| calories_kcal | 185 kcal |
| carbs_grams | 15 g |
| protein_grams | 15 g |
| fat_grams | 7 g |
| sugars_grams | 7 g |
| saturated_fat_grams | 5 g |
| sodium_mg | 200 mg |
| Image source | v1: 실물 사진 `더단백크런치바초코.jpg`, 2335×1020 RGB. v2: 판매 페이지 이미지 `더단백크런치바초코2.jpg`, 최초 600×600에서 사용자 수정 후 현재 538×228 RGB. 판매 페이지 URL은 UNVERIFIED. 에이전트가 사용자 파일을 변경하지 않음 |
| physical width | 사용자 실측 보고 **약 150mm / 0.150m**. 최종 참조 이미지의 경계와 같은 측정 영역인지는 **UNVERIFIED** |
| physical height | 사용자 실측 보고 **약 65mm / 0.065m**. 최종 참조 이미지의 경계와 같은 측정 영역인지는 **UNVERIFIED** |
| AR suitability | v1 arcoreimg **0/100**, 최초 v2 **45/100**, 현재 수정 v2 **0/100**. 모두 권장 75점에 미달. 실제 인식·추적 성공 여부는 **UNVERIFIED** |

### Identity·가격·Nutrition 근거

1. 기존 PriceTrace 설정의 공개 키를 프로세스 내에서만 읽어 `get_product_read_v1`을 호출했다. Unity 프로젝트나 Git에는 키를 복사하지 않았다. 브랜드·40g·1개 규격을 대조했다. `standardProduct.id`(`8510a217-fe6c-473b-8c14-2cba4a301e40`)는 사용하지 않는다.
2. 이 exact product의 공개 `sellerProducts`는 판매처 `국군복지단 바다마을마트` + sourceProductCode `260190`을 반환했다. 이 **판매처와 상품코드의 정확한 쌍**으로 `C:/Github/창팡맨/data/public/product-observations.v3.json`의 공개 스냅샷 1건에 연결했다. 상품명 유사도 연결이 아니다. 관측 ID `e78107dfe0379bff`, unitPrice 1400, quantity 9, totalPrice 12600, confidence `medium`, 스냅샷 revision `e6e6315772fcfc71`이다.
3. 현재 공개 RPC `get_product_read_v1`의 market observations와 `get_price_observations_v1`의 해당 exact product 결과는 각각 빈 배열이었다. 따라서 로컬 스냅샷 가격을 운영 verified market observation이나 현재가로 표현하지 않는다. 원격/비공개 가격의 부재도 단정하지 않는다. 가격 추이·최저·최고를 만들지 않았다.
4. Nutrition의 `get_public_product_nutrition_v1(namespace=pricetrace, catalogProductId=1516c3eb-3dfb-4e11-8040-d23fadcdaad0)`가 같은 정확 ID의 공개 링크를 반환했다. nutrition_food_id `17637a49-e3c6-45d9-8425-48b47612152b`, revision 1, source_type `pricetrace_manual`, source_reference `catalogProductId:1516c3eb-3dfb-4e11-8040-d23fadcdaad0`이다. 이는 공개 승인 링크 확인이며 영양표를 독립적으로 다시 측정한 결과는 아니다.
5. 7개 우선 영양 필드는 모두 숫자로 제공됐다. 추가 필드 fiber/cholesterol/trans fat/added sugars는 null을 그대로 유지한다. sugars를 carbs에, saturated fat을 fat에 더하지 않는다.
6. 운영 바코드 매핑은 권한 때문에 미검증이다. 권한 변경, 상품 등록, 운영 매핑 생성은 수행하지 않았다. 시연용 명시 매핑을 사용한다면 실물 확인 근거와 `demo-only` 출처를 따로 기록해야 하며, 현재 운영 DB의 기존 매핑이라고 주장할 수 없다.

### 이미지 평가

강의 ZIP의 `windows/arcoreimg.exe`를 로컬 임시 검증 경로에 추출해 실행했다. 도구 SHA-256: `9eaf4d810925d0bfc1bb8c08fa31cd62c9e0696e39169110c1c5c9a1923aab30`.

| 입력 | 결과 | 프로세스 종료 코드 |
|---|---:|---:|
| 강의 `Target-Hansung.jpg` | 80 | 0 |
| v1 실물 사진 원본 | 0 | 0 |
| v1 동일 바이트를 영문 파일명으로 복사 | 0 | 0 |
| v2 판매 페이지 이미지, 동일 바이트를 영문 파일명 `candidate-v2.jpg`로 복사 | 45 | 0 |
| 사용자 교체 후 v2 수정본, 동일 바이트를 영문 파일명 `candidate-v2-recheck.jpg`로 복사 | 0 | 0 |

사용자 이미지 SHA-256: `d8f5b53253a9fa617f7529457ee561a26edc0ed2ea0444dab0b8baf3eb856d51`. 한글 파일명 문제로 점수가 낮다는 근거는 없다. 주름·반사는 시각적으로 확인했지만 점수 0의 개별 원인을 분리 실험한 것은 아니다. 사진을 AI로 재작성하거나 임의 크기를 부여하지 않았다.

#### 2버전 재검증

사용자가 `더단백크런치바초코2.jpg`를 루트에 추가했고 판매 페이지에서 가져온 이미지라고 확인했다. 최초 파일은 JPEG/RGB, 600×600px, 30,932 bytes, SHA-256 `639b64959ed28865f0f6695be3a3fe90260c5d5f6f1d5616a9133f6e0175e0ed`였다. 원본 바이트를 영문 파일명으로 복사해 같은 강의 도구로 평가한 결과 **45점 / 종료 코드 0**이다. 도구 SHA-256도 위 기록과 일치한다.

최초 600×600 파일에서 흰 배경을 제외한 상품 부분은 픽셀 분석의 대략적 경계 `(38,200)–(568,416)`에서 **530×216px**이다. 이 경계는 어두운 픽셀 범위로 추정한 값이며 최종 참조 영역을 확정한 것이 아니다. 파일 전체가 600×600이어도 상품 영역의 세부 해상도는 다르다. 단순 확대가 원본 세부 정보를 늘린다고 가정하지 않는다.

이후 사용자가 실물 치수를 **가로 약 15cm, 세로 6.5cm**로 보고했다. 약 150×65mm / 0.150×0.065m로 기록한다. 측정 영역이 최종 참조 이미지의 경계와 같은지는 아직 미확인이다. 여백을 포함한 정사각형 사진에 상품 영역 치수를 그대로 대응시키거나, 사진을 가로·세로로 늘려 맞추지 않는다. 판매 이미지의 실제 상품 부분 비율도 실측 영역과 대조해야 한다. 판매 페이지 이미지와 현재 실물의 포장 디자인 일치 여부는 아직 검증하지 않았다.

최초 v2의 45점은 v1보다 개선됐지만 권장 75점에는 미달한다. 이것만으로 실물 인식이 반드시 실패한다고 단정하지 않는다. Reference Image Library에는 아직 등록하지 않았다.

#### 같은 파일명으로 교체된 2버전 수정본

사용자의 재확인 요청 시 루트 `더단백크런치바초코2.jpg`의 바이트가 변경된 것을 확인했다. 현재 파일은 JPEG/RGB, **538×228px**, 44,731 bytes, SHA-256 `a50a5f46505fb64773517d3de71db5334279ceeae43e9e741565bb372313019a`이다. 이전보다 흰 여백이 줄어든 상태를 시각적으로 확인했다. 에이전트가 이미지 편집이나 확대를 수행하지 않았다.

현재 바이트를 `candidate-v2-recheck.jpg`로 복사해 평가한 결과 **0점 / 종료 코드 0**이었다. 예상과 다른 결과를 확인하기 위해 같은 도구·같은 실행에서 강의 예제와 보관한 최초 v2도 재평가했다. 강의 예제 **80**, 최초 v2 **45**, 현재 수정본 **0**으로 각각 재현됐고 모두 종료 코드 0이었다. 최초 v2는 무시된 로컬 검증 경로의 복사본에 보존돼 있다.

현재 이미지의 짧은 변은 228px로 강의·공식 가이드의 참조 이미지 권장 300×300px보다 작다. 다만 크기, 종횡비, JPEG 재저장 등이 점수에 준 영향을 각각 분리한 실험은 하지 않았으므로 점수 하락의 단일 원인을 확정하지 않는다. 실제 인식·추적 성공 여부도 미검증이다. **M0 PARTIAL**을 유지한다.

다음으로 같은 포장 디자인의 고해상도 원본을 확보하거나, 포장을 평평하게 고정하고 균일한 조명에서 정면을 다시 촬영한다. 최종 참조 영역과 실측 경계를 맞춘 후 품질을 재평가한다. 점수 75 이상도 실물 추적 통과를 대신하지 않는다.

#### 기획 전환 전 추가 평가 — 더단백3·더단백4

| 입력 | 해상도 | 결과 | 종료 코드 |
|---|---|---|---:|
| `더단백3.jpg` | 1704×475 | `Failed to get enough keypoints from target image.` — 평가 실패, 0점으로 기록하지 않음 | 1 |
| `더단백4.jpg` | 2545×836 | 품질 0점 | 0 |

각 실행에서 강의 대조 이미지 `control.jpg`는 80점 / 종료 코드 0이었다. 사용자 원본은 수정하지 않고 동일 바이트를 영문 파일명으로 복사해 평가했다. 3번 SHA-256은 `26b7de0416ce2a09609e603e3d7da0c315e3da551cf6729f80a5a8c4d5b19ec4`, 4번은 `6127a66e7af7a4b1a3457a3e95546f7a36d42ae5606e54cc70ad9a5a7e38d0be`다. 로컬 로그는 `artifacts/m0-image-v3-evaluation.json`, `artifacts/m0-image-v4-evaluation.json`에 있다. 이 결과는 실제 기기 추적 시험이나 상품 이미지 인식 전체의 불가능을 뜻하지 않는다.

### 구기획 M0 재개 조건 — 현행 적용 종료

- 같은 포장 디자인의 충분한 해상도 참조 이미지와 평평하게 고정된 실물. 사용자 실측 약 150×65mm를 최종 참조 영역의 경계와 대조해 확정.
- 실물 바코드/규격을 확인할 수 있는 근거 및 운영 매핑 확인, 또는 출처가 명확한 시연 전용 매핑.
- 가격을 사용할 경우 위 1건의 로컬 관측 스냅샷이라는 출처·정밀도·confidence를 유지. 최신 운영 가격이 필요한 경우 별도 증거 필요.
- M1 실행을 위한 ARCore 지원 Android 기기 USB 연결과 기기 측 USB 디버깅 승인.

## M1 — Android AR Baseline

**PARTIAL — NOT RUN**. M0 미통과로 최소 씬 구성, Android 설정 확정, APK 빌드·설치, 카메라 권한·영상·AR Session·ARCore·방향·1분 실행 검증을 수행하지 않았다. 런타임 성공/실패는 **UNVERIFIED**다. 템플릿의 Object Spawner·Plane/Raycast와 데모 UI는 아직 M1용 최소 씬으로 정리하지 않았다.

## M2 — Product Image Tracking

**PARTIAL — NOT RUN**. M0 후보 사진 품질 검사만 수행했다. Reference Image Library, 실물 크기, ARTrackedImageManager, ProductImageTracker, 임시 마커는 미구현이다. 실기기 attempts 0, success/lost/recovery/jitter/misidentification 모두 **UNVERIFIED**다. 소실 상태에서 표시를 숨기는 동작도 아직 구현·검증하지 않았다.

## M3 — Barcode Decode

**PARTIAL — NOT RUN**. ZXing.Net의 실제 설치 버전·Unity/IL2CPP/Android 호환성·System.Drawing 비의존 빌드·라이선스 파일 배치는 아직 검증하지 않았다. XRCpuImage 변환/Dispose, 2–5Hz 스캔 토글, EAN_13/EAN_8/UPC_A/UPC_E 디코딩, raw/normalized 결과, 기대 바코드 일치, 회전/미러/stride와 카메라 공존 검증을 수행하지 않았다. Attempts 0, success/failure/time/conflict/memory/crash는 **UNVERIFIED**다. WebCamTexture나 별도 카메라 세션을 추가하지 않았다.

## Git 및 보존

시작 시 Git 저장소가 없었고 `lecture/`만 있었다. `feat/ar-foundation-m0-m3` 브랜치로 새 저장소를 초기화했다. `lecture/`와 사용자가 추가한 루트 이미지 v1·v2는 원본 그대로 보존한다. 이미지는 미통과 후보 원본이므로 커밋에서 제외한다. 생성된 Library/Logs/artifacts, 다운로드한 템플릿·도구, 조회 원문, APK, 키는 커밋하지 않는다. 원격 저장소 생성이나 push는 수행하지 않았다.

직접 작성한 문서·설정과 Packages 파일에는 `git diff --cached --check`를 적용한다. 템플릿 원본 및 Unity 직렬화 파일의 빈 필드 뒤 공백·기존 셰이더 들여쓰기·파일 끝 빈 줄은 그대로 보존하며, 전체 경로의 공백 검사가 깨끗하다고 주장하지 않는다. 이 공백 경고와 Unity 컴파일 성공은 별도 결과다.

## 구기획의 다음 단계 — 현행 적용 종료

당시에는 이미지 관련 M0 조건 충족 후 M1 → 이미지 추적 M2 → 바코드 M3를 진행하고 이후 Identity Resolver·Local Repository·World/Screen Card·State Machine을 검토할 계획이었다. 해당 기능은 구현하지 않았다. 이 순서는 종료됐으며, 현재 다음 작업과 통과 조건은 [기획 v2 마일스톤](MILESTONES.md)을 따른다.

## 공식 버전 근거

- [AR Foundation 6.3 설치·Unity 6 호환성](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.3/manual/project-setup/install-arfoundation.html)
- [ARCore 6.3 프로젝트 설정](https://docs.unity3d.com/Packages/com.unity.xr.arcore@6.3/manual/project-configuration-arcore.html)
- [Unity 공식 AR Mobile Template 레지스트리](https://packages.unity.com/com.unity.template.ar-mobile)
- [Unity 공식 AR Foundation 레지스트리](https://packages.unity.com/com.unity.xr.arfoundation)
- [Unity 공식 ARCore 레지스트리](https://packages.unity.com/com.unity.xr.arcore)
