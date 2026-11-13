#!/usr/bin/env python3
"""Stage a real acceptance fixture, retaining and restoring every existing save file."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil

NAMES = ['aquarium-session-v1.json' + suffix for suffix in ['', '.bak1', '.bak2', '.bak3', '.tmp']]

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def write_json(path, value):
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(value, indent=2) + '\n')
    os.replace(temporary, path)

def snapshot(directory, destination):
    destination.mkdir(parents=True, exist_ok=False)
    records = {}
    for name in NAMES:
        source = directory / name
        if source.is_symlink():
            raise ValueError('Refusing a symbolic-link save: ' + str(source))
        exists = source.exists()
        records[name] = {'existed': exists}
        if exists:
            if not source.is_file():
                raise ValueError('Save is not a regular file: ' + str(source))
            shutil.copy2(source, destination / name)
            records[name]['sha256'] = digest(destination / name)
            if records[name]['sha256'] != digest(source):
                raise ValueError('Save changed during backup: ' + name)
    return records

def stage(args):
    directory = args.save_directory.resolve()
    fixture = args.fixture.resolve()
    json.loads(fixture.read_text())
    args.evidence.mkdir(parents=True, exist_ok=True)
    manifest_path = args.evidence / 'save-manifest.json'
    if manifest_path.exists():
        raise ValueError('Existing staging manifest; use a new evidence directory or restore it first.')
    records = snapshot(directory, args.evidence / 'before')
    manifest = {'saveDirectory': str(directory), 'fixture': str(fixture), 'fixtureSha256': digest(fixture),
                'files': records, 'status': 'backed-up'}
    write_json(manifest_path, manifest)
    directory.mkdir(parents=True, exist_ok=True)
    temporary = directory / 'aquarium-acceptance-stage.tmp'
    if temporary.exists():
        raise ValueError('Staging temporary file already exists: ' + str(temporary))
    shutil.copy2(fixture, temporary)
    if digest(temporary) != manifest['fixtureSha256']:
        temporary.unlink()
        raise ValueError('Fixture copy failed verification.')
    os.replace(temporary, directory / NAMES[0])
    manifest['status'] = 'staged'
    write_json(manifest_path, manifest)
    print(json.dumps(manifest, indent=2))

def restore(args):
    manifest_path = args.evidence / 'save-manifest.json'
    manifest = json.loads(manifest_path.read_text())
    if manifest['status'] == 'restored':
        print('Already restored; no files changed.')
        return
    directory = Path(manifest['saveDirectory'])
    for name, record in manifest['files'].items():
        if record['existed'] and digest(args.evidence / 'before' / name) != record['sha256']:
            raise ValueError('Original backup failed verification: ' + name)
    after_manifest = args.evidence / 'after-save-manifest.json'
    if not after_manifest.exists():
        write_json(after_manifest, snapshot(directory, args.evidence / 'after'))
    for name, record in manifest['files'].items():
        target = directory / name
        if target.is_symlink():
            raise ValueError('Refusing a symbolic-link save: ' + str(target))
        if record['existed']:
            shutil.copy2(args.evidence / 'before' / name, target)
            if digest(target) != record['sha256']:
                raise ValueError('Restored save failed verification: ' + name)
        elif target.exists():
            target.unlink()
    manifest['status'] = 'restored'
    write_json(manifest_path, manifest)
    print('Original save files and absence states restored; acceptance saves retained in after/.')

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    command = commands.add_parser('stage')
    command.add_argument('--fixture', type=Path, required=True)
    command.add_argument('--save-directory', type=Path, required=True)
    command.add_argument('--evidence', type=Path, required=True)
    command = commands.add_parser('restore')
    command.add_argument('--evidence', type=Path, required=True)
    args = parser.parse_args()
    try:
        stage(args) if args.command == 'stage' else restore(args)
    except Exception as error:
        parser.exit(1, str(error) + '\n')
