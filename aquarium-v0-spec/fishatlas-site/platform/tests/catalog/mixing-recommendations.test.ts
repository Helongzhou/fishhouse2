import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { recommendedMixing } from '../../src/components/site/format';
import type { CatalogRelease, PairRule } from '../../src/domain/types';

const release = JSON.parse(readFileSync(new URL('../../src/data/catalog.json', import.meta.url), 'utf8')) as CatalogRelease;
const ids = release.catalog.filter((e) => e.kind === 'fish').map((e) => e.id);
function rule(id: string, a: string, b: string, visibility = 'recommended', enabled = true): PairRule {
  return { id, revision: 'test', a, b, visibility, enabled, decision: 'conditional',
    reason: { en: 'Suitable', 'zh-CN': '适合' }, condition: { en: 'Provide space', 'zh-CN': '保留空间' },
    action: {}, conditionKeys: [] } as PairRule;
}

test('only explicit enabled recommendations are public; caution and hidden stay private', () => {
  const fixture = { ...release, pairRules: [rule('r1', ids[0], ids[1]), rule('r2', ids[0], ids[2], 'caution'), rule('r3', ids[0], ids[3], 'hidden'), rule('r4', ids[0], ids[4], 'recommended', false)] };
  assert.deepEqual(recommendedMixing(fixture, ids[0], 'en').map(({ rule }) => rule.id), ['r1']);
  assert.deepEqual(recommendedMixing(fixture, ids[3], 'en'), []);
});

test('recommendations deduplicate partners, cap at three, and work in either rule direction', () => {
  const fixture = { ...release, pairRules: [rule('r1', ids[1], ids[0]), rule('repeat', ids[0], ids[1]), ...ids.slice(2, 7).map((other, i) => rule(`r${i + 2}`, ids[0], other))] };
  const result = recommendedMixing(fixture, ids[0], 'zh-CN');
  assert.equal(result.length, 3);
  assert.deepEqual(result.map(({ other }) => other.id), ids.slice(1, 4));
});

test('missing, inactive or untranslated partners do not produce dead recommendation links', () => {
  const fixture = { ...release, catalog: release.catalog.map((e) => e.id === ids[1] ? { ...e, active: false } : e.id === ids[2] ? { ...e, locales: { 'zh-CN': e.locales['zh-CN'] } } : e), pairRules: [rule('missing', ids[0], 'missing'), rule('inactive', ids[0], ids[1]), rule('missing-en', ids[0], ids[2])] };
  assert.deepEqual(recommendedMixing(fixture, ids[0], 'en'), []);
  assert.equal(recommendedMixing(fixture, ids[0], 'zh-CN').length, 1);
});

import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { SearchParamsContext } from 'next/dist/shared/lib/hooks-client-context.shared-runtime.js';
import { EntityDetail } from '../../src/components/site/detail';

function renderDetail(id: string, locale: 'zh-CN' | 'en', fixture = release, from = `/${locale}/aquatic?water=freshwater&type=fish`) {
  return renderToStaticMarkup(createElement(SearchParamsContext.Provider, { value: new URLSearchParams({ from }) },
    createElement(EntityDetail, { entity: fixture.catalog.find((e) => e.id === id)!, release: fixture, locale })));
}

test('detail renders a linked bilingual recommendation with original return URL and no action prose', () => {
  for (const locale of ['zh-CN', 'en'] as const) {
    const fixture = { ...release, pairRules: [rule('visible', ids[0], ids[1]), rule('hidden-warning', ids[0], ids[2], 'caution')] };
    fixture.pairRules[0].action = { 'zh-CN': '不应重复的操作', en: 'Do not repeat this action' };
    const markup = renderDetail(ids[0], locale, fixture);
    assert.ok(markup.includes(locale === 'zh-CN' ? '适合混养' : 'Suggested tankmates'));
    assert.ok(markup.includes(`/${locale}/aquatic/${ids[1]}?from=${encodeURIComponent(`/${locale}/aquatic?water=freshwater&type=fish`)}`));
    assert.ok(markup.includes(fixture.pairRules[0].reason[locale]!));
    assert.ok(markup.includes(fixture.pairRules[0].condition[locale]!));
    assert.ok(!markup.includes(fixture.pairRules[0].action[locale]!));
    assert.ok(!markup.includes(`/${locale}/aquatic/${ids[2]}?from=`));
  }
});

test('warning-only detail has no mixing section and uses single-column layout', () => {
  const fixture = { ...release, pairRules: [rule('warning-only', ids[0], ids[1], 'hidden')] };
  for (const locale of ['zh-CN', 'en'] as const) {
    const markup = renderDetail(ids[0], locale, fixture);
    assert.ok(!markup.includes('mixing-section'));
    assert.ok(!markup.includes('mixing-note'));
    assert.ok(markup.includes('detail-columns-single'));
  }
});
