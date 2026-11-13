import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('..', import.meta.url));
const dataDir = path.join(root, 'data');
const read = (name) => JSON.parse(fs.readFileSync(path.join(dataDir, name), 'utf8'));
const fail = (message) => { throw new Error(message); };
const assert = (condition, message) => { if (!condition) fail(message); };

const species = read('species.json');
const localizations = read('species_localizations.json');
const careProfiles = read('care_profiles.json');
const careGuides = read('care_guides.json');
const compatibility = read('compatibility_rules.json');
const notes = read('content_notes.json');
const media = read('media_assets.json');
read('tank_profiles.json');
read('tank_plan_items.json');

const categoryValues = new Set(['tetra', 'rasbora', 'barb', 'danio', 'pleco', 'corydoras', 'gourami', 'betta', 'cichlid', 'livebearer', 'catfish', 'shrimp', 'snail', 'other']);
const colorValues = new Set(['red', 'orange', 'yellow', 'blue', 'green', 'black', 'white', 'silver', 'transparent', 'brown', 'mixed']);
const sizeValues = new Set(['small', 'medium', 'large']);
const reproductionValues = new Set(['egg_laying', 'livebearing', 'unknown']);
const socialValues = new Set(['schooling', 'pairing', 'solitary_tendency', 'colony', 'unknown']);
const bodyShapeValues = new Set(['slender', 'deep_bodied', 'laterally_compressed', 'short_round', 'elongated', 'unknown']);
const filterLabelKeys = ['categories', 'colors', 'reproduction', 'social', 'body_shape'];
const locales = ['en', 'zh-CN'];
const assertString = (value, message) => assert(typeof value === 'string' && value.trim(), message);
const assertUniqueStringArray = (value, allowed, message) => {
  assert(Array.isArray(value) && value.length > 0, message);
  assert(value.every((entry) => typeof entry === 'string' && allowed.has(entry)), message);
  assert(new Set(value).size === value.length, `${message} (duplicate values)`);
};
const assertTagArray = (value, allowed, message) => {
  assert(Array.isArray(value), message);
  assert(value.every((entry) => typeof entry === 'string' && allowed.has(entry)), message);
  assert(new Set(value).size === value.length, `${message} (duplicate values)`);
};
const expectedSizeClass = (adultLengthCm) => adultLengthCm <= 4 ? 'small' : adultLengthCm <= 8 ? 'medium' : 'large';

const speciesIds = new Set();
for (const item of species) {
  assert(item.id && /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(item.id), `invalid species id: ${item.id}`);
  assert(!speciesIds.has(item.id), `duplicate species id: ${item.id}`);
  speciesIds.add(item.id);
  assert(['fish', 'shrimp', 'snail'].includes(item.organism_type), `invalid organism type: ${item.id}`);
  assert(item.status && ['draft', 'review', 'published', 'archived'].includes(item.status), `invalid species status: ${item.id}`);
  assert(item.content_confidence && ['high', 'medium', 'low'].includes(item.content_confidence), `invalid confidence: ${item.id}`);
  assertUniqueStringArray(item.category_tags, categoryValues, `invalid category tags: ${item.id}`);
  assertUniqueStringArray(item.primary_colors, colorValues, `invalid primary colors: ${item.id}`);
  assert(sizeValues.has(item.size_class), `invalid size class: ${item.id}`);
  assert(reproductionValues.has(item.reproduction_mode), `invalid reproduction mode: ${item.id}`);
  assertUniqueStringArray(item.social_pattern, socialValues, `invalid social pattern: ${item.id}`);
  assertUniqueStringArray(item.body_shape_tags, bodyShapeValues, `invalid body shape tags: ${item.id}`);
  for (const [values, label] of [[item.social_pattern, 'social pattern'], [item.body_shape_tags, 'body shape tags']]) {
    assert(!values.includes('unknown') || values.length === 1, `unknown ${label} cannot be combined: ${item.id}`);
  }
}
assert(species.length >= 6, 'need at least 6 species fixtures');

const localizationKeys = new Set();
for (const item of localizations) {
  assert(speciesIds.has(item.species_id), `localization points to missing species: ${item.species_id}`);
  assert(['en', 'zh-CN'].includes(item.locale), `unsupported P0 locale: ${item.locale}`);
  const key = `${item.species_id}:${item.locale}`;
  assert(!localizationKeys.has(key), `duplicate localization: ${key}`);
  localizationKeys.add(key);
  for (const field of ['common_name', 'slug', 'title', 'summary', 'body', 'seo_title', 'seo_description']) {
    assert(typeof item[field] === 'string' && item[field].trim(), `missing localization ${field}: ${key}`);
  }
  assert(['missing', 'draft', 'editorial_review', 'published', 'needs_update'].includes(item.translation_status), `invalid translation status: ${key}`);
  assert(item.filter_labels && typeof item.filter_labels === 'object' && !Array.isArray(item.filter_labels), `missing filter labels: ${key}`);
  for (const field of filterLabelKeys) {
    if (field === 'reproduction') {
      assertString(item.filter_labels[field], `missing filter label ${field}: ${key}`);
    } else {
      assert(Array.isArray(item.filter_labels[field]) && item.filter_labels[field].length > 0, `missing filter label ${field}: ${key}`);
      assert(item.filter_labels[field].every((label) => typeof label === 'string' && label.trim()), `invalid filter label ${field}: ${key}`);
      assert(new Set(item.filter_labels[field]).size === item.filter_labels[field].length, `duplicate filter label ${field}: ${key}`);
    }
  }
}
for (const id of speciesIds) {
  assert(localizationKeys.has(`${id}:en`), `missing en localization: ${id}`);
  assert(localizationKeys.has(`${id}:zh-CN`), `missing zh-CN localization: ${id}`);
}

const profileBySpecies = new Map();
const zoneValues = new Set(['top', 'mid', 'bottom']);
const difficultyValues = new Set(['easy', 'medium', 'hard']);
const temperamentValues = new Set(['peaceful', 'community', 'territorial', 'aggressive']);
const husbandryKeys = ['oxygen_need', 'flow_preference', 'minimum_tank_length_cm', 'group_mode', 'light_preference', 'substrate_tags', 'habitat_tags', 'feeding_zone', 'food_form_tags', 'notes', 'alerts'];
const noteKeys = ['oxygen', 'flow', 'space', 'group', 'feeding', 'setup'];
const husbandryTags = {
  flow_preference: new Set(['minimal', 'gentle', 'moderate', 'strong']),
  substrate_tags: new Set(['smooth_sand', 'smooth_gravel', 'optional']),
  habitat_tags: new Set(['plants', 'surface_cover', 'hiding_places', 'wood', 'open_swimming', 'mature_biofilm']),
  feeding_zone: new Set(['surface', 'midwater', 'bottom']),
  food_form_tags: new Set(['small_particles', 'floating', 'sinking', 'vegetable', 'biofilm'])
};
for (const profile of careProfiles) {
  assert(speciesIds.has(profile.species_id), `care profile points to missing species: ${profile.species_id}`);
  assert(!profileBySpecies.has(profile.species_id), `duplicate active care profile: ${profile.species_id}`);
  profileBySpecies.set(profile.species_id, profile);
  assert(zoneValues.has(profile.water_layer), `invalid water layer: ${profile.species_id}`);
  assertTagArray(profile.water_layers, zoneValues, `invalid water layers: ${profile.species_id}`);
  assert(profile.water_layers.length > 0 && profile.water_layers.includes(profile.water_layer), `primary water layer missing: ${profile.species_id}`);
  const husbandry = profile.husbandry;
  assert(husbandry && typeof husbandry === 'object' && !Array.isArray(husbandry), `missing husbandry: ${profile.species_id}`);
  assert(husbandryKeys.every((key) => Object.hasOwn(husbandry, key)), `incomplete husbandry: ${profile.species_id}`);
  assert(['standard', 'high', 'unknown'].includes(husbandry.oxygen_need), `invalid oxygen need: ${profile.species_id}`);
  assert(['shoal', 'single', 'conditional_pair', 'loose_group', 'colony', 'unknown'].includes(husbandry.group_mode), `invalid group mode: ${profile.species_id}`);
  assert(['subdued', 'moderate', 'adaptable', 'unknown'].includes(husbandry.light_preference), `invalid light preference: ${profile.species_id}`);
  assert(husbandry.minimum_tank_length_cm === null || Number.isFinite(husbandry.minimum_tank_length_cm) && husbandry.minimum_tank_length_cm > 0, `invalid tank length: ${profile.species_id}`);
  for (const [field, values] of Object.entries(husbandryTags)) assertTagArray(husbandry[field], values, `invalid ${field}: ${profile.species_id}`);
  assert(husbandry.notes && typeof husbandry.notes === 'object' && locales.every((locale) => Object.hasOwn(husbandry.notes, locale)), `missing husbandry notes: ${profile.species_id}`);
  for (const locale of locales) {
    const translated = husbandry.notes[locale];
    assert(translated && noteKeys.every((key) => typeof translated[key] === 'string'), `incomplete ${locale} notes: ${profile.species_id}`);
    for (const key of noteKeys) assert(Boolean(translated[key].trim()) === Boolean(husbandry.notes[locales.find((other) => other !== locale)][key].trim()), `note locale mismatch ${key}: ${profile.species_id}`);
  }
  assert(Array.isArray(husbandry.alerts) && husbandry.alerts.length <= 3, `invalid alerts: ${profile.species_id}`);
  const alertCodes = new Set();
  for (const alert of husbandry.alerts) {
    assert(typeof alert.code === 'string' && /^[a-z][a-z0-9_]*$/.test(alert.code) && !alertCodes.has(alert.code), `invalid alert code: ${profile.species_id}`);
    alertCodes.add(alert.code);
    assert(['caution', 'important'].includes(alert.severity), `invalid alert severity: ${profile.species_id}`);
    assert(alert.text && locales.every((locale) => typeof alert.text[locale] === 'string' && alert.text[locale].trim()), `missing alert translation: ${profile.species_id}`);
  }
  assert(difficultyValues.has(profile.difficulty), `invalid difficulty: ${profile.species_id}`);
  assert(temperamentValues.has(profile.temperament), `invalid temperament: ${profile.species_id}`);
  for (const [minKey, maxKey] of [['temperature_min_c', 'temperature_max_c'], ['ph_min', 'ph_max'], ['gh_min', 'gh_max']]) {
    assert(Number.isFinite(profile[minKey]) && Number.isFinite(profile[maxKey]), `missing numeric range: ${profile.species_id} ${minKey}`);
    assert(profile[minKey] <= profile[maxKey], `invalid range: ${profile.species_id} ${minKey}`);
  }
  assert(Number.isFinite(profile.minimum_tank_l) && profile.minimum_tank_l > 0, `invalid minimum tank: ${profile.species_id}`);
  assert(Number.isInteger(profile.school_min) && profile.school_min > 0, `invalid school min: ${profile.species_id}`);
  assert(typeof profile.calculator_enabled === 'boolean', `invalid calculator enabled: ${profile.species_id}`);
  const requiresCalculatorValues = profile.calculator_enabled || profile.calculator_data_status === 'ready';
  for (const [key, zeroAllowed] of [['calculator_min_tank_l', false], ['calculator_space_increment_l', false], ['calculator_bioload_index', true]]) {
    const value = profile[key];
    assert(!requiresCalculatorValues && value === null || Number.isFinite(value) && (zeroAllowed ? value >= 0 : value > 0), `invalid calculator value ${key}: ${profile.species_id}`);
  }
  assert(profile.calculator_data_status !== 'unavailable' || profile.calculator_enabled === false, `unavailable calculator cannot be enabled: ${profile.species_id}`);
  assert(['ready', 'partial', 'unavailable'].includes(profile.calculator_data_status), `invalid calculator status: ${profile.species_id}`);
}
for (const id of speciesIds) assert(profileBySpecies.has(id), `missing care profile: ${id}`);

assert(Array.isArray(careGuides) && careGuides.length === 2, 'expected two shared care guides');
assert(new Set(careGuides.map((guide) => guide.id)).size === careGuides.length, 'duplicate care guide ID');
for (const guide of careGuides) {
  assert(typeof guide.id === 'string' && /^[a-z][a-z0-9-]*$/.test(guide.id), `invalid care guide ID: ${guide.id}`);
  for (const field of ['title', 'body']) assert(guide[field] && locales.every((locale) => typeof guide[field][locale] === 'string' && guide[field][locale].trim()), `missing care guide ${field}: ${guide.id}`);
}

for (const speciesItem of species) {
  const profile = profileBySpecies.get(speciesItem.id);
  assert(Number.isFinite(profile.adult_length_cm) && profile.adult_length_cm > 0, `invalid adult length for size class: ${speciesItem.id}`);
  assert(speciesItem.size_class === expectedSizeClass(profile.adult_length_cm), `size class does not match adult length: ${speciesItem.id}`);
  for (const locale of locales) {
    const localization = localizations.find((item) => item.species_id === speciesItem.id && item.locale === locale);
    for (const [field, source] of [['categories', speciesItem.category_tags], ['colors', speciesItem.primary_colors], ['social', speciesItem.social_pattern], ['body_shape', speciesItem.body_shape_tags]]) {
      assert(localization.filter_labels[field].length === source.length, `filter label count mismatch ${field}: ${speciesItem.id}:${locale}`);
    }
  }
}

const ruleIds = new Set();
for (const rule of compatibility) {
  assert(typeof rule.id === 'string' && !ruleIds.has(rule.id), `duplicate or invalid compatibility ID: ${rule.id}`);
  ruleIds.add(rule.id);
  assert(speciesIds.has(rule.subject_species_id) && speciesIds.has(rule.object_species_id), `compatibility points to missing species: ${rule.id}`);
  assert(['compatible', 'caution', 'avoid', 'unknown'].includes(rule.decision), `invalid decision: ${rule.id}`);
  assert(['low', 'medium', 'high'].includes(rule.risk_level), `invalid risk level: ${rule.id}`);
  assert(['draft', 'review', 'published', 'retired'].includes(rule.status), `invalid compatibility status: ${rule.id}`);
}
const noteIds = new Set();
for (const note of notes) {
  assert(typeof note.id === 'string' && !noteIds.has(note.id), `duplicate or invalid note ID: ${note.id}`);
  noteIds.add(note.id);
  assert(['species', 'care_profile', 'compatibility_rule'].includes(note.entity_type), `invalid note entity: ${note.id}`);
  const validTarget = note.entity_type === 'compatibility_rule' ? ruleIds.has(note.entity_id) : speciesIds.has(note.entity_id);
  assert(validTarget, `note points to missing ${note.entity_type}: ${note.id}`);
  assert(['editor_note', 'personal_observation', 'uncertainty', 'update_note'].includes(note.note_type), `invalid note type: ${note.id}`);
  assert(['internal', 'public'].includes(note.visibility), `invalid note visibility: ${note.id}`);
}
for (const asset of media) {
  assert(speciesIds.has(asset.species_id), `media points to missing species: ${asset.id}`);
  assert(['formal', 'placeholder', 'none'].includes(asset.image_status), `invalid image status: ${asset.id}`);
  assert(asset.image_status !== 'formal' || Boolean(asset.file_url), `formal image missing URL: ${asset.id}`);
  assert(asset.image_status !== 'placeholder' || asset.is_ai_placeholder === true, `placeholder must be marked: ${asset.id}`);
}

const keyFields = ['adult_length_cm', 'temperature', 'ph', 'gh', 'water_layer', 'diet', 'temperament', 'school_min', 'minimum_tank_l', 'difficulty'];
for (const profile of careProfiles) {
  const statuses = profile.field_status ?? {};
  const handled = keyFields.filter((key) => statuses[key] === 'verified' || statuses[key] === 'needs_review' || statuses[key] === 'insufficient_data').length;
  assert(handled / keyFields.length >= 0.95, `key field completeness below 95%: ${profile.species_id}`);
}

console.log(`Validated ${species.length} species, ${localizations.length} localizations, ${careProfiles.length} care profiles, ${compatibility.length} compatibility rules.`);
console.log('P0 locale coverage: en + zh-CN');
console.log('P1 calculator fields reserved; no calculator algorithm executed.');
