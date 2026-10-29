# Repository instructions

- Read `README.md` and `docs/CLOUD-DEVELOPMENT.md` before changing environment or build setup.
- Work on a feature branch. Preserve unrelated changes, existing submission tags, original course files and device state.
- Unity is pinned to 6000.3.22f1. Use Serena for supported symbol navigation; if its language server is unavailable, use bounded path-scoped searches and disclose that limitation.
- The cloud baseline is `python3 scripts/cloud/check_repo.py`. It checks source contracts only and does not compile or execute Unity.
- For runtime or Editor source changes, run the full EditMode suite with a licensed Editor: `UNITY_EDITOR=/absolute/path/to/Unity python3 scripts/cloud/test_editmode.py`. If unavailable, report NOT_RUN and the missing dependency. Do not skip tests or claim earlier counts as the current run.
- Preserve the fixed 12mm/15-degree panel rule, recognition/conflict/loss behavior and PT-to-OFF lookup rules unless the task explicitly changes them.
- `Assets/Config/remote-lookup.local.json` and its meta are local-only. Never copy them into a cloud environment, commit, logs or test artifacts. Missing config deliberately means local-only behavior.
- Do not upload signing keys, licenses, local API keys, APKs, generated Unity caches, device logs or `lecture/`. Existing submission export/key checks remain authoritative.
- Separate source checks, Unity tests, Android builds, device installation and physical AR/camera tests in every verification report. No device uninstall or data reset.
