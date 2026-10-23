# 가상현실 중간 프로젝트

바코드로 상품을 식별하고, 사용자가 상품 옆 현실 평면을 터치하면 해당 위치에 상품 정보판을 배치하는 Android AR 앱.

기획 v2 확정일: **2026-10-23**. 현재 개발 기준은 [기획 및 마일스톤](docs/MILESTONES.md)이다.

## 사용 흐름

카메라 실행 → 바코드 인식 → 정확한 상품 확인 → 상품 옆 현실 평면 터치 → 공간에 상품 정보판 배치 → 정보 확인·재배치·삭제.

- 바코드는 상품 식별에 사용한다. 배치 위치는 사용자의 터치와 AR 평면 Raycast로 결정한다.
- 정보판은 배치한 현실 위치에 유지된다. 상품을 옮기면 사용자가 재배치한다.
- 첫 시연 대상은 **빙그레 더단백 크런치바 초코 40g**이다. 가격·영양정보는 출처가 확인된 로컬 JSON 스냅샷을 사용한다.
- 상품 이미지 인식, 참조 이미지 제작·실측, 상품 위치 자동 추적은 현재 기획에서 제외한다.

## 현재 상태

**M0 준비 완료 / 앱 코드·씬·APK 구현 / 실물 시험 대기**. 사용자의 자율 진행 지시에 따라 기기 검증을 기다리지 않고 독립적인 후속 구현을 진행했다. 단계별 실제 판정은 [구현·검증 기록](docs/VALIDATION.md)을 따른다.

- 바코드 판독, 정확한 로컬 매핑, 한국어 정보판, 평면 터치 배치·재배치·삭제·다시 스캔을 구현했다.
- Unity EditMode 자동 시험 **23/23 통과**, ARM64·IL2CPP Android APK 빌드 성공.
- Galaxy A25 / Android 16에 설치·실행하고 카메라 영상, CPU 이미지 변환, 합성 EAN-13의 Android 디코더 자체 점검을 확인했다.
- 공간 추적은 관찰 시 `SessionInitializing`이었다. **실물 바코드 10회·실제 공간 배치·고정 검증은 미완료**이며 합성 입력 시험으로 대체하지 않는다.
- 단발성 판독으로 미등록 안내가 확정되던 문제를 수정했다. 같은 번호를 3회 연속 확인하고, 미등록 시 읽은 번호를 표시한다. 실물 재시험 결과는 [수정 검증 기록](docs/BARCODE-VALIDATION.md)을 따른다.
- 바코드 연결은 사용자 제공 근거의 **demo-only** 매핑이다. 운영 DB의 기존 매핑은 미검증이다.
- 이미지 품질은 새 M0의 통과 조건이 아니다. 과거 시험은 [구기획 검증 기록](docs/M0-M3-validation.md)에 보존한다.

## 개발 환경

- Unity **6000.3.22f1 LTS**, AR Mobile Template **2.1.2** 기반.
- AR Foundation / ARCore XR Plugin **6.3.5**. 실제 패키지 버전은 `Packages/manifest.json`과 `Packages/packages-lock.json` 참조.
- ZXing.Net **0.16.11**, Unity Newtonsoft JSON **3.2.2**, OFL Noto Sans CJK KR을 사용한다. 바코드는 동일 AR 카메라의 CPU 이미지에서 판독한다.
- Unity Hub에 이 폴더를 추가하고 지정 에디터 버전으로 연다.
- 실행 씬은 **`Assets/Scenes/ProductAR.unity`**다. `SampleScene.unity`는 보존한 템플릿 샘플이다.
- `lecture/`는 강의 원본이다. 생성 캐시·로그·APK·키·로컬 조회 원문은 Git에서 제외한다.

## 빌드·설치·남은 검증

- APK: `Builds/Android/ProductAR.apk` (생성 파일, Git 제외), 앱 이름 **상품정보 AR**, 패키지 `com.yeonsik.midtermar`.
- 재빌드: `scripts/Build-Android.ps1`. 한글 경로 제한을 피하도록 영문 임시 복사본에서 빌드한다.
- 설치·실행: `scripts/Install-Android.ps1 -DeviceSerial <serial> -Launch`. 서명·버전을 확인한 `-r` 업데이트를 사용한다.
- 씬·프리팹 재생성: Unity 메뉴 **Product AR → Prepare Project and Scene**. 생성 씬을 수동 수정했다면 재생성 전 내용을 보존한다.
- 남은 실물 시험: [기기 체크리스트](docs/DEVICE-CHECKLIST.md). 제품의 바코드를 보여주고, 상품 옆 책상을 비춘 뒤 표면을 터치한다.
- 제출 준비: [중간보고서 초안](docs/REPORT-DRAFT.md). 실제 시험 결과·학번·이름·데모 영상은 보완해야 한다.
