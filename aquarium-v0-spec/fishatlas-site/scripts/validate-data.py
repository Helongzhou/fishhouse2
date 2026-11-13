import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / 'data'
def read(name):
    return json.loads((DATA / name).read_text())
def check(value, message):
    if not value:
        raise AssertionError(message)

CATEGORY_VALUES = {'tetra', 'rasbora', 'barb', 'danio', 'pleco', 'corydoras', 'gourami', 'betta', 'cichlid', 'livebearer', 'catfish', 'shrimp', 'snail', 'other'}
COLOR_VALUES = {'red', 'orange', 'yellow', 'blue', 'green', 'black', 'white', 'silver', 'transparent', 'brown', 'mixed'}
SIZE_VALUES = {'small', 'medium', 'large'}
REPRODUCTION_VALUES = {'egg_laying', 'livebearing', 'unknown'}
SOCIAL_VALUES = {'schooling', 'pairing', 'solitary_tendency', 'colony', 'unknown'}
BODY_SHAPE_VALUES = {'slender', 'deep_bodied', 'laterally_compressed', 'short_round', 'elongated', 'unknown'}
FILTER_LABEL_KEYS = {'categories', 'colors', 'reproduction', 'social', 'body_shape'}
WATER_LAYERS = {'top', 'mid', 'bottom'}
HUSBANDRY_TAGS = {
    'flow_preference': {'minimal', 'gentle', 'moderate', 'strong'},
    'substrate_tags': {'smooth_sand', 'smooth_gravel', 'optional'},
    'habitat_tags': {'plants', 'surface_cover', 'hiding_places', 'wood', 'open_swimming', 'mature_biofilm'},
    'feeding_zone': {'surface', 'midwater', 'bottom'},
    'food_form_tags': {'small_particles', 'floating', 'sinking', 'vegetable', 'biofilm'},
}
NOTE_KEYS = {'oxygen', 'flow', 'space', 'group', 'feeding', 'setup'}
HUSBANDRY_KEYS = set(HUSBANDRY_TAGS) | {'oxygen_need', 'minimum_tank_length_cm', 'group_mode', 'light_preference', 'notes', 'alerts'}

def unique_strings(values, allowed, message):
    check(isinstance(values, list) and values, message)
    check(all(isinstance(value, str) and value.strip() and value in allowed for value in values), message)
    check(len(values) == len(set(values)), f'{message} (duplicate values)')

def expected_size_class(adult_length_cm):
    return 'small' if adult_length_cm <= 4 else 'medium' if adult_length_cm <= 8 else 'large'

species = read('species.json')
localizations = read('species_localizations.json')
profiles = read('care_profiles.json')
guides = read('care_guides.json')
compatibility = read('compatibility_rules.json')
notes = read('content_notes.json')
media = read('media_assets.json')
read('tank_profiles.json'); read('tank_plan_items.json')
ids = {x['id'] for x in species}
check(len(ids) == len(species) and len(species) >= 6, 'species IDs must be unique and include 6 fixtures')
for x in species:
    check(x.get('organism_type') in {'fish', 'shrimp', 'snail'}, f'invalid organism type {x["id"]}')
    check(x['status'] in {'draft','review','published','archived'}, x['id'])
    check(x['content_confidence'] in {'high','medium','low'}, x['id'])
    unique_strings(x.get('category_tags'), CATEGORY_VALUES, f'invalid category tags {x["id"]}')
    unique_strings(x.get('primary_colors'), COLOR_VALUES, f'invalid primary colors {x["id"]}')
    check(x.get('size_class') in SIZE_VALUES, f'invalid size class {x["id"]}')
    check(x.get('reproduction_mode') in REPRODUCTION_VALUES, f'invalid reproduction mode {x["id"]}')
    unique_strings(x.get('social_pattern'), SOCIAL_VALUES, f'invalid social pattern {x["id"]}')
    unique_strings(x.get('body_shape_tags'), BODY_SHAPE_VALUES, f'invalid body shape tags {x["id"]}')
    check('unknown' not in x['social_pattern'] or len(x['social_pattern']) == 1, f'unknown social pattern cannot be combined {x["id"]}')
    check('unknown' not in x['body_shape_tags'] or len(x['body_shape_tags']) == 1, f'unknown body shape tags cannot be combined {x["id"]}')
loc_keys = {(x['species_id'], x['locale']) for x in localizations}
for x in localizations:
    check(x['species_id'] in ids and x['locale'] in {'en','zh-CN'}, str(x))
    check(all(x.get(k) for k in ('common_name','slug','title','summary','body','seo_title','seo_description')), str(x))
    labels = x.get('filter_labels')
    check(isinstance(labels, dict) and set(labels) == FILTER_LABEL_KEYS, f'missing filter labels {x["species_id"]}:{x["locale"]}')
    check(isinstance(labels['reproduction'], str) and labels['reproduction'].strip(), str(x))
    for field in ('categories', 'colors', 'social', 'body_shape'):
        unique_strings(labels[field], set(labels[field]), f'invalid filter labels {field} {x["species_id"]}:{x["locale"]}')
for sid in ids:
    check((sid,'en') in loc_keys and (sid,'zh-CN') in loc_keys, f'missing localization {sid}')
profiles_by_id = {x['species_id']: x for x in profiles}
check(set(profiles_by_id) == ids and len(profiles_by_id) == len(profiles), 'each species needs one care profile')
for x in profiles:
    check(x['water_layer'] in WATER_LAYERS, x['species_id'])
    unique_strings(x.get('water_layers'), WATER_LAYERS, f'invalid water layers {x["species_id"]}')
    check(x['water_layer'] in x['water_layers'], f'primary water layer missing {x["species_id"]}')
    husbandry = x.get('husbandry')
    check(isinstance(husbandry, dict) and HUSBANDRY_KEYS <= husbandry.keys(), f'incomplete husbandry {x["species_id"]}')
    check(husbandry['oxygen_need'] in {'standard', 'high', 'unknown'}, f'invalid oxygen need {x["species_id"]}')
    check(husbandry['group_mode'] in {'shoal', 'single', 'conditional_pair', 'loose_group', 'colony', 'unknown'}, f'invalid group mode {x["species_id"]}')
    check(husbandry['light_preference'] in {'subdued', 'moderate', 'adaptable', 'unknown'}, f'invalid light preference {x["species_id"]}')
    length = husbandry['minimum_tank_length_cm']
    check(length is None or type(length) in (int, float) and length > 0, f'invalid tank length {x["species_id"]}')
    for field, allowed in HUSBANDRY_TAGS.items():
        values = husbandry[field]
        check(isinstance(values, list) and len(values) == len(set(values)) and all(isinstance(value, str) and value in allowed for value in values), f'invalid {field} {x["species_id"]}')
    notes_by_locale = husbandry['notes']
    check(isinstance(notes_by_locale, dict) and {'en', 'zh-CN'} <= notes_by_locale.keys(), f'missing husbandry notes {x["species_id"]}')
    for locale in ('en', 'zh-CN'):
        translated = notes_by_locale[locale]
        check(isinstance(translated, dict) and NOTE_KEYS <= translated.keys() and all(isinstance(translated[key], str) for key in NOTE_KEYS), f'incomplete {locale} notes {x["species_id"]}')
    for key in NOTE_KEYS:
        check(bool(notes_by_locale['en'][key].strip()) == bool(notes_by_locale['zh-CN'][key].strip()), f'note locale mismatch {key} {x["species_id"]}')
    alerts = husbandry['alerts']
    check(isinstance(alerts, list) and len(alerts) <= 3, f'invalid alerts {x["species_id"]}')
    alert_codes = set()
    for alert in alerts:
        code = alert.get('code')
        check(isinstance(code, str) and re.fullmatch(r'[a-z][a-z0-9_]*', code) and code not in alert_codes, f'invalid alert code {x["species_id"]}')
        alert_codes.add(code)
        check(alert.get('severity') in {'caution', 'important'}, f'invalid alert severity {x["species_id"]}')
        text = alert.get('text')
        check(isinstance(text, dict) and all(isinstance(text.get(locale), str) and text[locale].strip() for locale in ('en', 'zh-CN')), f'missing alert translation {x["species_id"]}')
    check(x['difficulty'] in {'easy','medium','hard'}, x['species_id'])
    check(x['temperament'] in {'peaceful','community','territorial','aggressive'}, x['species_id'])
    for a,b in (('temperature_min_c','temperature_max_c'),('ph_min','ph_max'),('gh_min','gh_max')):
        check(isinstance(x[a], (int,float)) and isinstance(x[b], (int,float)) and x[a] <= x[b], f'{x["species_id"]} {a}')
    check(x['minimum_tank_l'] > 0 and x['school_min'] > 0, x['species_id'])
    check(x['calculator_data_status'] in {'ready','partial','unavailable'}, x['species_id'])
    check(isinstance(x.get('calculator_enabled'), bool), f'invalid calculator enabled {x["species_id"]}')
    requires_calculator_values = x['calculator_enabled'] or x['calculator_data_status'] == 'ready'
    for key, zero_allowed in (('calculator_min_tank_l', False), ('calculator_space_increment_l', False), ('calculator_bioload_index', True)):
        value = x.get(key)
        check((not requires_calculator_values and value is None) or (type(value) in (int, float) and (value >= 0 if zero_allowed else value > 0)), f'invalid calculator value {key} {x["species_id"]}')
    check(x['calculator_data_status'] != 'unavailable' or not x['calculator_enabled'], f'unavailable calculator cannot be enabled {x["species_id"]}')
check(isinstance(guides, list) and len(guides) == 2, 'expected two shared care guides')
guide_ids = [guide.get('id') for guide in guides]
check(len(set(guide_ids)) == len(guide_ids), 'duplicate care guide ID')
for guide in guides:
    check(isinstance(guide.get('id'), str) and guide['id'], 'invalid care guide ID')
    for field in ('title', 'body'):
        localized = guide.get(field)
        check(isinstance(localized, dict) and all(isinstance(localized.get(locale), str) and localized[locale].strip() for locale in ('en', 'zh-CN')), f'missing care guide {field} {guide["id"]}')
for x in species:
    profile = profiles_by_id[x['id']]
    check(isinstance(profile.get('adult_length_cm'), (int, float)) and profile['adult_length_cm'] > 0, f'invalid adult length {x["id"]}')
    check(x['size_class'] == expected_size_class(profile['adult_length_cm']), f'size class does not match adult length {x["id"]}')
    for locale in ('en', 'zh-CN'):
        labels = next(item['filter_labels'] for item in localizations if item['species_id'] == x['id'] and item['locale'] == locale)
        for field, source in (('categories', x['category_tags']), ('colors', x['primary_colors']), ('social', x['social_pattern']), ('body_shape', x['body_shape_tags'])):
            check(len(labels[field]) == len(source), f'filter label count mismatch {field} {x["id"]}:{locale}')
rule_ids = [x['id'] for x in compatibility]
check(len(rule_ids) == len(set(rule_ids)), 'duplicate compatibility ID')
for x in compatibility:
    check(x['subject_species_id'] in ids and x['object_species_id'] in ids, x['id'])
    check(x['decision'] in {'compatible','caution','avoid','unknown'}, x['id'])
    check(x['risk_level'] in {'low','medium','high'}, x['id'])
note_ids = [x['id'] for x in notes]
check(len(note_ids) == len(set(note_ids)), 'duplicate note ID')
for x in notes:
    check(x['entity_type'] in {'species','care_profile','compatibility_rule'}, x['id'])
    valid_targets = set(rule_ids) if x['entity_type'] == 'compatibility_rule' else ids
    check(x['entity_id'] in valid_targets, f'note points to missing {x["entity_type"]}: {x["id"]}')
for x in media:
    check(x['species_id'] in ids and x['image_status'] in {'formal','placeholder','none'}, x['id'])
    check(x['image_status'] != 'formal' or x.get('file_url'), x['id'])
key_fields = ['adult_length_cm','temperature','ph','gh','water_layer','diet','temperament','school_min','minimum_tank_l','difficulty']
for x in profiles:
    statuses = x.get('field_status', {})
    handled = sum(statuses.get(k) in {'verified','needs_review','insufficient_data'} for k in key_fields)
    check(handled / len(key_fields) >= .95, f'key field completeness {x["species_id"]}')
print(f'Validated {len(species)} species, {len(localizations)} localizations, {len(profiles)} care profiles, {len(compatibility)} compatibility rules.')
print('P0 locale coverage: en + zh-CN')
print('P1 calculator fields reserved; no calculator algorithm executed.')
