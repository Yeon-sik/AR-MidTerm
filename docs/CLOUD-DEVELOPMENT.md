# Codex / Claude 클라우드 개발

2026-10-29 기준, 이 저장소는 **소스 검토·수정과 정적 CI가 가능**하다. Unity 실행은 Editor 설치·라이선스·패키지 네트워크가 갖춰진 환경에서만 가능하다. 실제 Codex Cloud·Claude cloud 세션은 이 문서 작성 과정에서 실행하지 않았다.

| 검증 | 요구 조건 | 증거 범위 |
|---|---|---|
| 소스 계약 | Git, Python 3.10+ | 추적 파일·제외 경로·키 형태·패키지 lock·asmdef·지정 버전 |
| 전체 EditMode | Unity 6000.3.22f1, 유효 라이선스, Python, 패키지 다운로드 | 실제 Editor 컴파일과 시험 결과 XML |
| Android APK | 위 조건 + Editor의 Android Build Support / SDK / NDK / OpenJDK | APK 생성까지; 실물 AR 성공과 별개 |
| 카메라·ARCore·앞면 추적·현장 등록 | 지원 Android 실기기와 실제 상품 | 클라우드 headless 검사로 대체 불가 |

## 공통 명령

저장소 루트에서 실행한다. 초기 복제에는 키나 로컬 설정이 필요하지 않다.

```bash
python3 scripts/cloud/check_repo.py
UNITY_EDITOR=/opt/unity/Editor/Unity python3 scripts/cloud/test_editmode.py
```

첫 명령은 네트워크나 Unity 없이 실행한다. 두 번째는 `MidtermAR.Tests` 전체를 임시 프로젝트 사본에서 실행한다. 현재 수정 파일과 ignore되지 않은 새 Assets/Packages/ProjectSettings 파일을 복사하고, 로컬 설정·생성 캐시는 복사하지 않는다. 원래 씬·폰트·ProjectSettings를 Editor가 변경하지 않도록 격리한다. XML이 없거나 종료 코드가 0이 아니거나 통과 수가 전체와 다르거나 실패·건너뜀이 있으면 실패한다. 결과와 로그는 ignore된 `artifacts/cloud-editmode/<UTC-run>/`에 남는다. Unity 라이선스는 환경 제공자가 정식 절차로 준비하며 저장소·artifact에 넣지 않는다.

새 runner를 2026-10-29 Windows의 Unity 6000.3.22f1에서 실행하여 **381/381 PASS, 실패·건너뜀·미결정 0, Unity exit 0**을 확인했다. 키 없는 local-only 임시 프로젝트에서 실행했으며 원래 소스·씬·ProjectSettings는 변경하지 않았다. 근거는 로컬 `artifacts/cloud-editmode/20261029-143653-709756/{results.xml,summary.json,editor.log}`다. source-contract와 기존 제출 ZIP 정책 검사 **10/10**도 통과했다. 새 Linux Editor 실행 결과는 아직 없다. `.github/workflows/source-contract.yml`의 성공도 Unity 시험 성공을 뜻하지 않는다. 런타임 코드 PR의 필수 EditMode 검증을 이 정적 CI로 대체하지 않는다. 기존 Windows 절차는 `scripts/Test-EditMode.ps1`, `scripts/Build-Android.ps1 -Release`를 계속 사용한다. Linux Android 빌드 자동화는 이 변경에 포함하지 않았다.

## Codex Cloud

[공식 환경 안내](https://learn.chatgpt.com/docs/environments/cloud-environments)에 따라 GitHub 저장소를 선택하고 환경을 준비·검증한 뒤 Publish한다. Install script에는 Git/Python 준비와 `python3 scripts/cloud/check_repo.py`를 지정할 수 있다. Start skill에는 루트 `AGENTS.md`와 위 검증 명령, Unity 설치 유무를 기록한다. 런타임 작업 환경이면 정확한 Unity 버전과 라이선스를 별도로 준비한 뒤 전체 시험을 실행하고 게시한다. 저장만 한 환경을 게시 완료로 보고하지 않는다.

Unity import에는 `packages.unity.com`, `download.packages.unity.com` 및 실제 오류에 나타나는 Unity 패키지 CDN 접근이 필요할 수 있다. Editor/라이선스 서비스도 별도다. 저장소 CI에는 비밀값을 요구하지 않는다. 온라인 상품 조회를 검증하지 않으므로 운영 API 키를 추가할 이유가 없다.

## Claude cloud

[공식 환경 안내](https://code.claude.com/docs/en/cloud-environments)에 따라 Ubuntu 환경을 구성한다. VM 도구 설치용 Setup script와 clone 이후 저장소 검증을 구분한다. Setup script에 Git/Python 준비를 두고, 저장소가 준비된 세션에서 공통 명령을 실행한다. 클라우드에 로컬 개인 스킬이나 Unity 설치가 자동으로 전달된다고 가정하지 않는다. Editor 설치·패키지 import가 길면 초기 setup cache가 성립하지 않을 수 있으므로 실제 시간과 오류를 기록한다. 설정·라이선스 없이 실패하는 Unity 시험을 녹색 skip으로 바꾸지 않는다.

## 보존 계약

`Assets/Config/remote-lookup.local.json`과 meta는 계속 제외한다. 없으면 local-only이다. 키 없는 `remote-lookup.example.json`만 공유한다. `lecture/`, Library, Builds, artifacts, APK, 서명키·라이선스는 원격 소스에 넣지 않는다. 제출 ZIP은 기존 `scripts/Export-Submission.ps1`와 `scripts/Test-SubmissionPackage.ps1`로 별도 검증한다. 제출 기준 태그를 이동하지 않는다. 기존 0.7.0 (16)·381개 시험·A25 기록은 `docs/VALIDATION.md`의 해당 소스/산출물에 대한 이력이며 이번 클라우드 도구의 새 실행 증거가 아니다.
