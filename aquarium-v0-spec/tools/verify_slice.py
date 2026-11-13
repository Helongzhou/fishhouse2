#!/usr/bin/env python3
"""Run the source slice checks. Does not run Unity or certify a playable build."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[1]
DOTNET=ROOT/'unity/.tools/dotnet/dotnet'
if not DOTNET.exists(): DOTNET=Path('dotnet')

def run(name,args,expected=0):
    result=subprocess.run([str(a) for a in args],cwd=ROOT,text=True,capture_output=True)
    if result.returncode!=expected:
        print(result.stdout+result.stderr);raise SystemExit(f'FAIL {name}: exit {result.returncode}, expected {expected}')
    lines=(result.stdout+result.stderr).strip().splitlines()
    print(name+': '+ (' | '.join(lines[-2:]) if lines else 'PASS'))
    return {'name':name,'exit_code':result.returncode,'tail':lines[-4:]},result.stdout

def main():
    records=[]
    protected=list((ROOT/'data').glob('*.csv'))+[ROOT/'sources/references.json',ROOT/'art/reference_manifest.json']
    hashes={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
    for name,args in [('runtime export',[sys.executable,'tools/build_runtime_config.py']),('locale export',[sys.executable,'tools/build_catalogs.py']),('locale coverage',[sys.executable,'tools/check_localization.py']),('locale isolated self-tests',[sys.executable,'tools/check_localization.py','--self-test']),('Core netstandard2.1',[DOTNET,'build','unity/Verification/Aquarium.Core.csproj','--configuration','Release','-p:BaseIntermediateOutputPath=obj/Core/','-p:OutputPath=bin/Core/Release/'])]:
        result,_=run(name,args);records.append(result)
    for name,project in [('behavior','unity/Verification/BehaviorChecks.csproj'),('compatibility','compatibility-checks/CompatibilityChecks.csproj'),('ecology','ecology-checks/EcologyChecks.csproj'),('gameplay','gameplay-checks/GameplayChecks.csproj'),('integration','integration-checks/IntegrationChecks.csproj')]:
        result,_=run(name,[DOTNET,'run','--project',project,'--configuration','Release']);records.append(result)
    result,output=run('strict release negative gate',[sys.executable,'tools/validate_config.py','--strict-release','--json'],1)
    strict=json.loads(output)
    if strict['ok'] or not any('Release gate blocked' in error for error in strict['errors']):raise SystemExit('FAIL strict gate did not block pending review')
    records.append(result)
    if any(hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=digest for p,digest in hashes.items()):raise SystemExit('FAIL source CSV/research/art changed during export or checks')
    runtime=ROOT/'unity/Assets/_Game/Resources/Configuration/runtime.json';digest=hashlib.sha256(runtime.read_bytes()).hexdigest()
    result,_=run('repeat deterministic runtime export',[sys.executable,'tools/build_runtime_config.py']);records.append(result)
    if hashlib.sha256(runtime.read_bytes()).hexdigest()!=digest:raise SystemExit('FAIL runtime export is not deterministic')
    report={'status':'source_checks_passed','unity_editor_play_build':'not_run_no_editor','pending_release_reviews':len(strict['warnings']),'checks':records,'protected_sources_sha256':hashes}
    target=ROOT/'verification/latest.json';target.parent.mkdir(exist_ok=True);target.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print('PASS source slice; Unity import/Play/player remain unverified; '+str(len(strict['warnings']))+' release reviews remain pending.')

if __name__=='__main__':main()
