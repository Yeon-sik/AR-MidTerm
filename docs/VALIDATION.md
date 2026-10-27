# 상품정보 AR 구현·검증 기록

## 2026-10-27 촬영 전 UI P0·0.6.2 release

구현 소스 **b79fc63**, 브랜치 **feat/ui-p0-video**, 기준 **2dd295a**. 인식·온라인 조회·충돌·소실 규칙은 유지하고 표시·문구·생성 씬·빌드 옵션을 보완했다. 기존 제출 태그와 세 브랜치는 이동·병합하지 않는다.

- 상품 공통 폭 약 58mm와 크롭 오프셋으로 오른쪽 12mm 간격·이미지 평면 기준 고정 15° 정보판을 배치한다. 폭 92.8mm, 선택 크롭 윤곽과 짧은 연결선 하나를 함께 표시한다. 화면 경계·카메라 방향에 따른 자동 이동은 없다.
- 데이터에 따라 영양/가격/탭 없음 기본값을 고르고, 없는 탭은 터치를 무시한다. 0은 실제 값으로 유지한다. 내용에 맞춰 높이를 줄이며 2D도 빈 영역을 숨긴다. 정보판 안의 재스캔과 이벤트 연결을 제거하고 기존 하단 초기화 경로를 유지한다. 가격 날짜·출처·현재가 주의와 개발용 카드·PT 확인 불가/연결 필요 표시는 남긴다.
- ProjectBuilder로 씬·프리팹·선 재질을 재생성했다. `scripts/Test-EditMode.ps1` **194/194 PASS**, 기존 172개 유지 + 신규 22개, 실패·건너뜀 0, Unity exit 0. 기하·공통 원점·공통 폭 근거·같은 호출 내 세 시각 요소 숨김·기본 탭·비활성 포인터·누락/0·문구·중복 버튼을 확인했다. 첫 실행의 포인터 2개는 상품 바인딩을 생략한 기존 시험 입력을 보완했다. 최종 근거: `artifacts/editmode/20261026-164234-d8afbdc2/summary.json`.
- **릴리스 APK 0.6.2 (9):** BuildOptions.None, ARM64·IL2CPP, 오류 0·Unity exit 0, **52,906,964 bytes**, SHA-256 **b1423bc0989f30dee33a6709fa0834ef73a48d14d2a6e4f9eeb0afc532a28f2e**. 실제 manifest에 debuggable 없음, IL2CPP metadata에 DevelopmentLookupCheck·DevelopmentDecoderCheck 타입 없음, 분할 sharedassets0.assets를 합친 에셋에서 온라인 로컬 설정 포함을 확인했다. 빌드 로그의 실제 키·연락처 일치 0건이다.
- **설치:** 기존 0.6.1 (8)과 인증서 SHA-256 **cad84c2b4d6f3ad776d880794e00333953840746fb27424531c9f5a6e2846135** 일치, A25에서 compatible-update-r 성공·Activity 시작 응답 ok. 설치 APK를 다시 가져와 해시 일치, 버전 0.6.2 (9), 설치 패키지의 DEBUGGABLE 부재를 확인했다. 앱 삭제·데이터 초기화는 하지 않았다. 이후 확인 시 앱 프로세스가 실행 중이 아니어서 새 실행 로그의 오류/키 노출 검사는 NOT_RUN이며 크래시 여부를 단정하지 않는다.
- **실물 미수행:** 사용자가 ‘지금 실물 시험 불가’라고 답했다. 정면·약 30°·약 20/50cm·가격 탭·가림·더단백 바코드 캡처, 실제 정합·터치·워터마크 시각 확인, 대표 캡처 2장, 최소 5회 반복·5분 부하는 모두 NOT_RUN이다. 설치 성공이나 이전 버전 확인을 새 UI의 실물 성공으로 기록하지 않는다. [ui-p0-2026-10-27.json](validation/ui-p0-2026-10-27.json)에 소스·산출물 검사·설치와 미수행을 분리하고, [체크리스트](DEVICE-CHECKLIST.md)에 촬영 절차를 남겼다.

## 2026-10-27 온라인 표시 마무리·제출 원본·0.6.1

**시연 범위는 테아닌 앞면 AR 1종과 더단백·테아닌 바코드 식별 2종이다.** 테아닌 두 크롭은 같은 상품이며, 더단백 포장 앞면 AR과 여러 상품의 동시 표시는 구현 범위가 아니다. 개발용 브이젤 카드는 별도 시험이다.

- 검증 소스 `70d73e7545a103bca60954fc9132c3f2cd04fb49`. 온라인 DTO를 Selected에 넣지 않고 `DisplayProduct`·`DisplayProductKey`로 버튼 동작·상품 변경·패널 초기화를 결정한다. 소실 시 현재 표시 상태를 해제한다. 기존 같은 상품 탭 유지 규칙은 보존한다.
- `scripts/Test-EditMode.ps1` **172/172 PASS**, 기존 162개 유지 + 신규 10개, 실패·건너뜀 0, Unity exit 0. 온라인 전용 상품 버튼·두 패널 초기화·loss/pause/rescan, 설정 에셋 없음의 실제 Awake·로컬 바코드, PT conflict 후보 0/1/2개와 후속 공급자 호출 차단을 확인했다. 근거: `artifacts/editmode/20261026-160623-0906ab89/summary.json`.
- PT 계약은 초기 `835e281`에서 최종 `d7c1d33`으로 변경됐다. 최종 확정 계약은 `conflict + []`를 가려진 충돌로 허용하고 후보 1개는 허용하지 않는다. 파서가 새 규칙을 따르며 충돌에서 영양·OFF를 호출하지 않는다. 이 작업은 PT DB나 원격 함수를 변경하지 않았다.
- `scripts/Test-SubmissionPackage.ps1` **10/10 PASS**. `53e3a3e`의 실제 후보 ZIP은 980개 파일·키 패턴 0건·제외 경로 0건으로 검사를 통과했다. 최종 제출 ZIP은 고정 태그에서 다시 생성·검사하며 ZIP의 SUBMISSION-MANIFEST가 정확한 커밋을 보존한다. [제출 절차](SUBMISSION.md).
- 0.6.1 첫 APK `9828fe59…d8fcad4a`는 동일 서명의 `-r` 업데이트·버전 8·설치 해시 일치·카메라·인터넷 권한을 확인했다. 사용자는 더단백 상품정보·다른 상품 스캔 버튼·테아닌 앞면 3D가 정상이라고 확인했다. 기기 로그에서 Barcode2D→None·Image3D→None을 확인했고, 수집 구간의 NullReferenceException/NoSuchMethodError/FATAL EXCEPTION은 0건이다. 뒤이은 PT 계약 파서 보완 APK와 구분한다.
- **최종 계약 보완 APK:** 0.6.1 (8), ARM64·IL2CPP, 빌드 Succeeded / errors=0 / exit=0, **62,727,523 bytes**, SHA-256 **`744f7c8ce22854a0e3fe764141f74cdb50d111f3cda275bb42bea051485e5280`**. A25에 동일 서명 `-r`로 업데이트하고 설치 APK를 다시 가져와 해시 일치를 확인했다. 앱 삭제·데이터 초기화 없음. 빌드 입력 소스·시험 44개 파일의 원본/복사본 해시 불일치 0건이다.
- 최종 APK의 실제 클라이언트 진단은 **PT matched / 영양 found / OFF found / User-Agent wire-match**다(`artifacts/online-finish/final-device.log`). 최종 파서 보완 뒤의 재실물·온라인 전용 신규 상품·운영 redacted conflict는 미수행이다. 직전 같은 버전의 기본 실물 사용자 확인과 자동 시험을 구분한다.
- **미수행:** 테아닌 앞면의 통제된 최소 5회 회전·거리·가림·재획득, 온라인 전용 새 상품의 실제 바코드, 가려진 충돌의 실제 운영 응답, 5분 부하·모든 추적 상태 조합. 자동 시험·이전 버전 사용자 확인을 새 반복 시험 결과로 채우지 않는다. [빈 기록 양식](DEVICE-CHECKLIST.md)을 준비했다.
- 최신 빌드·설치·성공/미수행 상세: [online-lookup-finish-2026-10-27.json](validation/online-lookup-finish-2026-10-27.json). 이전 0.6.0 결과는 아래 역사 기록이다.

## 2026-10-27 바코드 온라인 조회

구현 소스 `389f9e9837cb82d16b0695038705051e16f4ab56`, 브랜치 `feat/barcode-online-lookup`. 공개 조회 클라이언트·상태·캐시·설정·UI를 추가했다. 기존 EAN-13 3회 확인·최대 0.65초·실패 프레임 즉시 해제·독립 FULL_TRACKING 경로를 유지한다. [설정·실행·중단 절차](ONLINE-LOOKUP.md).

- `scripts/Test-EditMode.ps1`: **162/162 PASS**, 실패·건너뜀 0, Unity exit 0. 기존 105개 + 신규 57개. 근거: `artifacts/editmode/20261026-152229-e25f6a71/{results.xml,editor.log,summary.json}`. 최초 159/162의 기존 가격 문구·배지 높이 문제 3건을 수정한 뒤 전체 통과했다.
- 가짜 응답: PT 연결/not_linked/conflict/invalid, 401·429·404·5xx·취소/시간초과·계약 오류, OFF 발견/미발견/오류와 캐시 의미, A→B·소실·pause·재스캔·캐시·요청 합치기, 앞면과 온라인 PT ID 일치/불일치, 설정 없음·로컬 즉시 표시 후 갱신을 확인했다.
- PC 읽기: 초반 PT 바코드 함수는 **404/PGRST202**였고, **00:27 이후 A25·PC에서 200/matched로 바뀌었다.** 두 시연 EAN의 정확 PT ID가 일치한다. 공개 가격은 null이며 로컬 관측을 사용한다. 공개 카탈로그 v4 **200/107개**·테아닌 PT ID 존재, 더단백 영양 **200/1건**, 테아닌 영양 **200/0건**, OFF v3 더단백·Nutella **200/product_found**. 사용자 제공 전일 카탈로그 200/102개와 시점을 구분한다. 키·연락처는 검증 기록에 저장하지 않는다.
- 사용자 동일 상품 확인에 따라 테아닌 ID·두 크롭 연결·관련 시험을 갱신했다. 관측 revision `e6e6315772fcfc71`에서 2026-05-08 바다마을마트(low)·2026-07-14 와마트 일산점(user_verified)의 13,210원 두 건을 확인했다. 대표 가격은 후자이며 현재가로 표현하지 않는다. 영양은 비워 뒀다.
- Android ARM64·IL2CPP 빌드 **Succeeded / errors=0 / exit 0**, 버전 **0.6.0 (7)**, 인터넷 권한 Require. 최종 APK **66,425,752 bytes**, SHA-256 **`c6c12a607c4598de4c2d8b510e616ed6915dfb8554c88f53269267706660f6f4`**. Galaxy A25 / SM-A256N / Android 16에 동일 서명의 `install --user 0 -r`로 업데이트하고, 설치 APK를 다시 가져와 해시 일치를 확인했다. 앱 삭제·데이터 초기화 없음. 실행·Editor·데이터 소스 37개와 빌드 복사본의 해시 불일치 0건.
- 초기 개발 진단은 Intent.removeExtra의 잘못된 JNI 반환형으로 실패했다. 최종 `389f9e9`에서 void 호출로 고치고 캐시 재빌드·호환 업데이트했다. 이전 진단 실패 APK `b1d9c571…891d7f6c`와 최종 APK를 구분한다. 최종 A25 검사: **PT matched / 영양 found / OFF 더단백 found / Android User-Agent wire-match**. HTTPS echo는 합성 연락처를 사용했다. 직접 클라이언트 검사이며 실물 OFF 발견 E2E와 구분한다. 근거: `artifacts/online-lookup/device-online-final.log`.
- **실물 사용자 확인 PASS:** 최종 APK에서 더단백·테아닌 바코드의 PT 등록 표시와 테아닌 앞면 3D. 초기 APK에서도 흰색 상품정보판·영양/가격 표·소실 시 숨김을 확인했다. 최종 로그는 Barcode2D→None과 Image3D→None을 기록했다.
- **비행기 모드 사용자 확인 PASS:** 새 프로세스에서 더단백의 PT 확인 불가+스냅샷 날짜·로컬 상품정보. 직접 클라이언트는 네트워크 unavailable을 기록했다. 시험 뒤 비행기 모드 0·Wi-Fi 0·모바일 데이터 1로 원상 복구했다. Home/복귀에서 센서 해제·재등록과 앱 복귀도 확인했다. 근거: `device-offline.log`, `device-lifecycle.log`, `network-{before-offline,restored}.json`.
- **정상 OFF 미발견 실물 사용자 확인 PASS:** `8809037615025`의 PT 200/not_linked → OFF 404/product_not_found를 PC에서 대조했고, 앱에서 읽은 번호·상품정보 미발견·PT 바코드 연결 필요·소실 시 해제를 확인했다. 근거: `off-physical-product.json`, `device-off-path.log`.
- **미수행:** 별도 미연결 실물의 OFF 상품명 발견 화면, 의도적으로 지연한 네트워크의 A→B/동시 앞면 충돌 실물 시험, 추적 대상이 보이는 상태의 pause·재스캔 전체 조합, 10회 반복·5분 부하·정량 성능 측정. 해당 경합은 자동 시험으로 확인했으며 모든 실기기 조합 PASS를 주장하지 않는다. 원격 함수 배포·DB 쓰기와 보고서 초안 재작성은 이 작업에서 수행하지 않았다.
- 공개 요약: [barcode-online-lookup-2026-10-27.json](validation/barcode-online-lookup-2026-10-27.json). 10/27 밤 이전 온라인 기기 확인 조건은 충족했으며 기능 플래그는 켜 둔다. 제출물 우선순위는 유지한다.

## 2026-10-26 v5 정보판 60%·소실 후 자동 스캔 복귀

- 사용자는 앞면·바코드가 정상 동작한다고 보고했고, 갤러리의 `Screenshot_20261026_140652_ AR.jpg`에서 테아닌 3D 정보판이 화면 대부분을 차지하는 것을 확인했다. 원본은 `artifacts/v5-rescan-fix/user-before.jpg`에 보존했다.
- 앞면 정보판의 가로·세로를 초기 v5의 **60%**로 줄였다. 약 58mm 참조 폭에서 정보판 폭은 **140mm → 84mm**다. 이미지 기준 위치·회전·크롭 오프셋과 바코드 2D 창 레이아웃은 유지했다.
- 인식이 끊기면 선택 상품·확정 번호를 해제한다. 바코드는 다음 판독 실패·다른 번호에서 해제하고, 입력 자체가 없으면 마지막 입력으로부터 **0.65초** 뒤 만료한다. 미등록 번호 안내도 만료한다. 앞면은 현재 FULL_TRACKING 소실 시 해제하며, 다른 경로가 계속 보이면 그 경로는 독립적으로 유지한다. 충돌은 현재 관측으로 재계산해 대상이 사라지면 자동 복구한다. 같은 상품 재획득의 탭 선택은 숨긴 정보판에 보존한다.
- Unity EditMode **105/105 PASS**, 실패·건너뜀 0, exit 0. 원시 결과: `artifacts/editmode/20261026-051505-38a83625/`. 판독 실패·무입력 만료·미등록 소실·다른 번호의 3회 확인·앞면 소실·다른 앞면 전환·충돌 해제·같은 상품 탭 유지와 60% 폭을 포함한다.
- Android 빌드 **Succeeded / errors=0 / exit=0**, **0.5.1 (6)** APK **66,314,660 bytes**, SHA-256 `86d3566e13fec5b2a3aeb40fa9959d95b16403209fb9ed3cd3960a60c3b06adc`. 격리 빌드 입력 **924개**를 해시로 대조했고 원본 입력도 빌드 후 동일했다.
- Galaxy A25에 동일 패키지·서명을 확인한 `-r` 업데이트를 완료했다. 최초 설치 시각은 유지됐다. 설치 APK를 다시 받아 해시 일치, 카메라 영상·권한, 상품 2개 로딩, SessionTracking과 스캔 대기 화면을 확인했다. 수집한 앱 PID 로그의 검사 대상 예외 패턴은 0건이다. **업데이트 후 실제 상품을 비춘 크기·소실·전환 시험은 아직 미실행**이다.

[검증 요약](validation/v5-rescan-panel-fix-2026-10-26.json). 원시 근거: `artifacts/v5-rescan-fix/`의 `tests.json`, `build.log`, `build-inputs.json`, `install-result.json`, `after-launch.log`, `after-screen.png`.

## 2026-10-26 v5 실기기 삭제 후 새 설치 — 실행 확인

- 사용자의 기존 앱 삭제·재설치 요청에 따라 `com.yeonsik.midtermar`만 uninstall했고, 패키지 부재를 확인한 뒤 새 설치했다. 기존 앱 데이터 초기화가 허용된 작업이다.
- Android 빌드 **Succeeded / errors=0 / exit=0**, APK **62,531,798 bytes**, 버전 **0.5.0 (5)**. 설치된 APK를 다시 받아 빌드 SHA-256 `eade0d7cac4d1e00868b2e3d9339bd842a65f724e9aa1ba1d4122f43d5c1f036`과 일치함을 확인했다. 원본 빌드 입력 145개는 그대로이며 격리 빌드 복사본의 코드·데이터 등 121개도 일치한다.
- Galaxy A25 **SM-A256N / Android 16**에서 카메라 권한·영상, v5 상품 2개와 참조 이미지 3개 로딩, 1920×1080 CPU 입력, `SessionTracking` 진입을 확인했다. 수집한 앱 PID 로그에서 검사한 FATAL EXCEPTION·NullReferenceException·DllNotFoundException·EntryPointNotFoundException·AndroidJavaException 패턴은 0건이다.
- **실물 테아닌 앞면·바코드 인식, 상하단 전환 정합, 정보판 탭 터치는 미검증**이다. 실행 성공을 실물 인식 성공으로 해석하지 않는다. 테아닌 가격·영양·원격 ID 확인 대기도 유지한다.

[새 설치 검증 요약](validation/product-recognition-v5-device-2026-10-26.json). 원시 근거: `artifacts/v5-device-reinstall/`의 `uninstall.txt`, `install-result.json`, `after-package.txt`, `device-launch.log`, `device-screen.png`, `build.log`.

## 2026-10-26 v5 독립 인식 — 코드·씬·데이터 검증

- 기준은 [v5 상품 인식 구조](PRODUCT-RECOGNITION-V5.md)다. 앞면은 바코드 선택 없이 3D 상품정보판을, 바코드는 앞면 없이 2D 정보판을 표시하도록 연결했다. 동일 상품은 3D를 우선하고, 서로 다른 상품의 동시 앞면·확정 바코드 충돌에서는 두 정보판을 숨긴다.
- 상품 ID 조회·선택적 EAN·필수 스냅샷 날짜, 이미지 연결 스키마 v2·동일 상품 복수 크롭, 바코드 미연결/앞면 미등록 배지, 로컬 기준 미등록 번호 안내를 구현했다. ②와 ④는 모르는 EAN에서 구분하지 않으며 상품명 추정으로 연결하지 않는다.
- 별도 프레임의 EAN 체크섬·3회 확인·오래된 결과 거부를 공통 판정기에 적용했다. 스캐너에 연속 관측 모드를 추가했으며 기존 단발 스캔·디코더·카메라/작업 스레드 경계는 유지한다. 이미지 소실·LAST_KNOWN_POSE·pause 숨김, 동일 상품 재획득·크롭 전환의 탭 유지, 상품 변경·재스캔 초기화를 검사했다.
- `iphone-t1` 상단 95점·하단 100점의 기존 크롭을 바이트 그대로 복사하고 약 58mm 폭을 설정했다. 반분 경계와 픽셀 비율에서 공통 상품 중심까지의 Z 오프셋을 계산했다. 다른 사진인 `iphone-t2` 100점은 대안으로 보존했다. [이미지 입력 근거](validation/v5-image-inputs-2026-10-26.json)
- 사용자 제공 EAN `8809375984692`는 체크섬 PASS다. PriceTrace 연결 프로젝트를 찾지 못했고 공개 조회가 401로 실패해 실제 원격 상품 ID는 미확인이다. 테아닌 항목은 로컬 시연 UUID로 명시하고 화면에도 ID 확인 대기를 표시한다. 가격·영양 객체는 null이며 실물 기능성·영양 수치나 가격을 만들지 않았다.
- 최종 Unity EditMode **97/97 PASS**, 실패·건너뜀 **0**, Unity exit **0**. 등록 상태 4종은 시험 전용 데이터로 검증했다. 실제 앱의 관측 이벤트→확정→재스캔, 복수 크롭 공통 원점, 중복 ID·EAN·날짜 오류, 미확정 번호 거부, 연속 스캔 시간 제한 제외, 2D 등록 배지 레이아웃을 포함한다. 기존 v4의 선택 선행 시험은 v5 독립 경로 시험으로 대체했다.
- 첫 실행은 88개 중 2개가 실패했다. 2D 정보판 캐시가 같은 상품의 값 변경을 갱신하지 못한 회귀를 수정했다. 다음 96개 중 배지 높이 1개가 실패해 표시 영역을 조정한 뒤 최종 97개를 통과했다.
- 씬·프리팹 준비는 `PrepareImageTrackingScene`이며 **앱 빌드·APK 생성·설치·실물 박스 시험은 수행하지 않았다.** 기존 Android 설정과 probe 씬·probe 라이브러리는 유지했다. 점수·코드 시험으로 실제 박스 인식률·상하단 전환 정합·화면 가독성·휴대폰 터치를 보장하지 않는다.

최종 원시 결과: `artifacts/editmode/20261025-152255-18ada5fe/results.xml`, `editor.log`, `summary.json`. 씬 준비: `artifacts/v5/prepare-delivery.log`, exit 0. [공개 시험 요약과 입력 해시](validation/product-recognition-v5-2026-10-26.json). 커밋·push는 수행하지 않았다.

## 2026-10-25 이미지 추적 World Space 정보판 — 코드 기능 검증

- `ProductAR.unity`를 바코드 상품 선택 → 등록 이미지의 3D 자세에 정보판 표시 흐름으로 연결했다. 바코드가 사라져도 선택을 유지하며 이미지 FULL_TRACKING에서만 표시한다. 소실·제한·마지막 자세·다른 이미지·다른 바코드·pause에서는 숨긴다.
- 영양/가격 탭과 다른 상품 스캔 버튼을 World Space 정보판에 연결했다. 추적 복구 시 선택한 탭을 유지하며 재스캔은 상품·자세·본문·탭을 초기화한다. 가격 관측일·판매처·출처, 영양 기준량, 누락값과 0의 차이를 보존한다.
- 현재 `test-card`와 더단백 데이터의 연결은 **개발용**이다. 상품 포장 인식이 아님을 대기 안내와 정보판에 표시한다. 상품 포장 마커로 전환할 때 실측 Physical Size를 필수 검증한다.
- `scripts/Test-EditMode.ps1`로 **76/76 PASS**, 실패·건너뜀 **0**, Unity exit **0**. 기존 51개와 새 이미지 정보판 기능 25개를 포함한다. 실제 앱의 선택→정보판→재스캔 이벤트, 위치·방향·크기 계산, pause/복귀, 연결 설정 검증, 정상/누락·0의 재바인딩을 검사했다.
- World Space 버튼은 카메라·GraphicRaycaster 연결, 정면/이미지 평면 방향의 포인터 영역, `ExecuteEvents` 버튼 이벤트 전달을 검사했다. EditMode의 RuntimeOnly 리스너는 시험 인스턴스에서만 활성화한다. 화면을 렌더링한 Graphic.depth 기반 히트 정렬이나 실기기 터치를 통과했다는 의미는 아니다.
- 평면 탐색·레이캐스트·공간 앵커와 이전 2D 바코드 표시기를 기본 이미지 추적 모드에서 비활성화했다. Android 설정·기존 probe 씬·probe 라이브러리는 유지했다.
- **앱 빌드·APK 생성·설치·실기기 시험 미실행**. 기존 APK의 파일 목록·크기·SHA-256·수정 시각이 작업 전과 일치한다. 새 코드의 실물 포장 인식률·정합·가독성·터치는 별도 검증한다.

최종 원시 결과: `artifacts/editmode/20261025-112507-34d4b9d9/results.xml`, `editor.log`, `summary.json`. 씬 준비 exit **0**: `artifacts/product-image-tests/prepare-final.log`. 공개 요약은 [코드 기능 검증 결과](validation/product-image-panel-2026-10-25.json), 구성과 한계는 [이미지 정보판 문서](PRODUCT-IMAGE-TRACKING.md)를 따른다.

## 2026-10-25 사용자 카드 마커 교체

- 루트의 실제 파일명은 `test.jpg`, `test2.jpg`다. 로컬 ARCore 6.3.5 `arcoreimg eval-img` 결과는 각각 **85점**, **40점**, exit 0이다. `test2.jpg`는 권장 기준 75점보다 낮아 사용자가 지정한 대체 이미지 `test.jpg`를 선택했다. 40점을 인식 불가능 판정으로 해석하지 않는다. 근거: `artifacts/image-tracking-probe/card-quality.json`.
- `test.jpg`를 편집하지 않고 `Assets/Images/test-card.jpg`로 복사했다. SHA-256 `4dca0d093e69a5b7208907d1a158d6f6d98cfaebfccd497831a9da39994b7813`로 일치한다. 참조 이름과 앱의 대기 안내를 카드에 맞췄다.
- 라이브러리가 이미 있는 경우에도 마커 텍스처·이름·크기 추정 설정을 갱신하도록 생성기를 수정했다. 기존 이미지 DB 캐시를 비우고 단일 마커를 유지한다. 생성 씬 시험에 실제 이미지 GUID 대조를 추가했다.
- 카드 마커 APK: **Android 빌드 Succeeded / errors=0 / Unity exit=0**. `Builds/Android/ImageTrackingProbe.apk`, **62,265,590 bytes**, SHA-256 `a2b7772f9239c8fa5bd8811f435d45318209add078742958786b2a94e5ff9837`.
- 새 임시 프로젝트의 전체 초기 빌드는 오래 걸려 중단하고 기존 프로젝트의 Android 캐시를 재사용했다. 빌드 전 ProjectSettings 전체를 백업하고 종료 후 바이트 해시를 대조해 복구했다. 근거: `artifacts/image-tracking-probe/card-cached-build.log`, `build-summary.json`. 일반 재빌드 스크립트의 격리 빌드 동작은 유지한다.
- Unity EditMode **51/51 PASS**, 실패·건너뜀 0, Unity exit **0**. 생성 씬의 참조 이미지 GUID가 현재 카드 에셋과 일치하는지와 기존 추적 조건을 확인했다. 시험 전 에셋·설정을 백업하고 시험 뒤 복구했다. 근거: `artifacts/image-tracking-probe/card-tests.xml`, `card-tests.log`, `card-tests-summary.json`.
- Galaxy A25 / Android 16에서 기존 앱과 서명이 달라 기본 설치 스크립트가 업데이트 전에 중단했다. 사용자 교체 설치 요청에 따라 기존 APK·내부 데이터·외부 저장 파일을 백업하고 새 APK로 교체했다. 기기에서 다시 가져온 설치 APK의 SHA-256이 위 카드 빌드와 일치했다. 앱과 백업 원문은 Git에서 제외한다.
- 초기 설치 직후에는 Android Activity 실행과 PID만 확인했으나 실제 Unity 초기화가 완료되지 않았다. 화면에는 `Not enough storage space to install required resources.`, 로그에는 IL2CPP 리소스 추출 실패가 있었다. 저장 공간은 약 **78GB** 남아 있었고, 복원한 외부 `files/il2cpp` 폴더의 소유자가 새 앱 UID가 아닌 **shell**이었다. 이전 실행 캐시까지 ADB로 복원한 것이 원인이었다.
- 해당 캐시를 다른 이름으로 보존하고 앱이 자신의 UID로 재생성하도록 하자 초기화가 정상 진행됐다. **18:29:41 KST** `[ImageProbe] Ready; target=test-card; build=0.1.1`, 이미지 DB **1개**, 카메라 활성화 및 이후 `SessionTracking`을 확인했다. 실행 캐시는 후속 데이터 복원 대상에서 제외하도록 로컬 복원 절차를 보완했다. 기본 설치 스크립트의 서명 대조·자동 삭제 방지 동작은 유지한다.
- **18:34:32 KST** 첫 `Cube visible=True; acquisitions=1`과 `FULL_TRACKING`을 확인했다. **18:35:11** 소실 시 숨김, **18:35:15** 재획득 시 `acquisitions=2`와 표시 재개를 확인했다. 당시 가림 조건을 통제하지 않았으므로 공식 가림 시험 PASS로 기록하지 않는다. 사용자에게서 **카드 위 큐브 표시와 카메라 회전·거리 변화 중 위치 유지**를 확인받았다.
- 초기 화면의 `상태 0`에서는 이미지 후보가 있으나 완전 추적 전이어서 큐브를 표시하지 않았다. 현재 안내는 이 단계와 미검출을 명확하게 구분하지 않는다. 실측 크기 미지정에 따른 추정 대기와 초기 인식 지연은 추가 검토 대상이며, 참조 이미지의 품질 점수만으로 지연 원인을 확정하지 않는다.
- **10회 반복·통제된 가림·장시간 시험은 미실시**다. 바코드 자체의 3D 추적·동적 포장 이미지 등록·상품정보판과 이미지 자세 연결도 미구현이다. 이 카드 결과를 기존 v3 바코드 추종 성공으로 확대하지 않는다.

기기 근거: 로컬 `artifacts/install/card-replacement.json`, `card-current-process.log`, `card-cache-fix-process.log`, `card-present-process.log`, `card-present-after.log`, `card-recognition-after.png`. 공개 가능한 빌드 식별·시험·실물 확인 요약은 [card-probe-2026-10-25.json](validation/card-probe-2026-10-25.json), 후속 기술 검토는 [바코드 마커 조사 보고서](BARCODE-MARKER-RESEARCH.md)에 남긴다. 기존 상품 씬·ProjectSettings·EditorBuildSettings의 원본 해시 일치를 최종 확인했다.

## 2026-10-25 이미지 추적 최소 구현·실기기 업데이트

과제의 3차원 정합을 먼저 검증하기 위해 [이미지 추적 검증](IMAGE-TRACKING-PROBE.md)을 추가했다. 전용 씬에서 강의 예제 이미지 1개의 현재 자세에 큐브 1개를 표시한다. 상품정보 기능을 연결하거나 v3의 실제 추적 성공을 주장하는 단계가 아니다.

- 강의 `Chap4-Resources.zip/Target-Hansung.jpg`와 참조 이미지의 SHA-256 일치, `arcoreimg` **80점 / exit 0**. 실측 크기를 지정하지 않고 ARCore의 크기 추정을 사용한다.
- `ARReadiness`의 권한·세션·100Hz 센서 처리를 재사용한다. ARCore의 현재 이미지 추적 방식이 FULL_TRACKING일 때만 표시하며 LAST_KNOWN_POSE·추적 제한·pause에서는 숨긴다. 실제 네이티브 추적 방식 호출은 이미지가 인식된 후에야 검증된다.
- Unity EditMode **51/51 PASS**, 실패·건너뜀 0, Unity exit **0**. 기존 42개와 추적 상태 조건 8개·생성 씬 저장/복원 검증 1개다. 첫 실행은 생성 직후 에셋의 일시 참조를 읽는 시험 1개가 실패했다. 저장된 씬을 다시 열어 영속 참조를 검증하도록 수정한 뒤 전체 통과했다. 근거: `artifacts/image-tracking-probe/tests-final.xml`, `tests-final.log`.
- Android ARM64·IL2CPP Development 빌드 **Succeeded / errors=0 / Unity exit=0**. APK: `Builds/Android/ImageTrackingProbe.apk`, **62,269,962 bytes**, SHA-256 `29125396d97f24dae267bda65d638cdbc07e684011984da354306663c41f85ce`. 근거: `build.log`, `build-summary.json`.
- 빌드에 사용한 Assets·Packages **862개 파일의 원본/빌드 복사본 SHA-256 일치**, 불일치 0. 원래 상품 씬·상품 JSON·ProjectSettings·EditorBuildSettings의 해시도 유지됐다. 검증용 Android 설정은 영문 임시 프로젝트에만 적용했다. 근거: `source-inventory.json`, `preserved-source-hashes.json`.
- 이전 서명 불일치를 현재 환경에서 재확인했다. 현재 로컬 `.android/debug.keystore`의 인증서는 기기의 기존 앱과 같은 **`cad84c…6135`**였다. 이를 빌드에서 명시하고 설치 스크립트로 다시 검증해 **같은 패키지 `com.yeonsik.midtermar`의 호환 `-r` 업데이트 성공**. 앱 삭제·데이터 초기화·다운그레이드는 하지 않았다. 기존 APK도 보존했다.
- Galaxy A25에서 **2026-10-25 02:26 KST**, versionName **0.1.1 / versionCode 2**, 앱 이름 **이미지 추적 검증**으로 업데이트하고 **COLD 실행 성공**. 실제 카메라 영상·한국어 안내·버튼을 화면으로 확인했다. 이미지 DB **1개 이미지**, 요청/지원 기능의 **Image Tracking**, 카메라 권한 허용, 두 센서 등록 성공을 로그로 확인했다.
- 02:27 KST 추가 관찰까지 동일 PID가 유지됐다. 수집한 앱 로그에서 FATAL EXCEPTION·NullReferenceException·DllNotFoundException·EntryPointNotFoundException은 0회였다. 네이티브 로그에는 초기 센서 큐 등록 오류와 타임스탬프 순서 경고가 있어 전체 로그가 무오류라고 판정하지 않는다.
- **실물 마커 인식·이동·회전·가림·재획득은 NOT PERFORMED**. 이번 관찰에서 세션은 `SessionInitializing`까지 확인했으며 `SessionTracking`과 이미지 `FULL_TRACKING`은 확인하지 못했다. 카메라 실행·설치 성공을 3차원 정합 성공으로 해석하지 않는다. 사용자가 사진과 주변을 비추며 움직이는 다음 시험이 필요하다.

기기 근거: `artifacts/image-tracking-probe/install-result.json`, `device-launch.log`, `device-observation.log`, `device-summary.json`, `device-screen.png`. Git 브랜치: `feat/image-tracking-probe`. 위 내용은 실기기 검증 시점의 기록이며 이후 커밋·push 상태는 Git 이력을 따른다.

## 2026-10-24 영양성분표·관측 가격 표시 개선

- 현재 자동 정보창의 영양정보를 항목·함량이 정렬된 표로 바꿨다. 기준량과 열량을 상단에 두고 굵은 구분선·행별 선·당류/포화지방 들여쓰기를 적용했다. 데이터에 없는 일일 기준치 비율이나 성분은 추가하지 않았다.
- 관측 가격은 별도 연녹색 영역에 금액을 강조하고 판매처와 관측일·개당 가격을 분리했다. 현재 가격으로 표현하지 않으며 시연용 연결·로컬 데이터 문구를 유지한다. 카드 폭 720은 유지하고 높이는 612에서 648 기준 단위로 조정했다.
- Unity EditMode **42/42 PASS**, 실패·건너뜀 0, Unity exit **0**. 기존 추종 시험과 정상/일부 누락·0/전체 누락의 표·가격 바인딩 3개를 포함한다. 상품을 다시 바인딩할 때 이전 정보가 남지 않는지 확인했다. 근거: `artifacts/nutrition-ui-tests.xml`, `nutrition-ui-tests.log`.
- 1080×1920·1080×2400에서 스캔, 중앙/왼쪽/상단 추종, 소실, 전체 데이터 누락, 일부 누락·0의 **14장**을 렌더링하고 활성 텍스트 넘침 검사를 통과했다. 정상·누락·0 화면과 상단 부족 시 아래 배치를 시각 확인했다. 누락·0 렌더용 데이터는 카탈로그와 실행 씬에 저장하지 않는다. 근거: `artifacts/nutrition-ui-previews.log`, `artifacts/ui/barcode-follow/`.
- 이 결과는 단색 배경의 에디터 UI 검사다. 실기기 카메라 위 가독성과 바코드 이동 중 사용성은 별도로 확인해야 한다.
- Android ARM64·IL2CPP Development APK 빌드 **Succeeded / errors=0 / Unity exit=0**. 파일 `Builds/Android/ProductAR.apk`, **85,737,220 bytes**, SHA-256 `2f78d6a49f74ad9d9dbc31abc03dbb1e3c94c284206c7d1f2eb7e4cfbebc8a3a`. 패키지 `com.yeonsik.midtermar`, 버전 **0.1.0 / 1**, min API **30**, target API **35**를 확인했다. 서명 검증 PASS, Android Debug 인증서 SHA-256은 직전 v3 APK와 같은 `31419df97e5066473b3a855350f38ebce701557a6a644c91ca07db75464d1667`이다. 근거: `artifacts/nutrition-ui-build.log`, `nutrition-ui-build-summary.json`, `nutrition-ui-apk-signature.log`. 빌드가 비운 `preloadedAssets`는 원래 3개 참조로 복구했다.
- 빌드 후 Galaxy A25 (`R5CWC01BHZP`) 연결을 확인했다. 설치 스크립트가 기기의 기존 APK를 보존하고 서명을 대조했으나, 기존 인증서는 아래 기기 기록과 같은 `cad84c…6135`, 새 APK는 `31419d…667`로 달라 업데이트 전에 중단했다. 로컬 Android 기본 디버그 키도 새 APK 인증서와 같으며 기존 앱과 일치하는 키는 확인하지 못했다. 앱 삭제·데이터 초기화·새 APK 설치·실기기 UI 검증은 수행하지 않았다. 기기에 보이는 화면은 이 변경을 포함하지 않는다.

## 2026-10-24 기획 v3 — 바코드 위 자동 정보창

사용자가 바코드 인식 직후 자동 표시하고 상품 이동 시 따라다니는 방식을 선택했다. 현재 상호작용 기준은 [BARCODE-FOLLOW.md](BARCODE-FOLLOW.md)다. 아래 공간 앵커·센서·터치 배치의 기기 기록은 이전 v2 성과이며 새 자동 추종의 실기기 성공으로 확대하지 않는다.

- 동일 AR 카메라의 EAN-13 3회 일치 후 터치 없이 정보창을 표시하도록 연결했다. 기존 평면 감지·배치 터치 처리는 기본 실행 씬에서 비활성화했다. 상품 확인 뒤에도 같은 번호의 위치를 계속 읽으며 초기 스캔의 15초 제한과 분리했다.
- ZXing의 원본 영상 기준 판독 선분을 전달하고 ARCore 네이티브 `IMAGE_NORMALIZED → VIEW_NORMALIZED` 변환을 카메라 콜백에서 복사한다. Unity 패키지의 version 1 포인터 구조와 ARM64 라이브러리의 `ArFrame_transformCoordinates2d` export를 확인했다. 네이티브 포인터를 디코더 스레드에 넘기지 않는다.
- 다른 번호, 오래된 비동기 결과, 화면 잘림 밖 위치, 좌표 변환 없음은 표시하지 않는다. 마지막 유효 촬영 시각부터 0.65초 뒤 숨기며 같은 번호를 다시 읽으면 자동 표시한다. 재스캔·백그라운드 전환 시 이전 위치를 정리한다.
- 자동 시험 **39/39 PASS**, 실패·건너뜀 0, Unity exit **0**. 0·90·180·270도 원본 영상의 판독점, 화면 회전·잘림 변환, 소실·일시 판독 공백·다른 번호·오래된 결과·재시작, 카드 경계, 터치 없는 상태 전환, 추종 중지·재시작, 기본 씬의 평면 비활성화를 확인했다. 근거: `artifacts/barcode-follow-tests.xml`, `barcode-follow-tests.log`.
- 1080×1920·1080×2400에서 스캔·중앙/왼쪽/상단 추종·소실 화면 총 10장을 렌더링하고 활성 텍스트 넘침 검사를 통과했다. 처음 발견한 가격 글자 넘침은 수정 후 재검증했다. 근거: `artifacts/barcode-follow-previews.log`, `artifacts/ui/barcode-follow/`. 단색 배경의 에디터 UI이므로 실제 카메라 정합 성공 화면으로 해석하지 않는다.
- ADB에 연결된 Android 기기가 없어 새 좌표 변환의 실제 네이티브 호출·실물 추종·5분 연속 부하·설치 업데이트는 **미검증**이다. 화면에서 보이는 바코드 위치를 따라가며 가림 이후의 상품 전체 3D 추적을 보장하지 않는다.

### v3 자동 정보창 APK

- ARM64·IL2CPP Development 빌드 **Succeeded / errors=0 / Unity exit=0**. 최종 버튼 강조 변경을 포함한 Android 네이티브 코드 생성을 확인했다. 근거: `artifacts/barcode-follow-build.log`, `barcode-follow-build-summary.json`.
- 파일: `Builds/Android/ProductAR.apk`, **85,735,718 bytes**, SHA-256 `c1fad9de4644ade856a49edac18f92f3f375cf9a02f9702df6572d549f88a016`.
- 패키지 `com.yeonsik.midtermar`, versionName **0.1.0**, versionCode **1**, min API **30**, target API **35**, `arm64-v8a` 확인. APK 안에 `lib/arm64-v8a/libarcore_sdk_c.so`가 포함된 것을 확인했다.
- 서명 검증 exit **0**, Android Debug 인증서 SHA-256 `31419df97e5066473b3a855350f38ebce701557a6a644c91ca07db75464d1667`. 직전 카메라 UI APK와 같은 현재 환경 서명이며, 아래 센서 보완 당시 설치 기록의 인증서와는 다르다. 설치 스크립트의 기존 서명 대조를 유지한다. 기존 앱 삭제·데이터 초기화·설치는 수행하지 않았다.
- 최종 버튼 색상까지 반영한 화면 10장과 **39/39**를 다시 확인했다. 빌드 후 비워진 `preloadedAssets`는 원래 3개 참조·순서로 복구했다. 임시 XR 이동·시험 리소스는 정상 종료 시 정리돼 관련 삭제·추가 변경을 남기지 않았다.

## 이전 v2 구현 및 검증

최신 확인일: **2026-10-24 KST**. 초기 구현은 `feat/ar-foundation-m0-m3`의 `b949cf7`, 바코드 수정 기준은 `29e66b0`이다. 아래 2026-10-24 공간 배치·센서 보완은 후속 작업 트리 변경과 생성 APK의 해시로 구분한다. 초기 조사 기준은 `45c7037`이다. 제품 사진·HTML 등 사용자 입력은 변경하지 않았다. Git 원격 생성·push·배포는 수행하지 않았다.

**2026-10-23 바코드 수정:** 3회 확인만으로 반복 오독이 해결되지 않아 판독 입력을 1920×1080으로 높였다. 당시 자동 시험은 **24/24 PASS**, 수정 APK 빌드·설치·실제 1920×1080 CPU 입력 확인까지 완료했다. 고해상도 실물 반복 시험은 대기 중이다. 최신 결과는 [해상도 분석 기록](BARCODE-RESOLUTION.md)을 따른다. 아래 초기 구현 검증 19개와 구분한다.

사용자가 사용자 개입 없이 가능한 후속 구현을 계속하도록 지시했으므로 **구현 완료·자동 시험·기기 실행·실물 검증을 분리**한다. 기기의 존재나 APK 설치만으로 실제 상품 인식·공간 배치의 PASS를 주장하지 않는다.

## 2026-10-24 카메라 UI 축소 및 상태·버튼 정리

- 실행 씬의 상단 앱 제목·소개를 현재 단계와 다음 행동으로 교체했다. 바코드 스캔·확인 중, 표면 탐색, 배치 가능, 고정 중, 배치 완료, 추적 복구, 권한·스캔 오류를 구분한다.
- 하단 안내판은 실제 안내·상품 요약·버튼 수에 따라 높이를 조절한다. 스캔 전 상품명 미리 표시와 비활성 재배치·삭제 버튼을 제거했다. 상품 확인 후 다른 상품 스캔, 배치 후 재배치·삭제를 표시하며 재배치 취소와 앱 복귀 후 재배치 이유 안내를 추가했다.
- 상품 확인 후 다른 상품 스캔 버튼은 보조 색상으로 낮춰 초록색 배치 표면과 경쟁하지 않게 했다. 재시도·설정 열기 버튼의 녹색 강조는 유지한다.
- Unity 메뉴 `Product AR → Update Camera UI`로 기존 AR 씬의 카메라 UI만 갱신했다. 추가 한국어 글리프를 동적 SDF에 보존했다.
- Unity EditMode **29/29 PASS**, 실패·건너뜀 0, 종료 코드 0. 기존 26개에 세 가지 단계의 카메라 영역·글자 넘침·버튼 높이 검증 3개를 추가했다. 근거: `artifacts/compact-ui-tests.xml`, `compact-ui-tests.log`.
- 실제 Unity UI를 1080×1920, 1080×2400으로 각각 8개 상태, 총 16장 렌더링했다. 모든 렌더에서 활성 텍스트 넘침 검사를 통과했다. 스캔·배치 완료·추적 복구·재배치 취소·권한 화면을 시각 확인했다. 단색 배경의 에디터 UI 검증이며 실기기 AR 성공 화면으로 해석하지 않는다. 근거: `artifacts/compact-ui-previews.log`, `artifacts/ui/{scan,surface,place,placed,reposition,recovery,unknown,permission}-{1920,2400}.png`.

| 화면 | 기존 UI 높이 비율 (1080×1920 기준) | 수정 후 1080×1920 | 수정 후 1080×2400 |
|---|---|---|---|
| 스캔 중 | 약 39.5% | 13.1% | 11.7% |
| 표면 찾기·배치 대기 | 약 39.5% | 23.7% | 21.2% |
| 배치 완료 | 약 39.5% | 21.5% | 19.2% |

비율은 상·하단 안내판의 높이 합을 캔버스 안전 영역 높이로 나눈 값이며, 기존값은 고정 레이아웃 198+560 / 1920으로 계산했다. 시스템 표시 영역과 실제 카메라·공간 정보판의 가독성은 별도 실기기 확인이 필요하다. 버튼 높이는 기존 132 기준 단위를 유지했다. 이 UI 변경의 실기기 설치·조작은 아직 수행하지 않았다. 아래 센서 보완 APK·기기 시험은 이전 빌드의 기록이다.

### 카메라 UI 수정 APK

- 최종 버튼 색상 반영 후 화면 16장 렌더링과 EditMode **29/29**를 다시 통과했다. ARM64·IL2CPP Android Development 빌드 **Succeeded / errors=0 / Unity exit=0**. 근거: `artifacts/compact-ui-final-build.log`, `compact-ui-build-summary.json`. 초기 전체 빌드는 `compact-ui-build.log`로 구분한다.
- 파일: `Builds/Android/ProductAR.apk`, **85,730,709 bytes**, SHA-256 `db10ef6ba8f309a74dfea4b902cc8e9bdb5eafe1ac9854c2a24af35b5ff6a0bf`.
- 패키지 `com.yeonsik.midtermar`, 이름 **상품정보 AR**, versionName **0.1.0**, versionCode **1**, min API **30**, target API **35**, `arm64-v8a` 확인.
- APK 서명 검증 종료 코드 **0**. 현재 환경의 Android Debug 인증서 SHA-256은 `31419df97e5066473b3a855350f38ebce701557a6a644c91ca07db75464d1667`이다. 아래 이전 설치 기록의 인증서와 다르므로 이전 기록만으로 호환 업데이트를 주장하지 않는다. 설치 스크립트의 기존 서명 대조를 유지하며, 기존 앱 삭제·데이터 초기화·기기 설치는 수행하지 않았다.
- 빌드가 옮긴 XR Simulation 에셋과 시험용 임시 리소스는 정상 종료 시 복구·정리됐다. 빌드 후 비워진 `preloadedAssets`는 원래 3개 참조·순서로 복구해 ProjectSettings 변경을 남기지 않았다. 씬 저장에서 ARCameraManager의 기존 필드 2개가 패키지의 직렬화 마이그레이션에 따라 `-1`로 바뀌었으며, 실제 자동 초점·광원 추정 설정은 유지된다.

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

2026-10-24 후속 주석 정리: 직접 작성한 C#·Java 소스 20개에 줄 끝 한국어 주석 76개를 추가했다. 추가한 주석만 제거하면 수정 전 파일의 바이트가 모두 동일함을 확인했다(`artifacts/inline-comment-validation.json`). 실행 코드 변경이 없어 APK 재빌드·기기 재설치는 수행하지 않았으며, 위 APK 해시와 자동 시험 결과는 센서 보완 빌드 당시의 기록이다.

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

### 센서 보완 APK (카메라 UI 수정 전)

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
