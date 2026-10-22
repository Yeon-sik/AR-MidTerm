# 가상현실 중간 프로젝트

Unity 6000.3.22f1 LTS 기반 Android AR 기술 검증 프로젝트.

현재 상태: **M0 PARTIAL / 전체 BLOCKED**. 환경 준비와 에디터 임포트는 완료했지만, 첫 상품 사진의 이미지 품질 점수가 0이고 실물 크기·운영 바코드 매핑·Android 기기가 확인되지 않았다. M1~M3는 단계 통과 조건에 따라 시작하지 않았다.

- AR Mobile Template 2.1.2 기반.
- AR Foundation / ARCore XR Plugin 6.3.5 고정. 실제 해결 결과는 `Packages/packages-lock.json` 참조.
- Unity Hub에서 이 폴더를 프로젝트로 추가하고 6000.3.22f1로 연다.
- `Assets/Scenes/SampleScene.unity`는 템플릿 샘플이다. 검증 완료된 최소 AR 씬이 아니다.
- `lecture/`는 변경하지 않은 강의 원본이며 Git에서 제외한다.
- 생성 캐시, 로그, APK, 로컬 조회 원문은 Git에서 제외한다.

검증 결과와 재개 조건: [M0–M3 검증 기록](docs/M0-M3-validation.md).

다음 작업은 M0의 참조 이미지 재촬영·실물 크기 확인·바코드 연결 근거 보완이다. M0 통과 후에만 M1의 Android 최소 AR 씬과 실기기 실행을 진행한다.
