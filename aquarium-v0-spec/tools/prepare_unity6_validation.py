#!/usr/bin/env python3
"""Prepare an isolated Unity 6 migration candidate without opening Unity or changing the 2022 baseline."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

ROOT=Path(__file__).resolve().parents[1]
EDITOR='6000.6.8f1'
REVISION='237b6593792b'
URP='17.6.0'

def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def prepare(root,destination):
    source=root/'unity'
    if destination.exists():raise ValueError('Refusing to overwrite an existing validation project: '+str(destination))
    if destination.resolve()==source.resolve() or source.resolve() in destination.resolve().parents:raise ValueError('Destination must be separate from the baseline project')
    source_files={str(p.relative_to(source)):digest(p) for p in (source/'Assets/_Game').rglob('*') if p.is_file() and p.name!='.DS_Store'}
    for relative in ['Packages/manifest.json','ProjectSettings/ProjectVersion.txt','ProjectSettings/EditorBuildSettings.asset']:
        source_files[relative]=digest(source/relative)
    destination.mkdir(parents=True)
    shutil.copytree(source/'Assets/_Game',destination/'Assets/_Game',ignore=shutil.ignore_patterns('.DS_Store'))
    (destination/'Packages').mkdir();(destination/'ProjectSettings').mkdir()
    manifest=json.loads((source/'Packages/manifest.json').read_text())
    manifest['dependencies']['com.unity.render-pipelines.universal']=URP
    (destination/'Packages/manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    (destination/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: '+EDITOR+'\nm_EditorVersionWithRevision: '+EDITOR+' ('+REVISION+')\n')
    shutil.copy2(source/'ProjectSettings/EditorBuildSettings.asset',destination/'ProjectSettings/EditorBuildSettings.asset')
    setup=destination/'Assets/_Game/Editor/AquariumProjectSetup.cs'
    text=setup.read_text();old='GraphicsSettings.renderPipelineAsset = pipeline;'
    if text.count(old)!=1:raise ValueError('Unexpected baseline setup API; review migration patch before copying')
    setup.write_text(text.replace(old,'GraphicsSettings.defaultRenderPipeline = pipeline;'))
    migration_patch=root/'tools/unity6_patches/inline-substrate-schema2.patch'
    subprocess.run(['patch','-p1','-i',str(migration_patch.resolve())],cwd=destination,check=True,capture_output=True,text=True)
    macos_build_patch=root/'tools/unity6_patches/macos-arm64-mono-build.patch'
    subprocess.run(['patch','-p1','-i',str(macos_build_patch.resolve())],cwd=destination,check=True,capture_output=True,text=True)
    for font_name in ['NotoSansCJKsc-Regular.otf','NotoSansCJKsc-Regular.otf.meta','OFL-1.1.txt','NOTICE.txt']:
        source_font = root/'tools/unity6_font_assets'/font_name
        target_font = destination/'Assets/_Game/Resources/Fonts'/font_name
        target_font.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source_font, target_font)
    for font_patch_name in ['bundled-font-AquariumGameEntry.patch','bundled-font-AquariumInteractionDemo.patch']:
        font_patch = root/'tools/unity6_patches'/font_patch_name
        subprocess.run(['patch','-p1','-i',str(font_patch.resolve())],cwd=destination,check=True,capture_output=True,text=True)
    font_validation_patch = root/'tools/unity6_patches/bundled-font-validation.patch'
    subprocess.run(['patch','-p1','-i',str(font_validation_patch.resolve())],cwd=destination,check=True,capture_output=True,text=True)
    responsive_patch = root/'tools/unity6_patches/responsive-aquarium-ui.patch'
    subprocess.run(['patch','-p1','-i',str(responsive_patch.resolve())],cwd=destination,check=True,capture_output=True,text=True)
    visibility_patch = root/'tools/unity6_patches/visibility-lighting.patch'
    subprocess.run(['patch','-p1','-i',str(visibility_patch.resolve())],cwd=destination,check=True,capture_output=True,text=True)
    (destination/'.gitignore').write_text('Library/\nTemp/\nLogs/\nUserSettings/\nObj/\nBuilds/\n*.csproj\n*.sln\n')
    report={'kind':'isolated_migration_candidate','source_project':'unity','editor':EDITOR,'revision':REVISION,'urp_candidate':URP,'unity_import_play_build':'not_run','source_sha256':source_files,
            'visibility_patch_sha256':digest(visibility_patch),'migration_patch_sha256':digest(migration_patch),'macos_build_patch_sha256':digest(macos_build_patch),'font_patch_sha256':{name:digest(root/'tools/unity6_patches'/name) for name in ['bundled-font-AquariumGameEntry.patch','bundled-font-AquariumInteractionDemo.patch','bundled-font-validation.patch','responsive-aquarium-ui.patch']},'bundled_font_sha256':{name:digest(root/'tools/unity6_font_assets'/name) for name in ['NotoSansCJKsc-Regular.otf','NotoSansCJKsc-Regular.otf.meta','OFL-1.1.txt','NOTICE.txt']},'intentional_changes':['Gameplay snapshot schema2 and explicit substrate presence (real JsonUtility batch-01 fix)','Packages/manifest.json: URP '+URP,'ProjectSettings/ProjectVersion.txt: '+EDITOR+' ('+REVISION+')','Assets/_Game/Editor/AquariumProjectSetup.cs: GraphicsSettings.defaultRenderPipeline and macOS ARM64 Mono build/report entry','Assets/_Game/Resources/Fonts/NotoSansCJKsc-Regular.otf: OFL bundled Latin + Simplified Chinese face; all runtime font_group paths avoid OS dynamic fonts','Editor validation: Resources.Load<Font> must return the bundled face','AquariumGameEntry: explicit flat ambient/probe, fixed main sun and readable empty-tank materials independent of ecological light','AquariumGameEntry: identity-matrix physical responsive panel and persistent font styles with measured text heights'],
            'validation_source_sha256':{str(p.relative_to(destination)):digest(p) for p in (destination/'Assets/_Game/Editor/Validation').rglob('*') if p.is_file()}}
    (destination/'migration-source-manifest.json').write_text(json.dumps(report,indent=2)+'\n')
    if any(digest(source/relative)!=value for relative,value in source_files.items()):raise ValueError('Baseline changed during candidate preparation; inspect before validation')
    print(json.dumps({'project':str(destination),'editor_candidate':EDITOR,'urp_candidate':URP,'copied_source_files':len(source_files),'baseline_unchanged':True,'unity_launched':False},indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--root',type=Path,default=ROOT);parser.add_argument('--destination',type=Path)
    args=parser.parse_args();prepare(args.root,args.destination or args.root/'unity6-validation')
