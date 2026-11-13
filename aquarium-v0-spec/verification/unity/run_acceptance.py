#!/usr/bin/env python3
"""Launch one explicitly scheduled exclusive Unity acceptance run; never installs Unity."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]


def roundtrip_diagnostic(output):
    before = output / "filled-before-restore.json"
    after = output / "filled-after-restore.json"
    if not before.exists() or not after.exists():
        return None
    left, right = before.read_text(), after.read_text()
    differences = []

    def visit(a, b, path):
        if len(differences) >= 12:
            return
        if isinstance(a, dict) and isinstance(b, dict):
            for key in dict.fromkeys(list(a) + list(b)):
                if key not in a or key not in b:
                    differences.append({"path": path + "." + key, "kind": "missing_key", "before": a.get(key), "after": b.get(key)})
                else:
                    visit(a[key], b[key], path + "." + key)
        elif isinstance(a, list) and isinstance(b, list):
            if len(a) != len(b):
                differences.append({"path": path, "kind": "array_length", "before": len(a), "after": len(b)})
            for i, (x, y) in enumerate(zip(a, b)):
                visit(x, y, path + "[" + str(i) + "]")
        elif type(a) is not type(b) and not (isinstance(a, (int, float)) and not isinstance(a, bool) and isinstance(b, (int, float)) and not isinstance(b, bool)):
            differences.append({"path": path, "kind": "type", "before": a, "after": b})
        elif a != b:
            differences.append({"path": path, "kind": "value", "before": a, "after": b})

    visit(json.loads(left), json.loads(right), "$")
    diagnostic = {"before": str(before), "after": str(after), "byte_equal": left == right,
                  "semantic_equal": not differences, "first_value_path": differences[0]["path"] if differences else None,
                  "differences_first_12": differences,
                  "note": "Diagnostic only. This never changes the C# typed codec comparison or run result."}
    target = output / "roundtrip-difference.json"
    target.write_text(json.dumps(diagnostic, indent=2) + "\n")
    return str(target)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--editor", required=True, type=Path, help="Installed Unity.app/Contents/MacOS/Unity")
    parser.add_argument("--project", type=Path, default=ROOT / "unity", help="Unity project directory; defaults to the original unity project")
    parser.add_argument("--mode", required=True, choices=("batch", "play"))
    parser.add_argument("--run-id", required=True, help="Run identifier agreed with the coordinating agent")
    parser.add_argument("--timeout", type=int, default=900)
    args = parser.parse_args()
    project = args.project.resolve()
    if not (project / "ProjectSettings/ProjectVersion.txt").is_file() or not (project / "Packages/manifest.json").is_file():
        parser.error("Selected project lacks ProjectVersion.txt or Packages/manifest.json")
    version_match = re.search(r"^m_EditorVersion:\s*(\S+)\s*$", (project / "ProjectSettings/ProjectVersion.txt").read_text(), re.MULTILINE)
    manifest = json.loads((project / "Packages/manifest.json").read_text())
    expected_urp = manifest.get("dependencies", {}).get("com.unity.render-pipelines.universal")
    if not version_match or not expected_urp or not re.fullmatch(r"\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?", expected_urp):
        parser.error("Selected project must freeze an editor version and an exact URP package version")
    expected_unity = version_match.group(1)
    if not args.editor.is_file():
        parser.error("Unity editor executable is missing")
    if not args.run_id or any(c not in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_" for c in args.run_id):
        parser.error("run-id may contain letters, numbers, hyphens and underscores")
    # Process inventory complements (not replaces) the team's explicit scheduling agreement.
    processes = subprocess.run(["ps", "-axo", "pid=,command="], text=True, capture_output=True, check=True).stdout
    conflicts = [line.strip() for line in processes.splitlines()
                 if "/Contents/MacOS/Unity" in line and "-projectPath" in line and str(project) in line]
    if conflicts:
        raise SystemExit("Project already has a Unity process:\n" + "\n".join(conflicts))
    output = ROOT / "verification" / "unity" / project.name / args.run_id
    if output.exists():
        raise SystemExit("Refusing to overwrite an existing evidence directory: " + str(output))
    output.mkdir(parents=True)
    report = output / "report.json"
    method = "RunBatch" if args.mode == "batch" else "RunPlaySmoke"
    command = [str(args.editor), "-batchmode", "-projectPath", str(project),
               "-executeMethod", "Aquarium.Validation.UnitySliceAcceptance." + method,
               "-aquariumReport", str(report), "-logFile", str(output / "editor.log")]
    if args.mode == "batch":
        command += ["-nographics", "-quit"]
    tracked = sorted((project / "Assets" / "_Game").rglob("*.cs"))
    tracked += sorted((project / "Assets" / "_Game").rglob("*.asmdef"))
    tracked += [project / "ProjectSettings/ProjectVersion.txt", project / "Packages/manifest.json",
                project / "Assets/_Game/Scenes/Aquarium.unity", project / "Assets/_Game/Resources/Configuration/runtime.json"]
    metadata = {"started_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "command": command,
                "mode": args.mode, "status": "running", "project_path": str(project),
                "expected_unity": expected_unity, "expected_urp": expected_urp,
                "sources_sha256": {str(p.relative_to(project)): hashlib.sha256(p.read_bytes()).hexdigest() for p in tracked}}
    metadata_path = output / "invocation.json"
    metadata_path.write_text(json.dumps(metadata, indent=2) + "\n")
    print("Running real Unity; evidence:", output, flush=True)
    try:
        with (output / "stdout.log").open("w") as stream:
            result = subprocess.run(command, cwd=ROOT, stdout=stream, stderr=subprocess.STDOUT, timeout=args.timeout)
        metadata["exit_code"] = result.returncode
        payload = json.loads(report.read_text()) if report.exists() else {}
        metadata["status"] = "passed" if result.returncode == 0 and payload.get("status") == "passed" else "failed"
        metadata["roundtrip_diagnostic"] = roundtrip_diagnostic(output)
    except subprocess.TimeoutExpired:
        metadata["status"] = "timed_out"
        metadata["failure"] = "Unity did not complete before the bounded host timeout"
    except Exception as error:
        metadata["status"] = "failed"
        metadata["failure"] = str(error)
    metadata["finished_utc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    metadata_path.write_text(json.dumps(metadata, indent=2) + "\n")
    print(metadata["status"], output)
    return 0 if metadata["status"] == "passed" else 1


if __name__ == "__main__":
    sys.exit(main())
