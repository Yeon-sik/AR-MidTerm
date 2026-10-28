# 2026-10-29 제출 원본과 브랜치 기준

최신 제출 목표는 **0.7.0 (13)**이며 아직 제출 기준으로 확정하지 않았다. OFF 영양 표시를 구현했고, 추가 PT 상품과 현장 등록의 실물 검증이 남아 있다. 현재 빌드·설치·미수행 구분은 [0.7.0 검증 기록](validation/online-lookup-0.7.0-2026-10-28.json)을 따른다. 최종 영상·APK와 동일한 소스가 확정된 뒤 `submission/v0.7.0-2026-10-29` 태그와 ZIP을 생성한다. 아직 이 태그는 만들지 않았다.

아래 **0.6.1 (8)** 태그와 내보내기 명령은 이전 기준의 역사 기록이다. 새 제출 기준으로 사용하지 않으며 기존 태그를 이동하거나 삭제하지 않는다. 당시 앞면 AR은 테아닌 포장 1종(두 크롭), 바코드 식별은 테아닌·더단백 2종이었다. 개발용 카드 연결과 실제 포장 AR을 구분한다. 서로 다른 상품이 동시에 보이면 숨기는 규칙은 유지한다.

## 이전 고정 기준 — 0.6.1

기존 로컬 태그 **`submission/v0.6.1-2026-10-29`가 가리키는 커밋**은 이전 제출 기준으로 보존한다. 아래 명령으로 당시 SHA와 원본을 재현할 수 있다. 압축 안의 `SUBMISSION-MANIFEST.json`에는 실제 내보낸 커밋 SHA가 들어간다.

```powershell
git rev-parse 'submission/v0.6.1-2026-10-29^{commit}'
./scripts/Export-Submission.ps1 -SourceRef submission/v0.6.1-2026-10-29
```

내보내기는 지정한 Git 커밋의 파일을 사용한다. 저장하지 않았거나 커밋하지 않은 파일은 포함하지 않는다. 기존 ZIP은 덮어쓰지 않는다. 생성 뒤 결과의 sourceCommit·SHA-256·키 패턴 0건·제외 경로 0건을 확인한다.

## 포함·제외

| 포함 | 제외 |
|---|---|
| Assets와 해당 meta, Packages, ProjectSettings, docs, scripts, README.md | Library, Temp, Logs, obj, Builds, artifacts 전체 |
| 존재하면 PRODUCT.md, DESIGN.md, ThirdParty, .gitignore, .gitattributes | *.local.json과 해당 meta, .env*, 키·서명 파일, APK/AAB, 로그 |
| 자동 생성한 SUBMISSION-MANIFEST.json | .git, .aws, .codex, .serena, UserSettings |

Git에서 제외된 로컬 설정은 원칙적으로 내보내지 않는다. 실수로 추적된 제외 파일도 ZIP에서 제거한다. 압축 내부 모든 파일의 바이트를 키 형태의 publishable/secret·API/GitHub 토큰·JWT·개인키 헤더 패턴으로 검사하고, 탐지되면 값을 출력하지 않고 내보내기를 실패시킨다. 이 패턴 검사가 모든 종류의 비밀을 완전하게 탐지한다는 보장은 없으므로 포함 목록과 Git 제외 정책도 함께 적용한다.

```powershell
# 만든 ZIP을 다시 검사
./scripts/Export-Submission.ps1 -VerifyArchive '<생성된 ZIP 경로>'
# 유출·제외 경로 검사 도구의 회귀 시험
./scripts/Test-SubmissionPackage.ps1
```

10개 정책 시험은 정상 ZIP, 로컬 설정/meta, env, 로그, 바이너리의 publishable key, secret key, JWT, 개인키, 상위 경로 탈출을 확인한다. APK는 원본 ZIP과 별도 산출물이다. 대용량 원시 로그는 artifacts에 남기고 공개 검증 요약은 docs/validation에 포함한다.

## 키 없는 원본 실행

`Assets/Config/remote-lookup.local.json`이 없으면 **로컬 전용으로 실행**한다. 온라인을 사용하려는 개발 환경에서만 다음 도구로 사용자 로컬 설정을 만든다. 키·연락처를 제출 ZIP이나 Git에 넣지 않는다.

```powershell
./scripts/Initialize-RemoteLookup.ps1 -EnvPath '<공개 URL·publishable key가 있는 로컬 env 파일>' -OffContact '<승인한 연락처>'
```

그 뒤 Unity의 **Product AR → Prepare Image Tracking Scene (No Build)**로 로컬 TextAsset 참조를 갱신하고 `scripts/Test-EditMode.ps1` → `scripts/Build-Android.ps1`을 실행한다. 설정 생성은 선택 사항이며 키 없는 검토 환경에서는 건너뛴다. Android 버전·인터넷 권한은 ProjectBuilder 빌드 경로가 설정한다.

## 병합 승인 경계

예정 순서는 `feat/barcode-online-lookup → feat/image-tracking-probe → feat/ar-foundation-m0-m3`다. **사용자가 확인하기 전에는 병합하지 않는다.** 승인 시 현재 커밋·깨끗한 작업 트리·조상 관계를 다시 확인하고 `--ff-only`로만 수행한다. 분기 상태가 달라졌거나 충돌이 필요하면 중단한다. main 변경·강제 갱신·자동 push는 이 절차에 포함하지 않는다.

## 사용자가 남길 실물 근거

[기기 체크리스트](DEVICE-CHECKLIST.md)의 테아닌 앞면 회전·거리·가림·재획득을 최소 5회 수행하고 빈 결과 칸을 채운다. 같은 상품의 두 크롭 전환·영양/가격 탭도 확인한다. 더단백은 바코드 경로와 개발용 카드로 따로 기록한다. 검증한 장면으로 무음 데모 영상을 만들고 보고서·학번/이름·실제 제출 형식을 마무리한다. 이 원본 정리는 미수행 실물 결과나 보고서 완료를 대신하지 않는다.
