import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const catalog = JSON.parse(readFileSync(new URL('../../src/data/catalog.json', import.meta.url), 'utf8'));
const baseline = JSON.parse(readFileSync(new URL('../../../docs/implementation/inputs/2026-11-06-catalog-before-simplify.json', import.meta.url), 'utf8'));

test('local catalog preserves all published entities, locales, media and compatibility reminders', () => {
  assert.equal(catalog.catalog.length, 26);
  assert.deepEqual(catalog.catalog.map((entity: any) => entity.id), baseline.catalog.map((entity: any) => entity.id));
  for (const original of baseline.catalog) {
    const current = structuredClone(catalog.catalog.find((entity: any) => entity.id === original.id));
    if (original.kind === 'fish') {
      delete current.care.feeding;
      current.revision = original.revision;
    }
    if (current.discovery) {
      delete current.discovery.waterTypes;
      if (!original.discovery) delete current.discovery;
    }
    assert.deepEqual(current, original, original.id);
  }
  assert.deepEqual(
    catalog.pairRules.filter((rule: any) => baseline.pairRules.some((original: any) => original.id === rule.id))
      .map(({ visibility, ...rule }: any) => rule),
    baseline.pairRules,
  );
  assert.deepEqual(catalog.categories, baseline.categories);
  assert.deepEqual(catalog.parameterDefinitions, baseline.parameterDefinitions);
  assert.deepEqual(catalog.publishedLocales, baseline.publishedLocales);
  assert.equal(catalog.capabilities.designer, false);
  assert.deepEqual(catalog.templates, []);
  assert.notEqual(catalog.id, baseline.id);
});

test('adult feeding is fish-only, bilingual, valid in mm, and distinguishes rasping wafers', () => {
  const fish = catalog.catalog.filter((entity: any) => entity.kind === 'fish');
  assert.equal(fish.length, 9);
  for (const entity of catalog.catalog) {
    const feeding = entity.care?.feeding;
    if (entity.kind !== 'fish') {
      assert.equal(feeding, undefined, entity.id);
      continue;
    }
    assert.ok(feeding, entity.id);
    assert.equal(feeding.lifeStage, 'adult');
    assert.ok(feeding.advice['zh-CN']?.trim());
    assert.ok(feeding.advice.en?.trim());
    if (feeding.foodForm === 'wafer') {
      assert.equal(feeding.pelletDiameterMm, null);
      continue;
    }
    assert.ok(['micro-pellet', 'pellet', 'sinking-pellet'].includes(feeding.foodForm));
    const { min, max } = feeding.pelletDiameterMm;
    assert.ok(Number.isFinite(min) && Number.isFinite(max) && min > 0 && min <= max, entity.id);
  }
  const pleco = fish.find((entity: any) => entity.id === 'bristlenose-pleco');
  assert.equal(pleco.care.feeding.foodForm, 'wafer');
  assert.equal(pleco.care.feeding.pelletDiameterMm, null);
  assert.equal(fish.find((entity: any) => entity.id === 'panda-corydoras').care.feeding.foodForm, 'sinking-pellet');
});

test('biological entries declare supported water types', () => {
  const biologicalKinds = new Set(['fish', 'shrimp', 'snail', 'plant']);
  const biological = catalog.catalog.filter((entity: any) => biologicalKinds.has(entity.kind));
  assert.equal(biological.length, 18);
  for (const entity of biological) {
    assert.deepEqual(entity.discovery?.waterTypes, ['freshwater'], entity.id);
  }
  for (const entity of catalog.catalog.filter((item: any) => !biologicalKinds.has(item.kind))) {
    assert.ok(Array.isArray(entity.material?.waterTypes), entity.id);
    assert.equal(entity.discovery, undefined, entity.id);
  }
});

test('pair rules distinguish hidden risks from a small valid recommendation set', () => {
  const entities = new Set(catalog.catalog.map((entity: any) => entity.id));
  const supported = new Set(['recommended', 'caution', 'hidden']);
  for (const rule of catalog.pairRules) {
    assert.ok(supported.has(rule.visibility), rule.id);
    assert.ok(entities.has(rule.a) && entities.has(rule.b), rule.id);
  }
  for (const original of baseline.pairRules) {
    const migrated = catalog.pairRules.find((rule: any) => rule.id === original.id);
    assert.ok(migrated, original.id);
    assert.equal(migrated.visibility, 'hidden', original.id);
  }
  const recommended = catalog.pairRules.filter((rule: any) => rule.visibility === 'recommended');
  assert.deepEqual(
    recommended.map((rule: any) => rule.id).sort(),
    [
      'neon-tetra-ember-tetra-recommended',
      'neon-tetra-panda-corydoras-recommended',
      'red-cherry-shrimp-ramshorn-snail-recommended',
    ],
  );
  const counts = new Map<string, number>();
  for (const rule of recommended) {
    assert.equal(rule.enabled, true, rule.id);
    assert.equal(rule.decision, 'conditional', rule.id);
    for (const side of [rule.a, rule.b]) counts.set(side, (counts.get(side) ?? 0) + 1);
    for (const locale of ['zh-CN', 'en']) {
      assert.ok(rule.reason?.[locale]?.trim(), `${rule.id} reason ${locale}`);
      assert.ok(rule.condition?.[locale]?.trim(), `${rule.id} condition ${locale}`);
      assert.ok(rule.action?.[locale]?.trim(), `${rule.id} action ${locale}`);
    }
  }
  for (const [entityId, count] of counts) assert.ok(count <= 3, entityId);
  const panda = recommended.find((rule: any) => rule.id === 'neon-tetra-panda-corydoras-recommended');
  for (const locale of ['zh-CN', 'en']) assert.match(panda.condition[locale], /22–25°C/);
});
