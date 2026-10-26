# 바코드 온라인 조회 — 0.6.1 (8)

기준 소스: `70d73e7545a103bca60954fc9132c3f2cd04fb49`, 브랜치 `feat/barcode-online-lookup`. 이전 0.6.0의 기록은 해당 검증 JSON에 보존한다.
제출 마감은 2026-10-29다. **실물 시험·무음 데모 영상·보고서를 우선한다.**

## 동작

EAN-13의 서로 다른 프레임 3회 확인 후, 정확히 일치하는 로컬 스냅샷을 즉시 표시한다. 설정이 있으면 PT 공개 바코드 함수 v1을 비동기로 조회한다. `matched`의 PT ID로 공개 영양 함수를 호출하며, `not_linked`만 OFF v3로 넘어간다. `conflict`는 자동 선택하지 않는다. 401·429·5xx·타임아웃·계약 오류·PGRST202는 PT 확인 불가이며 OFF로 우회하지 않는다.

앞면 이미지는 기존 FULL_TRACKING 경로로 표시한다. 온라인 PT ID가 앞면 ID와 같으면 3D 우선, 다르거나 조회 중·PT 미연결이면 충돌로 숨긴다. 바코드 0.65초는 관측 유효 시간의 최대치다. 판독 실패 프레임에서는 즉시 해제하며 pause·재스캔도 확정 상태를 초기화한다.

| 결과 | 표시 |
|---|---|
| 로컬 결과 | PT 스냅샷 날짜; 온라인 조회 중/확인 불가 병기 |
| PT matched | PT 등록 상품; 상품명·규격·확인된 가격·승인 영양 |
| PT not_linked + OFF 상품명 | Open Food Facts 정보 · PT 바코드 연결 필요 |
| OFF 정상 미발견 | 읽은 번호 · 상품정보를 찾지 못했습니다 |
| OFF 오류 | 외부 조회 실패 · 다시 시도; 미발견으로 캐시하지 않음 |
| PT conflict / invalid | 연결 확인 필요 / 바코드 확인 필요 |

영양은 `nutrition-read.v1`의 snake_case 응답을 사용한다. 요청은 `p_namespace=pricetrace`, `p_catalog_product_id=<PT ID>`다. domain의 `approvedCatalogProductId`는 원문 `catalog_product_id`를 매핑한 값이며, 요청 ID와 다르면 버린다. 복수 영양 행은 임의 선택하지 않고 영양 확인 불가로 둔다. 가격·영양 없음과 실제 0을 구분한다. 온라인 가격의 observed/listed, 로컬 가격의 스냅샷 출처를 구분한다.

## 로컬 설정과 중단 방법

`Assets/Config/remote-lookup.example.json`을 기준으로 Git 제외 로컬 파일을 만든다. 파일 없음·잘못된 설정·enabled=false이면 로컬 전용이다. 씬 생성기가 로컬 TextAsset을 연결하므로 설정 변경 후 씬 갱신과 재빌드가 필요하다. publishable key만 허용하며 service_role·다른 공급자 키는 사용하지 않는다.

```powershell
./scripts/Initialize-RemoteLookup.ps1 -OffContact '<사용자가 승인한 연락처>'
# Unity 메뉴 Product AR → Prepare Image Tracking Scene (No Build)
./scripts/Test-EditMode.ps1
./scripts/Build-Android.ps1
./scripts/Install-Android.ps1 -DeviceSerial <serial> -Launch
```

설치 스크립트는 기존 서명·버전을 대조하고 내부적으로 `install --user 0 -r`을 사용한다. `-r`이라는 별도 PowerShell 매개변수는 없다.

**2026-10-27 밤까지 정상 온라인 흐름을 실기기에서 확인하지 못하면 사용자와 중단 기준을 확인하고 플래그를 끈다.** `Initialize-RemoteLookup.ps1 -Disable` → 씬 갱신 → 빌드·호환 업데이트 후 스냅샷으로 시연한다. 밤의 구체적인 시각은 지정되지 않았다. 설정·연락처·원문 응답은 공개 기록에 쓰지 않는다. 이 파일은 APK에 포함되는 클라이언트 설정이며 비밀키 저장소가 아니다.

## 요청·상태 경계

0.6.1에서는 경로별 `DisplayProduct`·`DisplayProductKey`를 버튼 동작과 상품 변경 감지 기준으로 사용한다. 로컬에 없는 온라인 matched도 같은 동작을 하며 Selected에는 온라인 DTO를 넣지 않는다. 소실 시 현재 표시 상품·키는 해제하고, 숨긴 패널의 같은 상품 탭 재사용 규칙은 유지한다. `secondaryAction`은 현재 기본 버튼의 보조 스타일을 선택하는 플래그다.

PT 확정 계약은 작업 중 `d7c1d33`에서 보완됐다. verified 식별자 충돌은 공개 여부 필터 전에 판정하며 비공개 후보가 섞이면 `conflict + []`로 가린다. 클라이언트는 빈 후보 conflict를 충돌로 처리하고, 후보 1개 conflict·잘못된 버전/봉투는 계속 거부한다. 충돌에서는 상품을 선택하거나 영양·OFF를 호출하지 않는다. 검토 근거는 PriceTrace `docs/contracts/PUBLIC_BARCODE_LOOKUP_V1.md`다.

- HTTP 및 전체 조회에 5초 제한을 둔다. 자동 재시도 0회, 재스캔으로 명시적 재시도한다. 오류 뒤 같은 번호는 15초 동안 재호출을 억제한다.
- 같은 번호의 진행 중 요청을 합치며 최대 동시 번호 4개다. OFF는 앱 전체의 60초 이동 구간당 15회 상한을 공유한다. 다른 앱·같은 IP의 트래픽까지 통제하지는 않는다.
- matched/OFF 발견은 메모리 300초, 정상 미발견은 60초 캐시다. 최대 128개이며 앱 재시작 때 초기화한다. 오류·충돌은 미발견 캐시가 아니다.
- 결과는 메인 스레드 `Tick`에서 처리한다. 현재 확정 바코드와 요청 세대가 일치할 때만 화면에 반영한다. 늦은 정상 결과는 번호별 캐시에만 남길 수 있으며 소실 대상을 복원하지 않는다. 같은 번호를 새로 3회 확정한 경우에는 그 세대에서 캐시를 사용할 수 있다.
- 온라인 표시 결과는 `LookupResult`/`BarcodeDisplayProduct`에 보관한다. 로컬 `Selected`·앞면 카탈로그에 온라인 DTO를 주입하지 않는다.

## 검증 경계

0.6.1 EditMode **172/172 PASS**: 기존 162개 유지 + 신규 10개, 실패·건너뜀 0. 원문은 `artifacts/editmode/20261026-160623-0906ab89/`이다. 0.6.0 초기 159/162에서 수정한 가격 문구·배지 높이 결과는 이전 검증 기록에 보존한다.

2026-10-27 KST 초반 PC 읽기에서 PT 바코드 함수 404/PGRST202를 확인했다. **00:27 이후 A25·PC에서 200/matched로 바뀌었다.** 더단백·테아닌의 정확 PT ID가 일치하며 공개 가격은 null이어서 로컬 관측을 사용한다. Nutella `3017624010701`·시험 번호 `4006381333931`은 PT not_linked다. 공개 카탈로그 v4는 200/107개/테아닌 ID 존재, 더단백 영양은 200/1건, 테아닌 영양은 200/0건, OFF 더단백·Nutella는 200/product_found다. 사용자 제공 전일 카탈로그 기록은 200/102개다. 원격 함수는 이 작업에서 배포하거나 DB를 수정하지 않았다.

개발 빌드에서만 ADB Intent `--ez productar.lookupProbe true`로 직접 클라이언트 검사를 실행할 수 있다. `--es productar.lookupBarcode <EAN>`으로 OFF 입력을 지정하며 기본은 더단백이다. 카메라 인식·선택 상태는 건드리지 않는다. 최종 APK의 A25 검사에서 PT matched·영양 found·OFF found·Android User-Agent **wire-match**를 확인했다. User-Agent echo에는 합성 연락처만 보낸다. 실제 OFF는 사용자가 승인한 연락처로 구성한다. Unity는 User-Agent 직접 설정을 일반적으로 권장하진 않으므로 기기 전송 결과를 확인했다([Unity API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Networking.UnityWebRequest.SetRequestHeader.html)). 직접 클라이언트 검사를 실물 OFF E2E로 기록하지 않는다.

0.6.0에서 사용자가 두 시연 바코드 PT 등록 표시·테아닌 앞면 3D·소실, 비행기 모드의 확인 불가+스냅샷을 확인했다. 따라서 10/27 밤 이전 온라인 기기 확인 조건을 충족했으며 현재 로컬 플래그는 enabled=true다. 검증되지 않은 분기·제출물은 계속 별도로 기록한다.

추가 실물 `8809037615025`의 PT not_linked→OFF 정상 미발견·연결 필요 안내·소실 시 해제도 사용자 확인했다. 당시 APK는 0.6.0 (7), SHA-256 `c6c12a607c4598de4c2d8b510e616ed6915dfb8554c88f53269267706660f6f4`다. 빌드 입력 소스 37개 대조·실제 설치 APK 해시 일치, 키·연락처 Git 후보 유출 0건을 확인했다. 생성한 전용 임시 디렉터리만 `Build-Android.ps1 -ReuseStage <temp-stage>`로 재사용할 수 있으며 저장소·외부 경로와 junction은 거부한다.

상세 [검증 기록](VALIDATION.md), [0.6.1 검증 JSON](validation/online-lookup-finish-2026-10-27.json)을 따른다. REPORT-DRAFT.md의 v5 재작성·제출 영상 제작은 이번 범위 밖이다.
