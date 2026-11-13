#!/usr/bin/env python3
"""Check registered locale packages, fallbacks, sources and Unity exports.

Run --self-test for isolated third-language, draft and rejection scenarios.
No official locale registry, translation, CSV or art file is changed by tests.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
from pathlib import Path
import re
import shutil
import tempfile

from build_catalogs import (
    ROOT, REGISTRY_PATH, RESOURCE_PATH, LocalizationError, build_catalogs,
    load_registry, placeholders, prepare_catalogs, protected_biological_names,
    pseudo_localize, read_catalog, read_json, require, runtime_payload, write_json,
)


def validate_sources_and_models(root, english):
    refs = read_json(root / 'sources/references.json')
    require(refs.get('localization_registry') == REGISTRY_PATH.as_posix(),
            'References must point to the authoritative locale registry')
    require('supported_languages' not in refs, 'Remove obsolete language list from research references')
    references = {entry['id']: entry for entry in refs['references']}
    require(len(references) == len(refs['references']), 'Duplicate reference ID')
    for file, id_key, prefix in [('species', 'species_id', 'species'), ('plants', 'id', 'plant'),
                                 ('items', 'item_id', 'item'), ('tanks', 'tank_id', 'tank')]:
        with (root / 'data' / (file + '.csv')).open(encoding='utf-8-sig', newline='') as source:
            rows = list(csv.DictReader(source))
        for row in rows:
            source_id = row.get('source_id')
            if source_id:
                require(source_id in references, 'Unregistered source: ' + source_id)
                require(row['source_name_en'] == references[source_id]['source_name_en'],
                        'Source name changed: ' + row[id_key])
                require(row['display_name_en'] == references[source_id]['source_name_en'],
                        'English display name differs from verified source: ' + row[id_key])
            key = prefix + '.' + row[id_key] + '.name'
            if row['display_name_en']:
                require(english.get(key) == row['display_name_en'], 'Stale English source name: ' + key)
            elif row.get('content_status') in {'shipping', 'shipping_candidate'}:
                raise LocalizationError('Unverified English name in default content: ' + key)
    # Other locale names are translated in their packages. Never force them to
    # equal English/Chinese CSV columns or add one CSV column per language.
    reason_source = read_json(root / 'compatibility-checks/reason-catalog.json')
    for key, text in reason_source.get('en', {}).items():
        require(english.get(key) == text, 'English compatibility authoring source is stale: ' + key)
    manifest = read_json(root / 'art/reference_manifest.json')
    ids = set()
    for asset in manifest['assets']:
        require(asset['asset_id'] not in ids, 'Duplicate art task')
        ids.add(asset['asset_id'])
        require(asset['source_id'] in references, 'Unregistered art source')
        reference = references[asset['source_id']]
        require(asset['display_name_en'] == reference['source_name_en'], 'Art English name mismatch')
        require(asset['model_status'] in {'not_started', 'in_progress', 'review', 'complete'},
                'Unknown model status')
        require(asset['source_url'] == reference['url'], 'Art source URL mismatch')
        require(asset['scientific_name'] == reference['scientific_name'], 'Art scientific name mismatch')
    return len(ids)


def check_localization(root):
    root = Path(root)
    registry, catalogs, warnings = prepare_catalogs(root)
    english = catalogs[registry['default_language']]
    for key, text in english.items():
        require(not re.search(r'[\u4e00-\u9fff]', text), 'Chinese text leaked into English: ' + key)
    runtime_registry = read_json(root / RESOURCE_PATH / 'locales.json')
    require(runtime_registry == registry, 'Stale Unity locale registry')
    for code, catalog in catalogs.items():
        runtime = read_json(root / RESOURCE_PATH / (code + '.json'))
        require(isinstance(runtime, dict) and runtime.get('language') == code, 'Runtime language mismatch: ' + code)
        entries = runtime.get('entries')
        require(isinstance(entries, list), 'Runtime entries must be an array: ' + code)
        require(all(isinstance(entry, dict) and set(entry) == {'key', 'value'} for entry in entries),
                'Invalid runtime entry: ' + code)
        require(len(entries) == len({entry['key'] for entry in entries}), 'Duplicate Unity resource key: ' + code)
        require(runtime == runtime_payload(code, catalog), 'Stale Unity catalog: ' + code)
        if code == 'qps-ploc':
            require(read_catalog(root / 'data/localization/qps-ploc.json') == catalog,
                    'Stale generated pseudolocalization source')
    exported_codes = {path.stem for path in (root / RESOURCE_PATH).glob('*.json') if path.stem != 'locales'}
    require(exported_codes == set(catalogs), 'Unregistered or planned runtime catalog exported')
    art_count = validate_sources_and_models(root, english)
    locales = registry['locales']
    return {
        'ok': True,
        'source_keys': len(english),
        'catalogs': {code: len(catalog) for code, catalog in catalogs.items()},
        'counts': {status: sum(item['status'] == status for item in locales)
                   for status in ('ready', 'draft', 'planned', 'qa')},
        'ready_locales': [item['code'] for item in locales if item['status'] == 'ready'],
        'draft_locales': [item['code'] for item in locales if item['status'] == 'draft'],
        'qa_locales': [item['code'] for item in locales if item['status'] == 'qa'],
        'planned_locales': [item['code'] for item in locales if item['status'] == 'planned'],
        'art_tasks': art_count,
        'warnings': warnings,
    }


def add_fixture_locale(root, status='ready', required=True, code='it', native_name='Italiano',
                       english_name='Italian (test fixture)', culture='it-IT',
                       steam_language='italian', font_group='latin'):
    registry = read_json(root / REGISTRY_PATH)
    require(not any(item['code'] == code for item in registry['locales']), f'Fixture locale {code} already registered')
    registry['locales'].append({
        'code': code, 'native_name': native_name, 'english_name': english_name,
        'culture': culture, 'steam_language': steam_language, 'direction': 'ltr',
        'font_group': font_group, 'status': status, 'fallback': 'en', 'release_required': required,
    })
    write_json(root / REGISTRY_PATH, registry)
    english = read_catalog(root / 'data/localization/en.json')
    # Test-only text is deliberately not advertised as a production translation.
    translated = dict(english)
    translated['ui.save'] = 'Salva'
    translated['species.ember_tetra.name'] = 'Nome locale di prova'
    write_json(root / 'data/localization' / (code + '.json'), translated)
    return translated


def copy_fixture(root, destination):
    for folder in ('data/localization', 'unity/Assets/_Game/Resources/Localization'):
        shutil.copytree(root / folder, destination / folder)
    for relative in ('data/species.csv', 'data/plants.csv', 'data/items.csv', 'data/tanks.csv',
                     'sources/references.json', 'art/reference_manifest.json',
                     'compatibility-checks/reason-catalog.json'):
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(root / relative, target)


def digest_protected_files(root):
    files = list((root / 'data').glob('*.csv')) + [root / 'art/reference_manifest.json']
    return {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in files}


def run_self_tests(root):
    results = []
    official_registry = (root / REGISTRY_PATH).read_bytes()
    protected_before = digest_protected_files(root)
    with tempfile.TemporaryDirectory(prefix='aquarium-i18n-') as temp:
        fixture = Path(temp) / 'third-language'
        copy_fixture(root, fixture)
        translated = add_fixture_locale(fixture)
        source_paths = [fixture / ('data/localization/' + code + '.json') for code in ('en', 'zh-CN', 'it')]
        hashes_before = {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in source_paths}
        untouched = digest_protected_files(fixture)
        build_catalogs(fixture)
        report = check_localization(fixture)
        require('it' in report['ready_locales'] and report['catalogs']['it'] == report['source_keys'],
                'Third locale was not discovered dynamically')
        require(read_catalog(fixture / 'data/localization/it.json') == translated, 'Third-party translations were overwritten')
        require(hashes_before == {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in source_paths},
                'Authoring translations were modified')
        require(untouched == digest_protected_files(fixture), 'Generator modified CSV or art data')
        results.append('third locale discovered/exported; translated biological name accepted; source translations and art/CSV preserved')

    with tempfile.TemporaryDirectory(prefix='aquarium-i18n-') as temp:
        fixture = Path(temp) / 'game-support-only'
        copy_fixture(root, fixture)
        add_fixture_locale(fixture, code='hi', native_name='हिन्दी', english_name='Hindi (test fixture)',
                           culture='hi-IN', steam_language='', font_group='devanagari')
        build_catalogs(fixture)
        report = check_localization(fixture)
        require('hi' in report['ready_locales'], 'Game-support-only locale was blocked by an empty Steam API code')
        require(next(item for item in load_registry(fixture)['locales'] if item['code'] == 'hi')['steam_language'] == '',
                'Empty Steam API mapping was not preserved')
        results.append('game-support-only ready locale with empty Steam API code accepted/exported; manual/OS selection remains available')

    rejection_cases = (
        ('missing release-required locale', 'missing_required', 'missing required locale file'),
        ('required locale missing keys', 'missing_keys', 'missing'),
        ('parameter mismatch', 'parameter', 'format parameter mismatch'),
        ('unknown localization key', 'unknown_key', 'unknown localization keys'),
        ('fallback cycle', 'cycle', 'fallback cycle'),
        ('fallback self-loop', 'self_loop', 'fallback cycle'),
        ('unregistered fallback', 'unknown_fallback', 'unregistered fallback'),
        ('duplicate Steam API mapping (case-insensitive)', 'duplicate_steam', 'duplicate Steam API language mapping'),
    )
    for label, kind, expected in rejection_cases:
        with tempfile.TemporaryDirectory(prefix='aquarium-i18n-') as temp:
            fixture = Path(temp) / 'case'
            copy_fixture(root, fixture)
            translated = add_fixture_locale(fixture)
            registry = read_json(fixture / REGISTRY_PATH)
            if kind == 'missing_required':
                (fixture / 'data/localization/it.json').unlink()
            elif kind == 'missing_keys':
                translated.pop('ui.save')
            elif kind == 'parameter':
                translated['ui.wallet'] = 'Coins: {7}'
            elif kind == 'unknown_key':
                translated['ui.typo_not_in_source'] = 'Unexpected'
            elif kind == 'cycle':
                registry['locales'][-1]['fallback'] = 'zh-CN'
                next(item for item in registry['locales'] if item['code'] == 'zh-CN')['fallback'] = 'it'
            elif kind == 'self_loop':
                registry['locales'][-1]['fallback'] = 'it'
            elif kind == 'unknown_fallback':
                registry['locales'][-1]['fallback'] = 'not-registered'
            elif kind == 'duplicate_steam':
                registry['locales'][-1]['steam_language'] = 'ENGLISH'
            if kind != 'missing_required':
                write_json(fixture / 'data/localization/it.json', translated)
            write_json(fixture / REGISTRY_PATH, registry)
            try:
                build_catalogs(fixture)
            except LocalizationError as exc:
                require(expected in str(exc), f'{label}: wrong rejection: {exc}')
            else:
                raise LocalizationError(f'{label}: invalid locale package was accepted')
            results.append(label + ' rejected')

    with tempfile.TemporaryDirectory(prefix='aquarium-i18n-') as temp:
        fixture = Path(temp) / 'draft'
        copy_fixture(root, fixture)
        add_fixture_locale(fixture, status='draft', required=False)
        draft = {'ui.save': 'Salva'}
        write_json(fixture / 'data/localization/it.json', draft)
        build = build_catalogs(fixture)
        report = check_localization(fixture)
        require(build['warnings'] and report['catalogs']['it'] == 1, 'Partial draft was not warned/exported sparsely')
        require(read_catalog(fixture / 'data/localization/it.json') == draft, 'Draft translation overwritten')
        runtime = read_json(fixture / RESOURCE_PATH / 'it.json')
        require('ui.wallet' not in {entry['key'] for entry in runtime['entries']}, 'Fallback text incorrectly claimed as translated draft')
        require(next(item for item in load_registry(fixture)['locales'] if item['code'] == 'it')['fallback'] == 'en',
                'Draft does not have an English fallback')
        results.append('partial draft warns, preserves translation, exports sparse keys and English fallback')

    protected = protected_biological_names(root)
    sample = 'Ember Tetra {{literal}} {0:F1} {source} nearby'
    pseudo = pseudo_localize(sample, protected)
    require('Ember Tetra' in pseudo and '{{literal}}' in pseudo and '{0:F1}' in pseudo and '{source}' in pseudo,
            'Pseudolocalization damaged canonical names or tokens')
    require(placeholders(sample) == placeholders(pseudo) and len(pseudo) > len(sample) * 1.3,
            'Pseudolocalization lost parameters or expansion')
    require(pseudo.startswith('⟦') and pseudo.endswith('⟧') and pseudo != sample,
            'Pseudolocalization is not visibly marked')
    results.append('pseudolocalization preserves canonical names/format tokens, adds accents/brackets and expansion')
    require((root / REGISTRY_PATH).read_bytes() == official_registry, 'Self-tests polluted official supported locales')
    require(digest_protected_files(root) == protected_before, 'Self-tests changed official CSV/art data')
    return {'ok': True, 'self_tests': len(results), 'results': results,
            'official_registry_unchanged': True, 'official_csv_and_art_unchanged': True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    parser.add_argument('--json', action='store_true')
    parser.add_argument('--self-test', action='store_true', help='Run isolated fixture tests without adding production languages')
    args = parser.parse_args()
    try:
        report = run_self_tests(args.root) if args.self_test else check_localization(args.root)
    except (LocalizationError, OSError, KeyError, TypeError) as exc:
        parser.exit(1, f'Localization check failed: {exc}\n')
    if args.json:
        print(json.dumps(report, ensure_ascii=False, indent=2))
    elif args.self_test:
        print('PASS:', report['self_tests'], 'isolated internationalization cases')
        for result in report['results']:
            print('PASS:', result)
    else:
        print(f'PASS: {report["source_keys"]} source keys; registered catalogs {report["catalogs"]}; '
              f'{report["art_tasks"]} source/model tasks checked')
        print('Ready:', ', '.join(report['ready_locales']), '| QA:', ', '.join(report['qa_locales']))
        print('Planned only:', ', '.join(report['planned_locales']))
        for warning in report['warnings']:
            print('WARNING:', warning)


if __name__ == '__main__':
    main()
