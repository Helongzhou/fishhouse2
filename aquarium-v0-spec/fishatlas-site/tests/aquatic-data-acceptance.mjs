import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('..', import.meta.url));
const read = (name) => JSON.parse(fs.readFileSync(path.join(root, `data/${name}.json`), 'utf8'));
const species = read('species');
const profiles = read('care_profiles');
const localizations = read('species_localizations');
const guides = read('care_guides');
const media = read('media_assets');
const byId = new Map(species.map((item) => [item.id, item]));
const care = new Map(profiles.map((item) => [item.species_id, item]));

assert.equal(species.length, 10);
assert.equal(profiles.length, 10);
assert.deepEqual(species.filter((item) => item.organism_type === 'shrimp').map((item) => item.id), ['red-cherry-shrimp']);
assert.deepEqual(species.filter((item) => item.organism_type === 'snail'), []);
assert.deepEqual(byId.get('betta-splendens').category_tags, ['betta', 'gourami']);
assert.deepEqual(byId.get('honey-gourami').category_tags, ['gourami']);
assert(species.every((item) => !item.body_shape_tags.includes('bottom_dweller')));
for (const locale of ['en', 'zh-CN']) {
  const betta = localizations.find((item) => item.species_id === 'betta-splendens' && item.locale === locale);
  assert(betta, `Missing betta ${locale}`);
  assert.equal(betta.filter_labels.categories.length, 2);
}

const expectedGroups = {
  'neon-tetra': ['shoal', 8, 10],
  'honey-gourami': ['conditional_pair', 1, 1],
  'betta-splendens': ['single', 1, 1],
  'red-cherry-shrimp': ['colony', 6, 10],
  'guppy': ['loose_group', 3, 3],
};
for (const [id, [mode, min, recommended]] of Object.entries(expectedGroups)) {
  const profile = care.get(id);
  assert.deepEqual([profile.husbandry.group_mode, profile.school_min, profile.school_recommended], [mode, min, recommended], id);
}
assert.equal(care.get('zebra-danio').husbandry.minimum_tank_length_cm, 60);
assert.equal(care.get('panda-corydoras').husbandry.oxygen_need, 'high');
assert(care.get('panda-corydoras').husbandry.flow_preference.includes('gentle'), 'High oxygen does not require strong current');
assert(care.get('betta-splendens').husbandry.flow_preference.includes('minimal'));
assert.equal(care.get('betta-splendens').husbandry.oxygen_need, 'standard', 'Minimal current does not imply low oxygen');

for (const profile of profiles) {
  assert(profile.water_layers.includes(profile.water_layer), `${profile.species_id}: primary layer missing`);
  const husbandry = profile.husbandry;
  assert(husbandry.notes.en && husbandry.notes['zh-CN'], `${profile.species_id}: bilingual notes`);
  for (const key of ['oxygen', 'flow', 'space', 'group', 'feeding', 'setup']) {
    assert.equal(typeof husbandry.notes.en[key], 'string');
    assert.equal(typeof husbandry.notes['zh-CN'][key], 'string');
  }
  assert(husbandry.alerts.length <= 3, `${profile.species_id}: too many alerts`);
  for (const alert of husbandry.alerts) {
    assert(alert.text.en?.trim() && alert.text['zh-CN']?.trim(), `${profile.species_id}: untranslated alert`);
  }
}

assert.deepEqual(guides.map((guide) => guide.id), ['new-fish-observation', 'water-quality-maintenance']);
for (const guide of guides) {
  for (const locale of ['en', 'zh-CN']) assert(guide.title[locale]?.trim() && guide.body[locale]?.trim(), `${guide.id}: ${locale}`);
}
assert.match(guides[0].body.en, /short observation period does not guarantee/i);
assert.match(guides[1].body.en, /standing water alone is not a reliable chloramine treatment/i);
assert(!/\b7\s*days?\b|\b\d+\s*%\s*(?:water|weekly)|\b\d+\s*W\s*\/\s*L\b/i.test(JSON.stringify(guides)), 'Unsupported universal numeric care advice');

const disabled = ['guppy', 'betta-splendens', 'zebra-danio', 'bristlenose-pleco'];
assert.deepEqual(profiles.filter((item) => !item.calculator_enabled).map((item) => item.species_id), disabled);
for (const id of disabled) {
  const profile = care.get(id);
  assert.equal(profile.calculator_data_status, 'unavailable');
  for (const [key, value] of Object.entries(profile)) {
    if (!key.startsWith('calculator_') || ['calculator_enabled', 'calculator_data_status'].includes(key)) continue;
    assert.deepEqual(value, key === 'calculator_zone_weights' ? {} : null, `${id}: ${key} should remain unset`);
  }
}
assert(media.every((asset) => asset.image_status !== 'formal'), 'Unexpected claimed formal photo');
assert(media.every((asset) => asset.image_status === 'none' || asset.image_status === 'placeholder'));

console.log('Aquatic data acceptance passed: 10 species, bilingual husbandry and guides, distinct flow/oxygen, calculator boundaries.');
