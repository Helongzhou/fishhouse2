#!/usr/bin/env python3
"""Validate registered locale packages and export Unity resources.

Translation sources are data/localization/<code>.json. They are read, never
regenerated from CSV or overwritten. Only the QA qps-ploc package is generated.
The locale registry is the sole language list. No CSV or art manifest is written.
"""
from __future__ import annotations

import argparse
from collections import Counter
import csv
import json
import math
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
REGISTRY_PATH = Path('data/localization/locales.json')
RESOURCE_PATH = Path('unity/Assets/_Game/Resources/Localization')
LOCALE_STATUSES = {'ready', 'draft', 'planned', 'qa'}
LOCALE_FIELDS = {'code', 'native_name', 'english_name', 'culture', 'steam_language',
                 'direction', 'font_group', 'status', 'fallback', 'release_required'}
PARAMETER = re.compile(r'(?P<name>[A-Za-z_][A-Za-z0-9_]*|\d+)(?:\s*,\s*(?P<alignment>[+-]?\d+))?(?::(?P<format>[^{}]+))?')
ACCENTS = str.maketrans({
    'a': 'à', 'b': 'ƀ', 'c': 'ç', 'd': 'ď', 'e': 'ë', 'f': 'ƒ', 'g': 'ğ',
    'h': 'ħ', 'i': 'ï', 'j': 'ĵ', 'k': 'ķ', 'l': 'ľ', 'm': 'ṁ', 'n': 'ñ',
    'o': 'ô', 'p': 'þ', 'q': 'ʠ', 'r': 'ř', 's': 'š', 't': 'ţ', 'u': 'ü',
    'v': 'ṽ', 'w': 'ŵ', 'x': 'ẋ', 'y': 'ÿ', 'z': 'ž',
    'A': 'À', 'B': 'Ƀ', 'C': 'Ç', 'D': 'Ď', 'E': 'Ë', 'F': 'Ƒ', 'G': 'Ğ',
    'H': 'Ħ', 'I': 'Ï', 'J': 'Ĵ', 'K': 'Ķ', 'L': 'Ľ', 'M': 'Ṁ', 'N': 'Ñ',
    'O': 'Ô', 'P': 'Þ', 'Q': 'Ɋ', 'R': 'Ř', 'S': 'Š', 'T': 'Ţ', 'U': 'Ü',
    'V': 'Ṽ', 'W': 'Ŵ', 'X': 'Ẋ', 'Y': 'Ÿ', 'Z': 'Ž',
})


class LocalizationError(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise LocalizationError(message)


def read_json(path):
    path = Path(path)
    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, f'{path}: duplicate JSON key {key}')
            result[key] = value
        return result
    try:
        return json.loads(path.read_text(encoding='utf-8'), object_pairs_hook=unique)
    except (OSError, json.JSONDecodeError) as exc:
        raise LocalizationError(f'{path}: {exc}') from exc


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def load_registry(root):
    registry = read_json(root / REGISTRY_PATH)
    require(isinstance(registry, dict), 'Locale registry must be an object')
    require(set(registry) == {'schema_version', 'default_language', 'fallback_language', 'locales'},
            'Unknown or missing locale registry field')
    require(type(registry['schema_version']) is int and registry['schema_version'] == 1,
            'Unsupported locale registry schema_version')
    require(registry['default_language'] == 'en', 'Default language must be en')
    require(registry['fallback_language'] == 'en', 'Global fallback language must be en')
    require(isinstance(registry['locales'], list) and registry['locales'], 'Registry needs locales')
    indexed = {}
    seen_folded = set()
    steam_mappings = set()
    for locale in registry['locales']:
        require(isinstance(locale, dict) and set(locale) == LOCALE_FIELDS,
                'Unknown or missing locale metadata field')
        code = locale['code']
        require(isinstance(code, str) and re.fullmatch(r'[A-Za-z][A-Za-z0-9]*(?:-[A-Za-z0-9]+)*', code),
                f'Unsafe or invalid locale code: {code}')
        require(code.casefold() not in seen_folded, f'Duplicate locale code: {code}')
        seen_folded.add(code.casefold())
        for field in ('native_name', 'english_name', 'culture', 'font_group'):
            require(isinstance(locale[field], str) and locale[field].strip(), f'{code}: missing {field}')
        require(isinstance(locale['steam_language'], str), f'{code}: steam_language must be a string')
        require(locale['steam_language'] == locale['steam_language'].strip(), f'{code}: trim Steam language metadata')
        steam_id = locale['steam_language'].casefold()
        if steam_id:
            require(steam_id not in steam_mappings, f'{code}: duplicate Steam API language mapping: {locale["steam_language"]}')
            steam_mappings.add(steam_id)
        require(locale['direction'] in {'ltr', 'rtl'}, f'{code}: invalid direction')
        require(locale['status'] in LOCALE_STATUSES, f'{code}: invalid locale status')
        require(type(locale['release_required']) is bool, f'{code}: release_required must be boolean')
        require(isinstance(locale['fallback'], str), f'{code}: fallback must be a code or empty root')
        require(not (locale['status'] in {'planned', 'qa'} and locale['release_required']),
                f'{code}: planned/QA locale cannot be a required release language')
        indexed[code] = locale
    require('en' in indexed, 'Default en locale is not registered')
    require(indexed['en']['status'] == 'ready' and indexed['en']['release_required'],
            'Default en locale must be ready and release_required')
    require(indexed['en']['fallback'] == '', 'Root en locale must have an empty fallback')
    for code, locale in indexed.items():
        if code != 'en':
            require(locale['fallback'] in indexed, f'{code}: unregistered fallback {locale["fallback"]}')
            require(indexed[locale['fallback']]['status'] != 'planned', f'{code}: fallback points to planned locale')
        cursor, visited = code, set()
        while cursor:
            require(cursor not in visited, f'{code}: fallback cycle detected at {cursor}')
            visited.add(cursor)
            target = indexed[cursor]['fallback']
            require(not target or target in indexed, f'{code}: unknown fallback {target}')
            cursor = target
        require('en' in visited, f'{code}: fallback chain does not reach en')
    return registry


def placeholders(text):
    """Return parameter signatures, validating escaped braces and C# formats."""
    result = Counter()
    i = 0
    while i < len(text):
        if text.startswith('{{', i) or text.startswith('}}', i):
            i += 2
            continue
        char = text[i]
        if char == '{':
            end = text.find('}', i + 1)
            require(end != -1, 'Unclosed format placeholder')
            token = text[i + 1:end]
            match = PARAMETER.fullmatch(token)
            require(match is not None, f'Invalid format placeholder: {{{token}}}')
            alignment = match.group('alignment')
            result[(match.group('name'), str(int(alignment)) if alignment else '', match.group('format') or '')] += 1
            i = end + 1
        else:
            require(char != '}', 'Unescaped closing format brace')
            i += 1
    return result


def read_catalog(path):
    catalog = read_json(path)
    require(isinstance(catalog, dict), f'{path}: catalog must be a key/value object')
    for key, value in catalog.items():
        require(isinstance(key, str) and bool(key.strip()), f'{path}: invalid localization key')
        require(isinstance(value, str) and bool(value.strip()), f'{path}: blank/nonstring translation for {key}')
        try:
            placeholders(value)
        except LocalizationError as exc:
            raise LocalizationError(f'{path}: {key}: {exc}') from exc
    return catalog


def protected_biological_names(root):
    names = set()
    for file in ('species', 'plants'):
        path = root / 'data' / (file + '.csv')
        with path.open(encoding='utf-8-sig', newline='') as source:
            for row in csv.DictReader(source):
                names.update(value for value in (row.get('source_name_en'), row.get('scientific_name')) if value)
    refs = read_json(root / 'sources/references.json')
    for reference in refs['references']:
        if reference.get('scientific_name'):
            names.add(reference['scientific_name'])
            names.add(reference['source_name_en'])
    return sorted(names, key=lambda value: (-len(value), value))


def pseudo_localize(text, protected_names):
    # Preserve exact placeholder bytes, escaped braces, markup and canonical names.
    atoms = [r'\{\{[^{}]*\}\}', r'\{\{', r'\}\}', r'\{[^{}]+\}', r'</?[^<>]+>']
    atoms += [re.escape(name) for name in protected_names if name]
    token_pattern = re.compile('|'.join(atoms))
    parts = []
    start = 0
    for token in token_pattern.finditer(text):
        parts.append(text[start:token.start()].translate(ACCENTS))
        parts.append(token.group())
        start = token.end()
    parts.append(text[start:].translate(ACCENTS))
    padding = '~' * max(4, math.ceil(len(text) * 0.35))
    result = '⟦' + ''.join(parts) + ' ' + padding + '⟧'
    require(placeholders(result) == placeholders(text), 'Pseudolocalization changed format parameters')
    return result


def validate_catalog(code, catalog, english, locale, warnings):
    unknown = set(catalog) - set(english)
    require(not unknown, f'{code}: unknown localization keys: {", ".join(sorted(unknown))}')
    missing = set(english) - set(catalog)
    complete_required = locale['release_required'] or locale['status'] in {'ready', 'qa'}
    require(not missing or not complete_required,
            f'{code}: required/ready locale missing {len(missing)} keys: {", ".join(sorted(missing)[:5])}')
    if missing:
        warnings.append(f'{code}: {len(missing)} untranslated keys; registry fallback applies')
    for key, value in catalog.items():
        require(placeholders(value) == placeholders(english[key]), f'{code}: format parameter mismatch: {key}')


def prepare_catalogs(root, registry=None):
    registry = registry or load_registry(root)
    english = read_catalog(root / 'data/localization' / (registry['default_language'] + '.json'))
    require(bool(english), 'English source catalog cannot be empty')
    catalogs, warnings = {}, []
    protected = protected_biological_names(root)
    for locale in registry['locales']:
        code = locale['code']
        path = root / 'data/localization' / (code + '.json')
        if locale['status'] == 'planned':
            # Translators may start a planned file, but it is not exported/supported.
            if path.exists():
                catalog = read_catalog(path)
                validate_catalog(code, catalog, english, locale, warnings)
            continue
        if code == 'qps-ploc':
            require(locale['status'] == 'qa', 'qps-ploc must remain QA-only')
            catalog = {key: pseudo_localize(value, protected) for key, value in english.items()}
        elif path.exists():
            catalog = read_catalog(path)
        elif locale['status'] == 'draft' and not locale['release_required']:
            catalog = {}
            warnings.append(f'{code}: draft file absent; all keys fall back through registry')
        else:
            raise LocalizationError(f'{code}: missing required locale file: {path}')
        validate_catalog(code, catalog, english, locale, warnings)
        catalogs[code] = dict(sorted(catalog.items()))
    return registry, catalogs, warnings


def runtime_payload(code, catalog):
    return {'language': code, 'entries': [{'key': key, 'value': value} for key, value in sorted(catalog.items())]}


def build_catalogs(root):
    root = Path(root)
    registry, catalogs, warnings = prepare_catalogs(root)
    # All validation happens before writing any generated output.
    resource_dir = root / RESOURCE_PATH
    for code, catalog in catalogs.items():
        if code == 'qps-ploc':
            write_json(root / 'data/localization' / (code + '.json'), catalog)
        write_json(resource_dir / (code + '.json'), runtime_payload(code, catalog))
    write_json(resource_dir / 'locales.json', registry)
    # Resource outputs for removed/planned packages must not claim game support.
    for path in resource_dir.glob('*.json'):
        if path.stem != 'locales' and path.stem not in catalogs:
            path.unlink()
    source_count = len(catalogs[registry['default_language']])
    indexed = {locale['code']: locale for locale in registry['locales']}
    return {'catalogs': {code: len(catalog) for code, catalog in catalogs.items()},
            'counts': {status: sum(locale['status'] == status for locale in registry['locales'])
                       for status in ('ready', 'draft', 'planned', 'qa')},
            'coverage': {code: {'status': indexed[code]['status'], 'translated_keys': len(catalog),
                                'source_keys': source_count,
                                'coverage_percent': round(len(catalog) * 100 / source_count, 2),
                                'release_required': indexed[code]['release_required']}
                         for code, catalog in catalogs.items()},
            'planned': [locale['code'] for locale in registry['locales'] if locale['status'] == 'planned'],
            'warnings': warnings}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT, help='Project root (also supports isolated test fixtures)')
    parser.add_argument('--json', action='store_true')
    args = parser.parse_args()
    try:
        result = build_catalogs(args.root)
    except (LocalizationError, OSError, KeyError) as exc:
        parser.exit(1, f'Localization build failed: {exc}\n')
    if args.json:
        print(json.dumps(result, ensure_ascii=False, indent=2))
    else:
        print('Built registered catalogs:', result['catalogs'])
        print('Locale package states:', result['counts'], '(QA is not a release translation)')
        for code, coverage in result['coverage'].items():
            print(f'{code}: {coverage["status"]} {coverage["translated_keys"]}/{coverage["source_keys"]} keys '
                  f'({coverage["coverage_percent"]}%)')
        print('Planned, not exported:', ', '.join(result['planned']))
        for warning in result['warnings']:
            print('WARNING:', warning)


if __name__ == '__main__':
    main()
