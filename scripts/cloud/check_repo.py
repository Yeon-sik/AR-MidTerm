#!/usr/bin/env python3
"""Check the source checkout without starting Unity or contacting product APIs."""
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
KEY_PATTERNS = [
    rb"sb_(?:publishable|secret)_[A-Za-z0-9_-]{20,}",
    rb"sk-(?:proj-)?[A-Za-z0-9_-]{20,}",
    rb"(?:gh[pousr]_|github_pat_)[A-Za-z0-9_]{20,}",
    rb"eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}",
    rb"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----",
]
LOCAL_CONFIG = "Assets/Config/remote-lookup.local.json"


def git_files(*args):
    output = subprocess.check_output(["git", "-C", str(ROOT), "ls-files", "-z", *args])
    return [name.decode("utf-8") for name in output.split(b"\0") if name]


def excluded(name):
    path = PurePosixPath(name)
    excluded_parts = {"library", "temp", "logs", "obj", "builds", "artifacts",
                      "usersettings", ".git", ".aws", ".codex", ".serena", "lecture"}
    return (any(part.lower() in excluded_parts for part in path.parts)
            or path.name.startswith(".env")
            or re.search(r"\.(?:local\.json|keystore|jks|p12|pfx|pem|key|apk|aab|ulf)(?:\.meta)?$", name, re.I))


def main():
    tracked = git_files()
    errors = []
    for name in tracked:
        if excluded(name):
            errors.append(f"Excluded local/generated path tracked: {name}")
        path = ROOT / name
        if not path.is_file():
            errors.append(f"Tracked file missing: {name}")
            continue
        content = path.read_bytes()
        if any(re.search(pattern, content) for pattern in KEY_PATTERNS):
            errors.append(f"Key-shaped value in tracked file: {name}")

    required = ["Assets/Scenes/ProductAR.unity", "Assets/Tests/Editor/MidtermAR.Tests.asmdef",
                "Packages/manifest.json", "Packages/packages-lock.json", "ProjectSettings/ProjectVersion.txt"]
    errors.extend(f"Required source missing: {name}" for name in required if not (ROOT / name).is_file())
    if errors:
        raise ValueError("\n".join(errors))

    manifest = json.loads((ROOT / "Packages/manifest.json").read_text(encoding="utf-8-sig"))["dependencies"]
    lock = json.loads((ROOT / "Packages/packages-lock.json").read_text(encoding="utf-8-sig"))["dependencies"]
    for package, version in manifest.items():
        if package not in lock or lock[package]["version"] != version:
            raise ValueError(f"Manifest/lock mismatch: {package}")
    for name in tracked:
        if name.endswith(".asmdef"):
            json.loads((ROOT / name).read_text(encoding="utf-8-sig"))
    assembly = json.loads((ROOT / required[1]).read_text(encoding="utf-8-sig"))
    if assembly["name"] != "MidtermAR.Tests" or assembly["includePlatforms"] != ["Editor"]:
        raise ValueError("EditMode test assembly contract changed")
    version = (ROOT / "ProjectSettings/ProjectVersion.txt").read_text().splitlines()[0]
    if version != "m_EditorVersion: 6000.3.22f1":
        raise ValueError("Update the documented Unity toolchain together with ProjectVersion.txt")
    for name in [LOCAL_CONFIG, LOCAL_CONFIG + ".meta"]:
        if subprocess.run(["git", "-C", str(ROOT), "check-ignore", "-q", "--", name]).returncode != 0:
            raise ValueError(f"Local configuration is not ignored: {name}")
    print(json.dumps({"result": "PASS", "scope": "source-contract-only", "trackedFiles": len(tracked),
                      "unityExecuted": False, "deviceExecuted": False, "keyPatternMatches": 0}))


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        print(f"Source contract failed: {error}", file=sys.stderr)
        sys.exit(1)
