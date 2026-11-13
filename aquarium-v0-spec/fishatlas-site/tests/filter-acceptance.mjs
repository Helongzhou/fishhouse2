import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE || '/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs');

const root = fileURLToPath(new URL('..', import.meta.url));
const base = process.env.FISHATLAS_URL || 'http://127.0.0.1:4173/';
const artifacts = path.join(root, 'qa-artifacts');
fs.mkdirSync(artifacts, { recursive: true });
const results = [];
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROME_EXECUTABLE || '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome' });
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
const errors = [];
page.on('pageerror', (error) => errors.push(error.message));

async function open(query, target = page) {
  await target.goto(`${base}?${query}`, { waitUntil: 'networkidle' });
  try {
    await target.locator('.results-head, .detail-hero, .calculator-placeholder').first().waitFor({ timeout: 10000 });
  } catch (error) {
    const main = await target.locator('#main-content').innerText().catch(() => 'main unavailable');
    throw new Error(`${error.message}; url=${target.url()}; main=${main.slice(0, 300)}`);
  }
}
async function ids(target = page) {
  return (await target.locator('.results-grid .species-card').evaluateAll((cards) => cards.map((card) => new URL(card.href).searchParams.get('species')))).sort();
}
async function equalIds(expected, target = page) {
  assert.deepEqual(await ids(target), expected.sort());
}
async function test(name, fn) {
  if (process.env.FISHATLAS_TEST_MATCH && !name.includes(process.env.FISHATLAS_TEST_MATCH)) return;
  try { await fn(); results.push({ name, status: 'PASS' }); console.log(`PASS ${name}`); }
  catch (error) { results.push({ name, status: 'FAIL', detail: error.message }); console.error(`FAIL ${name}: ${error.message}`); }
}

await test('desktop baseline, honest image status, screenshot', async () => {
  await open('lang=en&view=database');
  assert.equal((await ids()).length, 10);
  assert.match(await page.locator('.species-card[href*="neon-tetra"] .card-media').getAttribute('aria-label'), /Placeholder/);
  assert.match(await page.locator('.species-card[href*="ember-tetra"] .card-media').getAttribute('aria-label'), /No photo/);
  await page.screenshot({ path: path.join(artifacts, 'database-desktop-en.png'), fullPage: true });
});
await test('OR within color and AND across dimensions', async () => {
  await open('lang=zh-CN&view=database&color=blue,orange');
  await equalIds(['neon-tetra', 'ember-tetra', 'harlequin-rasbora', 'honey-gourami', 'zebra-danio']);
  await open('lang=zh-CN&view=database&category=tetra&color=red&size=small');
  await equalIds(['neon-tetra', 'ember-tetra']);
  assert.equal(await page.locator('.active-chip').count(), 3);
  await page.screenshot({ path: path.join(artifacts, 'database-combination-zh.png'), fullPage: true });
  await open('lang=en&view=database&category=gourami&social=solitary_tendency');
  await equalIds(['honey-gourami', 'betta-splendens']);
});
await test('livebearing, large, exact numeric filters and empty state', async () => {
  await open('lang=en&view=database&reproduction=livebearing');
  await equalIds(['guppy']);
  await open('lang=en&view=database&size=large');
  await equalIds(['bristlenose-pleco']);
  await open('lang=en&view=database&tank=30');
  await equalIds(['ember-tetra', 'red-cherry-shrimp', 'betta-splendens']);
  await open('lang=en&view=database&phMin=7.8');
  await equalIds(['red-cherry-shrimp', 'guppy', 'zebra-danio']);
  await open('lang=en&view=database&tempMin=29');
  await equalIds([]);
  assert.match(await page.locator('.empty-state').innerText(), /No species match/);
});
await test('medium difficulty and unknown reproduction via isolated response fixture', async () => {
  await open('lang=en&view=database&difficulty=medium');
  await equalIds(['bristlenose-pleco']);
  const fixture = await browser.newPage();
  const species = JSON.parse(fs.readFileSync(path.join(root, 'data/species.json'), 'utf8'));
  const localizations = JSON.parse(fs.readFileSync(path.join(root, 'data/species_localizations.json'), 'utf8'));
  const care = JSON.parse(fs.readFileSync(path.join(root, 'data/care_profiles.json'), 'utf8'));
  const unknown = { ...species[0], id: 'qa-unknown-reproduction', primary_colors: [], reproduction_mode: 'unknown', social_pattern: ['unknown'], body_shape_tags: ['unknown'] };
  species.push(unknown);
  for (const locale of ['en', 'zh-CN']) {
    const source = localizations.find((item) => item.species_id === 'neon-tetra' && item.locale === locale);
    localizations.push({ ...source, species_id: unknown.id, common_name: 'QA unknown reproduction', filter_labels: { ...source.filter_labels, colors: [], reproduction: locale === 'en' ? 'Unknown' : '未知', social: [locale === 'en' ? 'Unknown' : '未知'], body_shape: [locale === 'en' ? 'Unknown' : '未知'] } });
  }
  care.push({ ...care[0], species_id: unknown.id, water_layer: 'unknown', difficulty: 'unknown' });
  const fixtureHits = new Set();
  for (const [name, body] of [['species', species], ['species_localizations', localizations], ['care_profiles', care]]) {
    await fixture.route(new RegExp(`/data/${name}\\.json(?:\\?.*)?$`), (route) => {
      fixtureHits.add(name);
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
    });
  }
  await open('lang=en&view=database', fixture);
  assert.deepEqual([...fixtureHits].sort(), ['care_profiles', 'species', 'species_localizations']);
  assert((await ids(fixture)).includes(unknown.id));
  await open('lang=en&view=database&reproduction=egg_laying', fixture);
  assert(!(await ids(fixture)).includes(unknown.id));
  await open('lang=en&view=database&color=red', fixture);
  assert(!(await ids(fixture)).includes(unknown.id));
  await open('lang=en&view=database&reproduction=unknown', fixture);
  await equalIds([unknown.id], fixture);
  await open(`lang=en&species=${unknown.id}`, fixture);
  assert.match(await fixture.locator('.stat-grid').innerText(), /Not set/);
  assert.match(await fixture.locator('.detail-hero').innerText(), /Unknown/);
  await fixture.close();
});
await test('invalid and repeated query values are deduplicated and merged', async () => {
  await open('lang=en&view=database&category=tetra,tetra,bogus&color=red,not-a-color');
  await equalIds(['neon-tetra', 'ember-tetra']);
  assert.equal(await page.locator('.active-chip').count(), 2);
  await open('lang=en&view=database&category=tetra&category=shrimp');
  await equalIds(['neon-tetra', 'ember-tetra', 'red-cherry-shrimp']);
  assert.equal(await page.locator('.active-chip').count(), 2);
});
await test('invalid numeric URL values show an error and can be cleared', async () => {
  await open('lang=en&view=database&tempMin=not-a-number&tank=-10');
  await equalIds([]);
  assert.match(await page.locator('.results-grid [role="alert"]').innerText(), /invalid|valid|range|number|tank|temperature/i);
  assert.equal(await page.locator('.active-chip').count(), 2);
  await page.locator('#clear-filters').click();
  assert.equal((await ids()).length, 10);
});
await test('preset, chip removal, language switch, history and reload', async () => {
  await open('lang=en&view=database');
  await page.locator('[data-preset="livebearer"]').click();
  await equalIds(['guppy']);
  assert.equal(new URL(page.url()).searchParams.get('reproduction'), 'livebearing');
  await page.locator('[data-locale="zh-CN"]').click();
  await equalIds(['guppy']);
  assert.equal(await page.locator('html').getAttribute('lang'), 'zh-CN');
  await page.reload({ waitUntil: 'networkidle' });
  await equalIds(['guppy']);
  await page.locator('[data-remove-field="reproduction"]').click();
  assert.equal((await ids()).length, 10);
  await page.goBack({ waitUntil: 'networkidle' });
  await equalIds(['guppy']);
  for (let i = 0; i < 3; i++) { await page.locator('[data-locale="en"]').click(); await page.locator('[data-locale="zh-CN"]').click(); }
  const before = await page.evaluate(() => history.length);
  await page.locator('[data-locale="en"]').click();
  assert.equal(await page.evaluate(() => history.length), before + 1);
  assert.equal(await page.locator('html').getAttribute('lang'), 'en');
  await page.screenshot({ path: path.join(artifacts, 'database-en-filtered.png'), fullPage: true });
});
await test('numeric range validation blocks inverted limits', async () => {
  await open('lang=en&view=database');
  await page.locator('.advanced-filters > summary').click();
  await page.locator('[name="tempMin"]').fill('30');
  await page.locator('[name="tempMax"]').fill('20');
  await page.locator('#filter-form button.apply').click();
  assert.match(await page.locator('#filter-error').innerText(), /Minimum cannot exceed maximum/);
  assert(!new URL(page.url()).searchParams.has('tempMin'));
  await page.locator('[name="tempMin"]').fill('41');
  await page.locator('#filter-form button.apply').click();
  assert(!new URL(page.url()).searchParams.has('tempMin'));
});
await test('detail and P1 calculator regression', async () => {
  await open('lang=zh-CN&species=betta-splendens');
  assert.match(await page.locator('.detail-hero').innerText(), /斗鱼/);
  assert.match(await page.locator('.stat-grid').innerText(), /7 cm/);
  assert.match(await page.locator('.care-rows').first().innerText(), /单养/);
  await open('lang=en&species=bristlenose-pleco');
  assert.match(await page.locator('.stat-grid').innerText(), /12 cm/);
  assert.match(await page.locator('.stat-grid').innerText(), /Moderate/);
  await open('lang=en&species=neon-tetra');
  assert.match(await page.locator('.detail-media').innerText(), /Placeholder/i);
  await page.screenshot({ path: path.join(artifacts, 'detail-placeholder-en.png'), fullPage: true });
  await open('lang=en&view=calculator');
  assert.match(await page.locator('.calculator-placeholder').innerText(), /Coming in P1/);
});
await test('390px collapsed and expanded mobile filters, focus and overflow', async () => {
  await page.setViewportSize({ width: 390, height: 844 });
  await open('lang=zh-CN&view=database');
  assert(!(await page.locator('.filter-shell').evaluate((el) => el.open)));
  await page.screenshot({ path: path.join(artifacts, 'database-mobile-collapsed.png'), fullPage: true });
  await page.locator('.filter-shell > summary').focus();
  await page.keyboard.press('Enter');
  assert(await page.locator('.filter-shell').evaluate((el) => el.open));
  assert.equal(await page.locator('.filter-shell > summary').evaluate((el) => el === document.activeElement), true);
  await page.locator('.advanced-filters > summary').click();
  await page.screenshot({ path: path.join(artifacts, 'database-mobile-expanded.png'), fullPage: true });
  const overflows = await page.evaluate(() => ({
    document: document.documentElement.scrollWidth > innerWidth,
    cards: [...document.querySelectorAll('.species-card')].some((el) => el.scrollWidth > el.clientWidth + 1),
    panel: document.querySelector('.filter-panel').scrollWidth > document.querySelector('.filter-panel').clientWidth + 1
  }));
  assert.deepEqual(overflows, { document: false, cards: false, panel: false });
  await page.locator('input[name="category"][value="tetra"]').check();
  await page.locator('input[name="color"][value="red"]').check();
  const rangeLabels = await page.locator('[name="tempMin"], [name="tempMax"], [name="phMin"], [name="phMax"]').evaluateAll((inputs) => inputs.map((input) => input.getAttribute('aria-label')));
  assert(rangeLabels.every((label) => label && !/\bMin\b|\bMax\b/.test(label)), `English numeric labels in Chinese UI: ${rangeLabels.join(', ')}`);
  const placeholders = await page.locator('[name="tempMin"], [name="tempMax"], [name="phMin"], [name="phMax"]').evaluateAll((inputs) => inputs.map((input) => input.placeholder));
  assert.deepEqual(placeholders, ['最低', '最高', '最低', '最高']);
  await page.locator('#filter-form button.apply').click();
  await equalIds(['neon-tetra', 'ember-tetra']);
  assert.equal(await page.locator('html').getAttribute('lang'), 'zh-CN');
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.locator('.filter-panel').waitFor({ state: 'visible', timeout: 2000 });
});
await test('390px English first viewport fits without horizontal overflow', async () => {
  await page.setViewportSize({ width: 390, height: 844 });
  await open('lang=en&view=database');
  const bounds = await page.evaluate(() => ({
    documentWidth: document.documentElement.scrollWidth,
    viewportWidth: innerWidth,
    headerItems: [...document.querySelectorAll('.site-header > *')].map((el) => ({ left: el.getBoundingClientRect().left, right: el.getBoundingClientRect().right }))
  }));
  assert(bounds.documentWidth <= bounds.viewportWidth, JSON.stringify(bounds));
  assert(bounds.headerItems.every(({ left, right }) => left >= 0 && right <= bounds.viewportWidth), JSON.stringify(bounds));
  await page.screenshot({ path: path.join(artifacts, 'database-mobile-en-first-viewport.png') });
});
await test('sort control is available', async () => {
  await open('lang=en&view=database');
  const sort = page.locator('select[name="sort"], [data-sort]').first();
  assert(await sort.count() > 0, 'No sort control found in database view');
  const care = JSON.parse(fs.readFileSync(path.join(root, 'data/care_profiles.json'), 'utf8'));
  const byId = new Map(care.map((item) => [item.species_id, item]));
  for (const [value, field] of [['length', 'adult_length_cm'], ['tank', 'minimum_tank_l']]) {
    await sort.selectOption(value);
    assert.equal(new URL(page.url()).searchParams.get('sort'), value);
    const ordered = await page.locator('.results-grid .species-card').evaluateAll((cards) => cards.map((card) => new URL(card.href).searchParams.get('species')));
    const values = ordered.map((id) => byId.get(id)[field]);
    assert.deepEqual(values, [...values].sort((a, b) => a - b));
    await page.reload({ waitUntil: 'networkidle' });
    assert.equal(new URL(page.url()).searchParams.get('sort'), value);
  }
});
await test('no browser exceptions', async () => { assert.deepEqual(errors, []); });

const filterResultPath = process.env.FISHATLAS_TEST_MATCH
  ? path.join(artifacts, 'cache-fix/filter-fixture-results.json')
  : path.join(artifacts, 'filter-acceptance-results.json');
fs.writeFileSync(filterResultPath, JSON.stringify(results, null, 2));
await browser.close();
const failed = results.filter((item) => item.status === 'FAIL').length;
console.log(`${results.length - failed}/${results.length} passed`);
process.exitCode = failed ? 1 : 0;
