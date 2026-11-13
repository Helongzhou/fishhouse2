import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import {
  availableWaterTypes,
  removeActiveFilter,
  filterEntities,
  normalizeFilters,
  publicType,
  presetFilters,
  traitValues,
} from '../../src/components/site/library-filters';

const release = JSON.parse(readFileSync(new URL('../../src/data/catalog.json', import.meta.url), 'utf8'));
const living = release.catalog.filter((e: any) => ['fish', 'shrimp', 'snail', 'plant'].includes(e.kind));

test('water values are exposed from the current aquatic entries and filter as a URL dimension', () => {
  const waters = availableWaterTypes(living);
  assert.deepEqual(waters, ['freshwater']);
  assert.deepEqual(traitValues(living[0], 'water'), ['freshwater']);
  const query = normalizeFilters('water=freshwater', living, release.categories);
  assert.equal(query.get('water'), 'freshwater');
  assert.equal(filterEntities(living, query, 'zh-CN', release.categories).length, living.length);
});

test('main type grouping covers fish, plants and both invertebrate kinds while preserving legacy URLs', () => {
  assert.equal(publicType({ kind: 'shrimp' } as any), 'invertebrate');
  assert.equal(publicType({ kind: 'snail' } as any), 'invertebrate');
  assert.equal(publicType({ kind: 'fish' } as any), 'fish');
  assert.equal(publicType({ kind: 'plant' } as any), 'plant');
  assert.equal(filterEntities(living, normalizeFilters('type=invertebrate', living, release.categories), 'en', release.categories).every((e) => ['shrimp', 'snail'].includes(e.kind)), true);
  assert.equal(filterEntities(living, normalizeFilters('type=shrimp', living, release.categories), 'en', release.categories).every((e) => e.kind === 'shrimp'), true);
  assert.equal(filterEntities(living, normalizeFilters('type=snail', living, release.categories), 'en', release.categories).every((e) => e.kind === 'snail'), true);
});

test('water options never expose empty marine or brackish entries', () => {
  assert.deepEqual(availableWaterTypes(living), ['freshwater']);
});

test('group filtering is nonempty and preserves shrimp/snail categories with water intersection', () => {
  const expected = living.filter((e: any) => e.kind === 'shrimp' || e.kind === 'snail');
  const query = normalizeFilters('type=invertebrate&water=freshwater&category=shrimp,snail', living, release.categories);
  assert.equal(query.get('category'), 'shrimp,snail');
  assert.deepEqual(filterEntities(living, query, 'en', release.categories).map((e) => e.id).sort(), expected.map((e: any) => e.id).sort());
  for (const kind of ['shrimp', 'snail']) {
    const oldQuery = normalizeFilters(`type=${kind}&water=freshwater`, living, release.categories);
    const result = filterEntities(living, oldQuery, 'zh-CN', release.categories);
    assert.ok(result.length > 0);
    assert.ok(result.every((e) => e.kind === kind));
  }
});

test('valid empty-water deep links stay empty, while invalid water values are discarded', () => {
  for (const water of ['marine', 'brackish']) {
    const query = normalizeFilters(`type=fish&water=${water}`, living, release.categories);
    assert.equal(query.get('water'), water);
    assert.deepEqual(filterEntities(living, query, 'en', release.categories), []);
    assert.equal(normalizeFilters(query.toString(), living, release.categories).toString(), query.toString());
  }
  const query = normalizeFilters('water=freshwater,invalid&water=freshwater', living, release.categories);
  assert.equal(query.get('water'), 'freshwater');
  assert.equal(query.getAll('water').length, 1);
});

test('future water values appear only when an active aquatic entry actually exists', () => {
  const fish = living.find((e: any) => e.kind === 'fish');
  const marine = { ...fish, id: 'future-marine', discovery: { ...fish.discovery, waterTypes: ['marine'] } };
  const brackish = { ...fish, id: 'future-brackish', active: false, discovery: { ...fish.discovery, waterTypes: ['brackish'] } };
  const source = [...living, marine, brackish];
  assert.deepEqual(availableWaterTypes(source), ['freshwater', 'marine']);
  const query = normalizeFilters('type=fish&water=marine', source, release.categories);
  assert.deepEqual(filterEntities(source, query, 'en', release.categories).map((e) => e.id), ['future-marine']);
  const material = release.catalog.filter((e: any) => e.material);
  assert.deepEqual(availableWaterTypes(material), []);
  assert.equal(normalizeFilters('water=freshwater', material, release.categories).has('water'), false);
});

test('shortcut filters keep water, public/legacy type and search instead of resetting the catalog context', () => {
  for (const type of ['fish', 'invertebrate', 'shrimp', 'snail']) {
    const result = presetFilters(new URLSearchParams(`water=marine&type=${type}&q=neon&sort=name&color=red`), { tank: '40' });
    assert.equal(result.get('water'), 'marine');
    assert.equal(result.get('type'), type);
    assert.equal(result.get('q'), 'neon');
    assert.equal(result.get('sort'), 'name');
    assert.equal(result.get('tank'), '40');
    assert.equal(result.has('color'), false);
    assert.deepEqual(filterEntities(living, result, 'en', release.categories), []);
  }
});

test('removing legacy single-kind chip broadens to invertebrates and preserves every other URL condition', () => {
  for (const type of ['shrimp', 'snail']) {
    const original = new URLSearchParams(`type=${type}&water=freshwater&q=red&sort=name&color=red`);
    const next = removeActiveFilter(original, 'type', type);
    assert.equal(next.get('type'), 'invertebrate');
    for (const key of ['water', 'q', 'sort', 'color']) assert.equal(next.get(key), original.get(key));
    assert.equal(original.get('type'), type);
  }
  const next = removeActiveFilter(new URLSearchParams('water=freshwater,marine&type=invertebrate'), 'water', 'marine');
  assert.equal(next.get('water'), 'freshwater');
  assert.equal(next.get('type'), 'invertebrate');
});

import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { SearchParamsContext, PathnameContext } from 'next/dist/shared/lib/hooks-client-context.shared-runtime.js';
import { AppRouterContext } from 'next/dist/shared/lib/app-router-context.shared-runtime.js';
import { Library } from '../../src/components/site/library';

function renderLibrary(type: string, locale: 'zh-CN' | 'en') {
  return renderToStaticMarkup(createElement(AppRouterContext.Provider, { value: {} as any },
    createElement(PathnameContext.Provider, { value: `/${locale}/aquatic` },
      createElement(SearchParamsContext.Provider, { value: new URLSearchParams({ type }) },
        createElement(Library, { release, locale })))));
}

test('legacy single-kind URLs show localized removable chips while aggregate pages add no subtype chip', () => {
  for (const locale of ['zh-CN', 'en'] as const) {
    for (const [type, zh, en] of [['shrimp', '虾类', 'Shrimp'], ['snail', '螺类', 'Snails']]) {
      const markup = renderLibrary(type, locale);
      assert.ok(markup.includes(`aria-label="${locale === 'zh-CN' ? '移除' : 'Remove'} ${locale === 'zh-CN' ? zh : en}"`));
      assert.ok(markup.includes('class="active-chip secondary"'));
    }
    assert.ok(!renderLibrary('invertebrate', locale).includes('class="active-chip secondary"'));
  }
});
