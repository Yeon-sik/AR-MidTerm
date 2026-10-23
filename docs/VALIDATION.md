# 기획 v2 구현·검증 기록

확인일: **2026-10-23 KST**. 소스 경계는 `feat/ar-foundation-m0-m3`의 구현 커밋 `b949cf7`이다. 초기 조사 기준은 `45c7037`이며 문서는 별도 커밋으로 기록한다. 제품 사진·HTML 등 사용자 입력은 변경하지 않았다. Git 원격 생성·push·배포는 수행하지 않았다.

**후속 수정:** 단발성 판독 확정 방지·미등록 번호 표시를 적용했다. 최신 자동 시험은 **23/23 PASS**이며 수정 APK 설치·실행까지 완료했다. 실물 재시험은 미완료다. 상세 원인·새 로그·한계는 [바코드 수정 기록](BARCODE-VALIDATION.md)을 따른다. 아래 초기 구현 검증 19개와 구분한다.

사용자가 사용자 개입 없이 가능한 후속 구현을 계속하도록 지시했으므로 **구현 완료·자동 시험·기기 실행·실물 검증을 분리**한다. 기기의 존재나 APK 설치만으로 실제 상품 인식·공간 배치의 PASS를 주장하지 않는다.

## 단계별 판정

| 단계 | 구현·준비 | 수행한 검증 | 남은 조건·판정 |
|---|---|---|---|
| M0 | 환경, 실기기, 정확한 상품 데이터, demo-only 매핑 준비 | 지정 에디터/패키지, ARCore 설치 기기, 사용자 제공 실물 정보와 기존 정확 ID 근거 대조, 로컬 스냅샷 검증 | **준비 완료**. 운영 바코드 매핑은 UNVERIFIED 유지 |
| M1 | 최소 AR 씬·Android 설정·권한/지원 안내 | APK 설치·기기 카메라 영상·권한·AR Session 초기화·동일 프로세스 70초 관찰 | **PARTIAL**. 관찰 시 `SessionInitializing`; 충분한 시각 특징과 움직임을 제공한 `SessionTracking` 확인 필요 |
| M2 | 동일 AR 카메라 EAN-13 판독·회전·재시도·해제 | Editor 합성 판독, 기기 IL2CPP 합성 정방향/90도 판독, 실제 AR CPU 이미지 R8 변환 | **PARTIAL**. 실물 더단백 10회 판독·시간/실패 조건 미검증 |
| M3 | 평면 터치·앵커·한 개 배치·재배치·삭제·추적 중단 숨김 | 컴파일·씬 참조·프리팹·정보판 렌더링 자동/시각 확인 | **PARTIAL**. 실제 평면·앵커·고정·소실/복구 10회 미검증 |
| M4 | 정확한 바코드 매핑·로컬 JSON 조회 | 정확 ID·다른 바코드·중복/잘못된 연결·가격·영양 기준량·null/0 시험, 기기 카탈로그 로드 | **로컬 검증 완료**. 실제 카메라 판독에서 이어지는 E2E 미검증 |
| M5 | 한국어 AR 정보판·상태·조작 UI | 실제 기기 카메라 UI·다시 스캔, 한국어 글리프·정보판 잘림 시험·Editor 렌더링 | **PARTIAL**. 실제 배치된 정보판·전체 예외 UX 미검증 |
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
- 크기: **85,710,171 bytes** (약 81.7 MiB).
- SHA-256: `5460a6fb04bcc0ab3891f540959b1b66480fa9ed21efe43aaae5e95021a5d0a4`.
- Development 빌드다. 에디터 테스트 어셈블리를 플레이어에 포함하지 않았다. 개발 빌드에서만 합성 바코드 자체 점검을 한 번 수행한다.
- 서명 SHA-256: `cad84c2b4d6f3ad776d880794e00333953840746fb27424531c9f5a6e2846135`.
- 최초 새 패키지 설치 후 최종 APK는 동일 서명·동일 버전을 확인한 `install --user 0 -r`로 업데이트했다. uninstall·`pm clear`·다운그레이드는 사용하지 않았다. 다른 앱의 패키지·데이터는 변경하지 않았다.

## 초기 구현 자동 시험 — PASS (최신 후속 수정은 23개)

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

## 실기기에서 확인한 범위

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

1. 휴대폰 카메라에 실물 바코드 `8801104671507`을 보여준다. 판독된 상품명·규격과 실제 제품이 같은지 확인한다.
2. 상품 옆의 특징이 있는 수평 표면을 여러 각도에서 비춰 공간 추적을 확보하고, 표면을 터치한다.
3. 휴대폰을 움직여 정보판의 공간 고정·재배치·삭제·추적 소실을 시험한다. 자세한 10회 기록과 통합 시험은 [DEVICE-CHECKLIST.md](DEVICE-CHECKLIST.md)를 따른다.
4. 실제 영상과 측정 결과, 학번·이름을 [보고서 초안](REPORT-DRAFT.md)에 반영한다.

남은 검증 때문에 독립적인 코드·씬·데이터·보고서 초안 작성을 중단하지 않았다. 실제 환경을 제공하기 전에는 M1~M3·M5~M7 전체 PASS나 최종 제출 완료로 표시하지 않는다.
