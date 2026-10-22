# 가상현실 중간 프로젝트

Unity 6000.3.22f1 LTS 기반 Android AR 기술 검증 프로젝트.

현재 상태: **M0 PARTIAL / 전체 BLOCKED**. 환경 준비와 에디터 임포트는 완료했다. 첫 실물 사진은 품질 0점, 판매 페이지에서 가져온 최초 2버전(600×600)은 45점, 같은 이름으로 교체된 수정본(538×228)은 0점이다. 사용자 실측은 약 150×65mm이며 최종 참조 영역과의 대응은 미확인이다. 운영 바코드 매핑·Android 기기도 미확인이다. M1~M3는 단계 통과 조건에 따라 시작하지 않았다.

- AR Mobile Template 2.1.2 기반.
- AR Foundation / ARCore XR Plugin 6.3.5 고정. 실제 해결 결과는 `Packages/packages-lock.json` 참조.
- Unity Hub에서 이 폴더를 프로젝트로 추가하고 6000.3.22f1로 연다.
- `Assets/Scenes/SampleScene.unity`는 템플릿 샘플이다. 검증 완료된 최소 AR 씬이 아니다.
- `lecture/`는 변경하지 않은 강의 원본이며 Git에서 제외한다.
- 생성 캐시, 로그, APK, 로컬 조회 원문은 Git에서 제외한다.

검증 결과와 재개 조건: [M0–M3 검증 기록](docs/M0-M3-validation.md).

다음 작업은 같은 포장 디자인의 고해상도 참조 이미지 확보·평면 고정·참조 영역에 맞는 실측값 확정·바코드 연결 근거 보완이다. M0 통과 후에만 M1의 Android 최소 AR 씬과 실기기 실행을 진행한다.
