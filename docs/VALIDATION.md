# 기획 v2 구현·검증 기록

최신 확인일: **2026-10-24 KST**. 초기 구현은 `feat/ar-foundation-m0-m3`의 `b949cf7`, 바코드 수정 기준은 `29e66b0`이다. 아래 2026-10-24 공간 배치·센서 보완은 후속 작업 트리 변경과 생성 APK의 해시로 구분한다. 초기 조사 기준은 `45c7037`이다. 제품 사진·HTML 등 사용자 입력은 변경하지 않았다. Git 원격 생성·push·배포는 수행하지 않았다.

**2026-10-23 바코드 수정:** 3회 확인만으로 반복 오독이 해결되지 않아 판독 입력을 1920×1080으로 높였다. 당시 자동 시험은 **24/24 PASS**, 수정 APK 빌드·설치·실제 1920×1080 CPU 입력 확인까지 완료했다. 고해상도 실물 반복 시험은 대기 중이다. 최신 결과는 [해상도 분석 기록](BARCODE-RESOLUTION.md)을 따른다. 아래 초기 구현 검증 19개와 구분한다.

사용자가 사용자 개입 없이 가능한 후속 구현을 계속하도록 지시했으므로 **구현 완료·자동 시험·기기 실행·실물 검증을 분리**한다. 기기의 존재나 APK 설치만으로 실제 상품 인식·공간 배치의 PASS를 주장하지 않는다.

## 2026-10-24 사용자 확인 및 공간 배치 후속 작업

사용자가 실물 바코드를 스캔한 뒤 기본 “시연 상품” 문구가 **“빙그레 / 40g 1개 더단백 크런치바 초코”**로 바뀌었다고 보고했다. 현재 `ProductARApp.Refresh()`에서 이 상품 요약은 `workflow.Selected`가 존재할 때만 표시되므로 **상품 판독·로컬 조회 연결의 사용자 확인**으로 기록한다. 이후 기기 로그에서 00:28:45·00:29:06·00:29:09의 정확한 `8801104671507` 판독을 확인했다. 근거: `artifacts/device/placement-before-update.log`. 정해진 10회 시험의 성공률은 아직 수집하지 않았다. M2의 10회 목표 달성이나 실제 공간 배치 성공으로 확대하지 않는다.

다음 단계는 M3의 **수평 평면 탐색 → 상품 옆 터치 → ARAnchor에 정보판 배치 → 카메라 이동 시 현실 위치 유지**다. 상품명 요약은 화면 UI이고, 공간에 놓이는 가격·영양정보판과 구분한다. 현재 씬의 평면/레이캐스트/앵커 매니저와 World Space 프리팹 연결은 정적으로 확인했다.

후속 점검 시작 시 ADB에 대상 Galaxy A25가 연결되지 않았으나, 사용자 연결 후 대상 serial을 지정해 로그를 수집하고 수정 APK를 호환 업데이트했다. 에뮬레이터에는 설치하지 않았다.

### 공간 배치 코드 수정 및 검증

- `ProductPlacement.Update()`가 `MeshRenderer.enabled`를 끄더라도 활성 `ARPlaneMeshVisualizer.Update()`가 다음 프레임에 다시 켜는 충돌을 발견했다. 현재 설치된 AR Foundation 6.3.5 소스와 실제 평면 프리팹 회귀 시험으로 재현했다.
- 표시 책임을 가진 `ARPlaneMeshVisualizer.enabled`를 제어하도록 수정했다. 평면의 추적 컴포넌트와 앵커는 유지하고, 배치 완료 시 표면 표시만 즉시 숨긴다. 재배치 때 다시 표시한다.
- 병합되어 다른 평면에 흡수된 평면과 추적 제한 상태를 배치 대상에서 제외한다. 레이캐스트 결과 중 가장 가까운 유효한 위쪽 수평 평면을 선택하며, 유효한 평면이 없으면 안내한다.
- 상태가 바뀔 때 평면 수·앵커 추적 상태를 기록하고, 터치 시 UI 차단·평면 충돌 실패·앵커 생성 결과를 기록한다. 세션 변경 로그에는 `notTrackingReason`을 추가했다.
- 씬과 프리팹을 재생성하지 않았다. 실물 바코드 판독·상품 매핑은 변경하지 않았다.
- Unity EditMode **26/26 PASS**, 실패·건너뜀 0, 종료 코드 0. `artifacts/placement-tests.xml`, `placement-tests.log`에 기록했다. 실제 패키지 컴포넌트의 기존 표시 충돌 재현, 수정 후 숨김 유지 4회·재활성 복구, 병합/추적 제한 평면 제외를 검사했다.
- 수정 APK ARM64·IL2CPP 빌드 성공, 오류 0, Unity 종료 코드 0. 기기에 동일 서명을 확인한 `install --user 0 -r` 업데이트 및 COLD 실행 성공. 빌드: `artifacts/placement-build.log`, `placement-build-summary.json`. 소스 대조: `placement-source-inventory.json`.
- 새 APK에서도 00:43:03.222 재스캔 이후 00:43:04.422 정확한 번호가 세 프레임 연속 확인됐고, 00:43:04.430 평면 감지 활성화, 다음 줄에 `AwaitingPlacement` 진입을 확인했다. 이 한 번의 관찰 시간은 약 1.20초이며 전체 성공률로 해석하지 않는다.
- 실제 평면 수는 0이었고 사용자는 선명한 책상을 여러 각도로 비춰도 초록색 표면이 나오지 않는다고 보고했다. 공간 추적은 `SessionTracking`/`SessionInitializing`을 빠르게 반복했다. native 로그에 IMU buffer 부족 및 최신 IMU timestamp보다 앞선 pose 요청 오류가 반복됐다.
- Android `dumpsys sensorservice`에서 해당 앱의 가속도계·비보정 자이로 요청과 실제 선택 주기가 **160,000µs (6.25Hz)**였고, 센서 접근은 허용된 상태였다. 절전 설정 `low_power=0`도 확인했다. 근거: `artifacts/device/placement-product-live.log`, `placement-sensors.txt`, `placement-product-screen.png`. 아래 센서 요청 비교 시험으로 이어졌으며 기기 결함으로 확정하지 않는다.
- 이 시험은 로컬 컴포넌트 시험이다. 실제 책상 평면 감지·기기 이동 중 앵커 고정·추적 소실/복구를 증명하지 않는다. 기기 시험 절차는 [DEVICE-CHECKLIST.md](DEVICE-CHECKLIST.md)를 따른다.

### 센서 요청 주기 비교 — 2026-10-24

가속도계와 비보정 자이로에 **10,000µs (100Hz)**를 55초간 추가 요청하고 해제하는 개발용 실험을 수행했다. 센서 값을 AR 자세에 주입하지 않았고 ARCore·Android 설정을 변경하지 않았다. `dumpsys sensorservice`의 선택 주기는 요청 중 10ms, 해제 후 160ms로 바뀌었다. 요청 주기는 보장된 실제 수신 주기가 아니라 시스템에 주는 힌트다([Android SensorManager](https://developer.android.com/reference/android/hardware/SensorManager)).

| 실험 | 요청 중 관찰 | 해제 후 수집 구간 |
|---|---|---|
| 1: 00:53:54.765–00:54:49.773 | IMU buffer 부족 0회, pose timestamp too new 0회. 추적 성립은 확인 못함 | 약 20.5초에 IMU buffer 부족 613회, pose timestamp too new 210회, AR 상태 전환 142회 |
| 2: 00:55:36.354–00:56:31.368 | 00:55:40.265부터 **26.571초** 연속 Tracking 후 `InsufficientLight`. 위 두 native 오류 0회 | 약 13.4초 동안 Initializing 유지. 오류 0회 |
| 3: 00:56:49.638–00:57:44.678 | Initializing 유지, 위 두 native 오류 0회 | 후속 관찰에서 빠른 상태 전환 재발. IMU buffer 부족 74회, pose timestamp too new 13회 |

각 행은 동일 기기의 개별 로그 구간이며 조명·구도·움직임을 완전히 통제한 실험이 아니다. 2회차의 개선은 **센서 데이터 공급 문제를 완화할 근거**지만 평면·앵커 성공이나 모든 환경에서의 안정성을 증명하지 않는다. 3회차 이후 사용자가 다른 앱으로 전환한 것도 확인했다. 센서 요청만으로 시각 특징이나 빛 부족을 해결할 수 없다.

근거: `artifacts/device/motion-probe-{ended,table-live,third}.log`, `motion-probe-active-sensors.txt`, `motion-probe-ended-sensors.txt`, `motion-probe-comparison.json`. 분석 스크립트는 `artifacts/analyze-motion-probes.py`다. 실험 APK는 `Builds/Android/ProductAR-motion-probe.apk`이며 SHA-256은 `07811bceddacef480f35535831496d571ad4e94e340e0c136cf1ccab0f7f0853`이다. 55초 뒤 끝나는 실험 코드는 최종 런타임에 남기지 않는다.

### 앱 수명주기에 연결한 센서 보완

- `ARReadiness`가 카메라 권한·AR 지원·세션 활성 상태를 확인해 센서 요청을 시작한다. 추적 초기화/복구 중에는 요청을 유지한다. 앱 pause, 컴포넌트 disable, 권한 회수 때 해제하고 복귀 시 다시 등록한다.
- `AndroidMotionSampling`은 Java 브리지의 수명과 오류 로그만 맡는다. Java `MotionSampling`은 앱 Context를 보관하고 가속도계·비보정 자이로 모두 등록돼야 성공한다. 부분 등록·예외는 해제한다. 빈 Java 콜백에서 센서 측정값을 읽거나 Unity로 전달하지 않는다.
- Java 소스는 Android 전용 PluginImporter로 빌드한다. [Unity Java 소스 플러그인](https://docs.unity3d.com/6000.3/Documentation/Manual/AndroidJavaSourcePlugins.html)을 사용하며 외부 라이브러리를 추가하지 않았다. 현재 Android minify는 꺼져 있다. 이후 켤 경우 C#에서 호출하는 JNI 클래스/메서드 보존 규칙을 추가하고 실기기 호출을 다시 검증해야 한다.
- 요청은 100Hz이며 200Hz 초과 접근을 위한 `HIGH_SAMPLING_RATE_SENSORS` 권한을 추가하지 않는다([Android 센서 주기 제한](https://developer.android.com/develop/sensors-and-location/sensors/sensors_overview#sensors-rate-limiting)). 센서 값과 공간 자세의 권위는 ARCore에 유지한다.
- 상품 확인 후 공간 추적이 준비되지 않았으면 표면 탐색과 구분해 복구 안내를 표시한다. 빛 부족·특징 부족·과도한 움직임은 현재 `notTrackingReason`에 맞춰 안내한다.
- Unity EditMode **26/26 PASS**, 실패·건너뜀 0, Unity 종료 코드 0. ARM64·IL2CPP APK 빌드 **Succeeded / errors=0 / exit=0**. Java 클래스가 APK DEX에 포함된 것을 확인했고, 기기에서 실제 두 센서 등록 성공으로 JNI 호출까지 확인했다. `artifacts/motion-sampling-tests.xml`, `motion-sampling-build.log`, `motion-sampling-build-summary.json`. 실행 코드·Java 플러그인·시험·씬·프리팹·데이터·패키지 등 72개 파일을 빌드 복사본과 SHA-256 대조했다(`motion-sampling-source-inventory.json`).
- 동일 서명·동일 버전의 `install --user 0 -r` 업데이트와 COLD 실행 성공. 첫 100Hz 요청은 01:10:31.736, 01:11:41.169 센서 서비스에서도 10ms 선택 유지. Home 전환 시 01:11:43.048 해제, 일시정지 덤프에는 이 앱의 추가 센서 연결이 없었으며 10ms 요청도 제거됐다. 01:11:45.672 HOT 복귀 시 두 센서를 다시 등록했고 10ms 선택을 확인했다. 근거: `artifacts/motion-sampling-installed.json`, `artifacts/device/motion-final-{persistent,paused,resumed}.log`, 대응 `*-sensors.txt`.

### 수정 앱의 첫 실제 공간 배치 — 사용자·로그 확인

- 01:12:02.643 `SessionTracking` 진입.
- 01:12:12.176 실물 `8801104671507`을 서로 다른 카메라 프레임 세 번으로 확정. 01:12:12.183 `AwaitingPlacement` 진입.
- 01:12:16.189 사용 가능한 위쪽 수평 평면 **1개** 확인.
- 01:12:19.415 터치로 **World panel placed** 및 실제 평면·앵커 ID 기록, 다음 프레임 `Placement tracking=True` 확인.
- 사용자가 **“초록색 표면이 보이고 정보판도 나옴”**이라고 확인했다. 화면 고정 UI나 가상 좌표 주입으로 성공을 대체하지 않았다.
- 01:12:35 앱 일시정지까지 수집한 해당 PID 로그에서 IMU buffer 부족 및 `GetRecentDevicePose: Failed` 오류는 **0회**였다. 전체 환경에서 오류가 없다는 의미는 아니다.
- 앱 복귀 후 01:12:41.098 Tracking 회복, 01:12:50.547 수평 평면 재감지, 01:12:53.760 새 앵커로 두 번째 정보판 배치와 추적 성공을 확인했다. 사용자는 휴대폰을 좌우로 20~30cm 천천히 이동하는 확인 요청에 **“책상의 같은 위치에 유지됨”**이라고 답했다. 이동 거리·드리프트를 도구로 정량 측정한 것은 아니다.
- 근거: `artifacts/device/motion-final-live.log`, `motion-final-placement-followup.log` 및 사용자 확인. **스캔→조회→평면 감지→터치 배치→이동 중 위치 유지**의 기본 E2E가 실기기에서 확인됐다. 10회 배치·재배치·삭제, 1분 이상 연속 고정, 추적 소실/복구 및 5분 부하 시험은 미완료다.

## 단계별 판정

| 단계 | 구현·준비 | 수행한 검증 | 남은 조건·판정 |
|---|---|---|---|
| M0 | 환경, 실기기, 정확한 상품 데이터, demo-only 매핑 준비 | 지정 에디터/패키지, ARCore 설치 기기, 사용자 제공 실물 정보와 기존 정확 ID 근거 대조, 로컬 스냅샷 검증 | **준비 완료**. 운영 바코드 매핑은 UNVERIFIED 유지 |
| M1 | 최소 AR 씬·Android 설정·권한/지원 안내·수명주기 센서 보완 | 수정 앱 실제 Tracking·평면 감지·앵커 생성, 센서 Home 해제/복귀 재등록 | **기본 실행 확인**. 권한 거부/재허용 전체 경로·장시간 안정성 미완료 |
| M2 | 동일 AR 카메라 EAN-13 판독·회전·재시도·해제 | Editor 합성 판독, 기기 IL2CPP 합성 정방향/90도 판독, 실제 AR CPU 이미지 R8 변환 | **PARTIAL**. 2026-10-24 사용자 실물 판독·상품명 표시 성공 확인. 10회 판독·시간/실패 조건 미검증 |
| M3 | 평면 터치·앵커·한 개 배치·재배치·삭제·추적 중단 숨김 | 실제 평면·두 차례 앵커 생성·정보판 배치 로그, 이동 중 같은 위치 유지 사용자 확인 | **기본 E2E 성공 / PARTIAL**. 1분 연속 고정·소실/복구·10회 반복 미완료 |
| M4 | 정확한 바코드 매핑·로컬 JSON 조회 | 정확 ID·다른 바코드·중복/잘못된 연결·가격·영양 기준량·null/0 시험, 실물 판독부터 정보판 배치까지 확인 | **로컬 검증 완료 / 기본 E2E 연결 확인**. 정량 반복 시험 별도 |
| M5 | 한국어 AR 정보판·상태·조작 UI | 카메라 UI·다시 스캔, 한국어 글리프·정보판 잘림 시험, 실제 배치된 정보판 사용자 확인 | **PARTIAL**. 전체 예외 UX 미검증 |
| M6 | 스캔 제한·버퍼 재사용·작업 세대 분리·자원 해제·상태 가드 | 자동 시험, 기기 실행·CPU 획득·70초 동일 PID/오류 관찰 | **PARTIAL**. 실물 통합 10회·5분 부하·오프라인·백그라운드/추적 소실 시험 미완료 |
| M7 | 보고서 초안·시험 체크리스트·영상 구성안 | 강의 항목·현재 결과·AI 활용 구분 문서화 | **PARTIAL**. 실제 데모 영상·학번/이름·제출 공지 반영 필요 |

## 환경과 산출물

- Unity **6000.3.22f1 LTS**, AR Foundation·ARCore XR Plugin **6.3.5**.
- ZXing.Net **0.16.11** 공식 NuGet의 `netstandard2.0` DLL. `System.Drawing` 바인딩을 사용하지 않는다. Apache-2.0 전문·출처·해시는 `Assets/Plugins/ZXing/`에 보존했다.
- Unity Newtonsoft JSON **3.2.2** 공식 패키지. Nullable 숫자로 정보 없음과 0을 구분한다.
- Noto Sans CJK KR 정규 폰트와 SIL OFL 라이선스: `Assets/Fonts/`. 생성 SDF와 한국어 글리프를 포함한다.
- 실행 씬: `Assets/Scenes/ProductAR.unity`. 프리팹: `Assets/Prefabs/ProductInfoPanel.prefab`, `PlacementPlane.prefab`.
- 앱: **상품정보 AR**, package `com.yeonsik.midtermar`, versionName **0.1.0**, versionCode **1**.
- Android: minimum API **30**, target API **35**, **ARM64·IL2CPP**, OpenGLES3, 세로 방향.
- 대상 기기: **Samsung Galaxy A25 / SM-A256N**, Android **16**. Google Play Services for AR **1.57.262640093** 설치 확인. 기기 serial·원문 로그·화면은 Git 제외 로컬 artifacts에 보관한다.

### 최종 APK

- 경로: `Builds/Android/ProductAR.apk` — Git 제외.
- 크기: **85,699,362 bytes** (약 81.7 MiB).
- SHA-256: `b43d8289bb5b9e1fca110d7896a6c65646c1beb6b1795aa371488606f2327b93`.
- Development 빌드다. 에디터 테스트 어셈블리를 플레이어에 포함하지 않았다. 개발 빌드에서만 합성 바코드 자체 점검을 한 번 수행한다.
- 서명 SHA-256: `cad84c2b4d6f3ad776d880794e00333953840746fb27424531c9f5a6e2846135`.
- 최초 새 패키지 설치 후 최종 APK는 동일 서명·동일 버전을 확인한 `install --user 0 -r`로 업데이트했다. uninstall·`pm clear`·다운그레이드는 사용하지 않았다. 다른 앱의 패키지·데이터는 변경하지 않았다.

## 초기 구현 자동 시험 — PASS (최신 후속 수정은 26개)

명령: 지정 Unity 실행 파일의 `-batchmode -nographics -buildTarget Android -runTests -testPlatform EditMode -assemblyNames MidtermAR.Tests -testResults <결과 XML> -logFile <로그>`.

최종 결과: **19개 통과 / 실패 0 / 건너뜀 0**, Unity 종료 코드 **0**. 근거: `artifacts/editmode-results-camera.xml`, `artifacts/editmode-tests-camera.log`. 최종 Editor 설정 수정 후에도 **19/19 통과, Unity exit 0**을 재확인했다. 전달 점검 결과는 `artifacts/editmode-results-delivery.xml`, `artifacts/editmode-tests-delivery.log`에 별도로 보관한다.

| 범위 | 시험 내용 |
|---|---|
| 카메라와 공간 추적 분리 | `SessionInitializing`에서도 바코드 판독 준비 가능, 배치에는 `SessionTracking` 필요 |
| EAN-13 Identity | 공백 정리, 길이·ASCII 숫자·체크섬 검증, 선행 0 보존 |
| 카탈로그 | 정확 ID·demo-only 매핑·가격·날짜·영양 값, 다른 바코드의 미조회 |
| 누락값 | null과 0, 가격/영양 객체 전체 없음의 표시 |
| 잘못된 데이터 | 중복 바코드/ID, 다른 상품의 Nutrition 링크, 음수 가격 거부 |
| 상태 | 반복 스캔 무시, 재배치, 다시 스캔 시 이전 상품 제거, 시연 매핑 없음, 시간 초과 |
| 디코더 | 생성한 EAN-13의 정방향·90도 회전 판독 |
| 씬·UI | 단일 AR Session·TrackedPoseDriver·필수 참조·실행 씬, 한국어 글리프와 정보판 넘침 |

첫 시험에서는 15개 중 13개가 통과했다. 테스트가 발견한 프리팹 참조 누락과 가격 영역 높이를 수정한 뒤 15개가 통과했고, 카메라/공간 준비 분리 회귀 시험 4개를 추가해 최종 19개를 통과했다. 합성 디코더 시험을 실물 카메라 정확도로 해석하지 않는다.

## 초기 Android 빌드·설치 — PASS (후속 재빌드·설치는 수정 기록 참조)

1. 지정 에디터의 `MidtermAR.Editor.ProjectBuilder.PrepareProject`로 씬·프리팹·한국어 SDF를 생성했다.
2. 원본 한글 경로에서 Android 빌드는 `Invalid project path ... contains non-ASCII characters`로 실패했다. 원본을 이동하지 않고 영문 임시 디렉터리의 복사본에서 빌드하도록 `scripts/Build-Android.ps1`을 작성했다.
3. 최초 빌드 후 카메라 판독을 월드 추적 완료와 분리하고 개발용 플랫폼 자체 점검을 추가했다. 최종 입력을 동일 영문 빌드 복사본에 갱신해 재빌드했다.
4. 최종 `ProjectBuilder.BuildAndroid`는 **Succeeded / errors=0 / Unity exit 0**. 실제 압축 APK 크기는 위 파일 크기를 기준으로 한다. Unity의 `summary.totalSize`를 APK 파일 크기로 사용하지 않는다.
5. `scripts/Install-Android.ps1`은 영문 APK 복사본으로 package·버전·서명을 검사한다. aapt도 한글 경로를 거부하므로 같은 우회가 필요했다.
6. APK 설치는 성공했다. 일반 MAIN/LAUNCHER 패키지 지정 실행은 이 기기에서 해석되지 않아, 확인한 `com.unity3d.player.UnityPlayerActivity`를 명시해 실행했다. 스크립트에 이를 반영했다.

근거: `artifacts/android-build-summary.json`, `android-build-final.log`, `source-build-inventory.json`, `artifacts/install/result.json`. 실제 실행 명령과 재현 절차는 [기기 체크리스트](DEVICE-CHECKLIST.md)를 참조한다.

실패한 원본 경로 빌드가 이동해 둔 XR Simulation 임시 설정은 기존 HEAD와 바이트 내용이 같은 것을 확인해 원래 위치와 GUID로 복구했다. 최종 전달 점검에서는 실행 씬의 빌드 목록 GUID를 복구하고, 씬 생성 뒤 동기 import와 목록 갱신을 추가했다. 최종 APK와 현재 소스의 차이는 이 Editor 전용 처리·빌드 목록 GUID·XR Simulation 설정 복구 및 빈 임시 폴더 정리뿐이다. 앱 실행 코드·상품 JSON·실행 씬·정보판 프리팹은 최종 APK 입력과 SHA-256으로 일치함을 확인했다. ARCore 패키지 캐시를 직접 수정하지 않았다. PackageCache C#의 Serena 심볼 추출은 실패해 해당 API만 범위를 제한한 파일 검색과 공식 문서로 확인했다.

## 초기 구현 당시 실기기에서 확인한 범위

- 설치·Activity 실행 성공, 한국어 안내·버튼과 실제 카메라 영상 표시.
- 로컬 카탈로그 1개 로드. 매핑은 demo-only이며 실물 판독은 대기로 기록.
- 카메라 권한은 설치 스크립트의 ADB 부여로 확인했다. 사용자 권한 팝업의 거부/재허용 경로 전체를 시험한 것은 아니다.
- AR Session: `CheckingAvailability → Ready → SessionInitializing`. 관찰 중 `SessionTracking`은 확인하지 못했다.
- 실제 AR 카메라 CPU 이미지 변환: **640×480, R8**. 별도 카메라 세션이나 사진 파일 입력으로 대체하지 않았다.
- Android IL2CPP 자체 점검: **합성 EAN-13 정방향·90도 회전 PASS**. 실제 제품 디코딩은 NOT VERIFIED.
- 다시 스캔 버튼으로 스캔 상태 복귀 확인.
- 70.23초 간격의 관찰에서 동일 앱 PID 유지, 수집한 앱 로그에 FATAL/NullReferenceException 없음. 이는 완전한 무오류·성능·5분 통합 시험을 뜻하지 않는다.

근거: `artifacts/device/final-launch-summary.json`, `final-launch-logcat.txt`, `final-screen.png`, `stability-samples.json`, `stability-logcat.txt`. 공간 정보판의 별도 Editor 렌더링은 `artifacts/ui/world-panel-preview.png`이며 실물 공간 배치 성공 화면이 아니다.

## 남은 사용자 개입과 완료 조건

1. 기본 실물 판독·상품명·표면 터치·이동 중 위치 유지는 사용자와 기기 로그로 확인했다. 정해진 조건의 실물 바코드 10회 반복을 기록한다.
2. 10회 배치·재배치·삭제, 최소 1분 연속 고정·추적 소실/복구, 5분 부하·오프라인 시험을 수행한다. 절차는 [DEVICE-CHECKLIST.md](DEVICE-CHECKLIST.md)를 따른다.
3. 실제 영상과 정량 결과, 학번·이름을 [보고서 초안](REPORT-DRAFT.md)에 반영한다.

남은 검증 때문에 독립적인 코드·씬·데이터·보고서 초안 작성을 중단하지 않았다. 실제 환경을 제공하기 전에는 M1~M3·M5~M7 전체 PASS나 최종 제출 완료로 표시하지 않는다.
