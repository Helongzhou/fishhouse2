#!/usr/bin/env python3
"""Run existing domain checks against an explicitly selected Unity project's actual C# sources."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import sys
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
SUITES = {"behavior": ROOT / "unity/Verification/Program.cs",
          "compatibility": ROOT / "compatibility-checks/Program.cs",
          "ecology": ROOT / "ecology-checks/Program.cs",
          "gameplay": ROOT / "gameplay-checks/Program.cs",
          "integration": ROOT / "integration-checks/Program.cs"}


def project_file(path, sources, framework="net8.0", executable=True):
    includes = "\n".join('    <Compile Include="' + escape(str(p), {'"': '&quot;'}) + '" />' for p in sources)
    path.write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>''' + ("Exe" if executable else "Library") + '''</OutputType><TargetFramework>''' + framework + '''</TargetFramework>
    <LangVersion>7.3</LangVersion><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
''' + includes + '''
  </ItemGroup>
</Project>
''')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=ROOT / "unity")
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--dotnet", type=Path, default=ROOT / "unity/.tools/dotnet/dotnet")
    args = parser.parse_args()
    selected = args.project.resolve()
    core = selected / "Assets/_Game/Core"
    localization = selected / "Assets/_Game/UnityAdapter/DemoLocalization.cs"
    if not core.is_dir() or not localization.is_file() or not args.dotnet.is_file():
        parser.error("Selected project sources or the .NET executable are missing")
    if not args.run_id or any(c not in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_" for c in args.run_id):
        parser.error("run-id may contain letters, numbers, hyphens and underscores")
    output = ROOT / "verification/unity" / selected.name / args.run_id
    if output.exists():
        raise SystemExit("Refusing to overwrite evidence: " + str(output))
    output.mkdir(parents=True)
    stage = output / "stage"
    stage.mkdir()
    # Existing Programs retain their path discovery and all assertions unchanged.
    # Behavior discovers Assets, integration discovers data/unity, compatibility discovers data+unity.
    roots = {"Assets": selected / "Assets", "unity": selected, "data": ROOT / "data"}
    for name, target in roots.items():
        (stage / name).symlink_to(target, target_is_directory=True)
    sources = sorted(core.rglob("*.cs"))
    tracked = sources + [localization] + list(SUITES.values())
    tracked += [selected / "Assets/_Game/Resources/Configuration/runtime.json"]
    tracked += sorted((selected / "Assets/_Game/Resources/Localization").glob("*.json"))
    report = {"status": "running", "project": str(selected),
              "started_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "resource_roots": {str(stage / k): str(v) for k, v in roots.items()},
              "sources_sha256": {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in tracked}, "checks": [],
              "coverage": "Production pure C# from the selected project and unchanged existing Program.cs checks. This does not replace real Unity adapter/import/Play acceptance."}
    report_path = output / "report.json"
    report_path.write_text(json.dumps(report, indent=2) + "\n")
    jobs = [("Core-netstandard2.1", sources, "netstandard2.1", False)]
    jobs += [(name, sources + [localization, program], "net8.0", True) for name, program in SUITES.items()]
    for name, inputs, framework, executable in jobs:
        suite_dir = stage / name
        suite_dir.mkdir()
        csproj = suite_dir / (name + ".csproj")
        project_file(csproj, inputs, framework, executable)
        command = [str(args.dotnet.resolve()), "run" if executable else "build", "--project" if executable else str(csproj)]
        if executable:
            command += [str(csproj)]
        command += ["--configuration", "Release", "-p:UseSharedCompilation=false"]
        result = subprocess.run(command, cwd=stage, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        (output / (name + ".log")).write_text(result.stdout)
        record = {"name": name, "exit_code": result.returncode, "command": command, "tail": result.stdout.strip().splitlines()[-5:]}
        report["checks"].append(record)
        report_path.write_text(json.dumps(report, indent=2) + "\n")
        print(name, "passed" if result.returncode == 0 else "FAILED", " | ".join(record["tail"][-2:]), flush=True)
    changed = [str(p) for p in tracked if hashlib.sha256(p.read_bytes()).hexdigest() != report["sources_sha256"][str(p)]]
    report["changed_sources_during_run"] = changed
    report["status"] = "passed" if not changed and all(x["exit_code"] == 0 for x in report["checks"]) else "failed"
    report["finished_utc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    report_path.write_text(json.dumps(report, indent=2) + "\n")
    print(report["status"], output)
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    sys.exit(main())
