#!/usr/bin/env python3
"""Run the real Unity EditMode suite in a temporary, key-free project copy."""
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

from check_repo import LOCAL_CONFIG, ROOT, git_files, main as check_repo


def main():
    editor = os.environ.get("UNITY_EDITOR", "")
    if not editor or not Path(editor).is_file():
        raise ValueError("Set UNITY_EDITOR to a licensed Unity 6000.3.22f1 executable. No tests ran.")
    check_repo()
    run_id = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S-%f")
    output = ROOT / "artifacts/cloud-editmode" / run_id
    output.mkdir(parents=True)
    results, log = output / "results.xml", output / "editor.log"
    # Include current edited and new, nonignored source files, never Library or local keys.
    files = git_files("--cached", "--others", "--exclude-standard", "--", "Assets", "Packages", "ProjectSettings")
    with tempfile.TemporaryDirectory(prefix="product-ar-editmode-") as folder:
        project = Path(folder)
        for name in files:
            if name in (LOCAL_CONFIG, LOCAL_CONFIG + ".meta"):
                continue
            source = ROOT / name
            if source.is_symlink() or not source.is_file():
                raise ValueError(f"Missing or symbolic source file: {name}")
            destination = project / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)
        args = [str(Path(editor).resolve()), "-batchmode", "-nographics", "-projectPath", str(project),
                "-runTests", "-testPlatform", "EditMode", "-assemblyNames", "MidtermAR.Tests",
                "-testResults", str(results), "-logFile", str(log)]
        # Do not use -quit: the Unity test runner must finish its asynchronous test run.
        process = subprocess.run(args, check=False)
    if not results.is_file():
        raise ValueError(f"No test results (Unity exit {process.returncode}). Inspect {log}")
    run = ET.parse(results).getroot()
    summary = {"unityExitCode": process.returncode, "result": run.get("result"),
               **{key: int(run.get(key, "0")) for key in ("total", "passed", "failed", "skipped", "inconclusive")},
               "playerBuildRequested": False, "deviceExecuted": False, "configuration": "local-only",
               "results": str(results), "log": str(log)}
    (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary))
    if (process.returncode != 0 or summary["result"] != "Passed" or summary["total"] < 1
            or summary["passed"] != summary["total"] or summary["failed"] or summary["skipped"] or summary["inconclusive"]):
        raise ValueError("The full EditMode suite did not pass without skips.")


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, ET.ParseError, subprocess.CalledProcessError) as error:
        print(f"EditMode verification failed: {error}", file=sys.stderr)
        sys.exit(1)
