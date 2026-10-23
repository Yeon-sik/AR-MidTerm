# 실물 바코드 재시험과 판독 해상도 분석

확인일: 2026-10-23 KST. 기준 소스는 `1c0dce9`의 3회 연속 확인 구현이며, 이번 작업은 그 후속 수정이다.

## 기기에서 확인한 사실

- 등록된 정확한 바코드는 `8801104671507`이다. 사용자가 비춘 실물 포장 숫자와 카메라 미리보기에서 일치함을 확인했다.
- 19:27:21에는 이 번호가 3회 확인되어 `AwaitingPlacement`로 진입했다. 상품 조회 연결은 동작했다.
- 19:27:32에는 `7801181671507`이 3회 반복되어 `UnknownBarcode`로 잘못 확정됐다. 프레임을 반복 확인하는 것만으로 같은 영상 왜곡에 의한 오독을 제거하지 못했다.
- Android 카메라 서비스의 해당 앱 스트림은 GPU 미리보기 **1920×1080**, CPU 영상 **640×480**이었다. 앱의 실제 CPU 변환 로그도 **640×480 R8**이었다.
- 카메라 요청은 `CONTINUOUS_VIDEO` 자동초점이었다. 자동초점이 꺼져 있다는 근거는 없다. 특정 촬영 순간의 초점 성공까지 증명한 것은 아니다.
- 반복적인 `SessionTracking`/`SessionInitializing` 전환과 URP frustum 오류가 있었다. 개발 콘솔이 조작 버튼 위에 표시되는 것도 실기기 화면에서 확인했다. 공간 추적 오류의 근본 원인은 별도 조사 대상이다.

원문: `artifacts/device/barcode-retry-logcat.txt`, `barcode-retry-camera.txt`, `live-barcode-screen-2.png`. 개인 기기 원문과 실물 영상은 Git에서 제외한다.

## 같은 실물 미리보기의 크기별 재생 실험

확보한 화면 PNG를 동일 ZXing 디코더로 처리했다. 화면을 축소한 실험이므로 실제 저해상도 CPU 프레임과 완전히 동일한 입력이라고 주장하지 않는다. 미리보기의 품질과 판독 입력 해상도의 차이를 확인하는 통제 실험이다.

| 실험 입력 크기 | 판독 결과 |
|---|---|
| 295×640 | 판독 없음 |
| 443×960 | `8801104671507` |
| 590×1280 | `8801104671507` |
| 886×1920 | `8801104671507` |
| 원본 1080×2340 | `8801104671507` |

이 결과와 실제 640×480 CPU 스트림은 **판독 영상의 세부 픽셀 부족**을 주요 원인으로 뒷받침한다. 화면에서 보이는 선명함만으로 디코더에 들어간 영상 품질을 판단할 수 없다. 체크섬 통과와 3회 반복만으로도 진위를 보장할 수 없었다.

근거: `artifacts/screen-resolution-analysis.json`, `resolution-analysis.log`. 재생 도구는 `Assets/Editor/BarcodeImageAnalysis.cs`이며 입력 PNG 또는 진단용 PGM을 여러 크기로 비교한다.

## 구현한 개선

- `ARCameraManager.GetConfigurations()`에서 실제 지원하는 CPU 영상 설정을 조회하고, 최대 1920×1080 안에서 가장 상세한 설정을 고른다. 해상도가 같으면 30 fps를 우선한다. 지원하지 않는 기기에서는 기존 설정으로 동작하며 로그를 남긴다.
- 디코더 입력 제한도 긴 변 960에서 **1920**으로 올려 카메라가 제공한 세부 정보를 다시 줄이지 않게 했다. 가짜 픽셀 확대는 하지 않는다.
- `XRCpuImage.timestamp`가 이전과 같은 중복 프레임은 판독하지 않는다. 3회 확인은 새로운 카메라 영상에 기반한다.
- 개발 빌드에서만 한 스캔의 첫 8개 Gray8 입력과 판독 번호·소요 시간·원본 timestamp를 앱 로컬에 저장한다. 다음 스캔에서 같은 8개 슬롯을 덮어쓰며 네트워크로 전송하지 않는다.
- 개발 콘솔이 조작 UI를 가리지 않도록 표시를 비활성화했다. 오류 로그는 유지하며, frustum 오류 자체가 해결됐다고 주장하지 않는다.

API 근거: Unity 6.3 [XRCameraConfiguration](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.3/api/UnityEngine.XR.ARSubsystems.XRCameraConfiguration.html)은 CPU 이미지의 해상도를 결정한다. 로컬 ARCore 6.3.5 구현의 설정 변경 및 이미지 해제 조건도 확인했다.

## 현재 검증

- Unity EditMode **24/24 PASS**, 실패·건너뜀 0, 종료 코드 0. 로그: `artifacts/resolution-fix-tests-final.xml`, `resolution-fix-tests-final.log`.
- Android 첫 재빌드는 임시 복사본의 불완전한 패키지 캐시 때문에 실패했다. 원본에서 테스트를 통과한 동일 버전 캐시를 복사하고, 이전 불완전한 캐시는 별도 보존했다. 복구 후에도 임포트 인덱스가 정상 반영되지 않아 재현용 `scripts/Build-Android.ps1`으로 새 영문 복사본을 만들었고, 새 복사본에서 빌드를 완료했다. 원본 패키지 코드는 수정하지 않았다.
- Android ARM64·IL2CPP 빌드 **Succeeded / errors 0 / Unity exit 0**. 기기에 동일 서명·동일 versionCode를 대조한 `install --user 0 -r` 업데이트 및 COLD 실행 성공. 삭제·초기화는 수행하지 않았다.
- 지원 CPU 설정은 **640×480 / 1280×720 / 1920×1080**, 모두 30 fps였다. **1920×1080을 선택했고 실제 CPU 입력과 디코더 출력 모두 1920×1080 R8**, autofocus=True를 로그에서 확인했다.
- 실제 저장한 첫 8개 CPU 프레임 모두 1920×1080이고, sourceTimestamp도 8개 모두 달랐다. 픽셀 수는 이전 640×480의 **6.75배**다.
- 바코드가 판독되지 않은 초기 8개 프레임의 Android 디코더 시간은 최소 114.1ms·중앙값 132.2ms·최대 304.4ms였다. 한 개 작업만 실행하므로 판독 작업을 누적시키지 않는다. 이 값은 전체 앱 FPS·성능 보증이나 제품이 잘 비춰진 조건의 판독 시간은 아니다.
- 21:23에 대상 앱이 전경인 것을 확인하고 재스캔을 1회 실행했다. 화면에는 바코드가 없었으며 새 CPU 영상에서도 판독 결과는 없었다. 근거: `artifacts/device/resolution-retry.log`, `highres-retry-frames/`, `highres-retry-screen.png`. 이 결과를 제품 판독 실패율로 계산하지 않는다.
- 고해상도 상태의 실제 제품 반복 시험은 아직 대기 중이다. 이전 구현의 성공 사례나 해상도 증가만으로 안정적인 판독 완료를 주장하지 않는다.

## 전달 산출물

- APK: `Builds/Android/ProductAR.apk`, **62,130,684 bytes**.
- SHA-256: `568511e7a379e287758112563dd941286c37dcc1f1f0d077d2d337b40f21f60b`.
- 빌드 근거: `artifacts/android-build-ascii.log`, `android-build-summary.json`, `resolution-fix-source-inventory.json`.
- 설치·기기 근거: `artifacts/install/result.json`, `artifacts/device/resolution-startup.log`, `highres-startup-frames/`.
- 현재 런타임 소스·상품 JSON·실행 씬은 실제 빌드 입력과 바이트 단위로 일치함을 확인했다.
- 기존의 빠르게 변하는 AR 추적 상태·frustum 오류·실제 공간 배치의 안정성은 별도 미완료 항목이다. 개발 콘솔 표시를 끈 것을 원인 해결로 해석하지 않는다.
