import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE || '/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs');
const root = fileURLToPath(new URL('..', import.meta.url));
const base = process.env.FISHATLAS_URL || 'http://127.0.0.1:4173/';
const artifacts = path.join(root, 'qa-artifacts/aquatic');
fs.mkdirSync(artifacts, { recursive: true });
const browser = await chromium.launch({ headless: true, channel: process.env.CHROME_CHANNEL || 'chrome', ...(process.env.CHROME_EXECUTABLE ? { executablePath: process.env.CHROME_EXECUTABLE } : {}) });
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
const results = [];
const exceptions = [];

function watch(target, name) {
  target.on('pageerror', (error) => exceptions.push(`${name}: ${error.message}`));
}
watch(page, 'main');
async function open(query, target = page) {
  await target.goto(new URL(`?${query}`, base).href, { waitUntil: 'networkidle' });
  await target.locator('.results-head, .detail-hero, .calculator-placeholder').first().waitFor();
}
async function cardIds(target = page) {
  return target.locator('.results-grid .species-card').evaluateAll((cards) => cards.map((card) => new URL(card.href).searchParams.get('species')));
}
async function expectIds(expected, target = page) {
  assert.deepEqual((await cardIds(target)).sort(), [...expected].sort());
}
async function test(name, fn) {
  if (process.env.FISHATLAS_TEST_MATCH && !name.includes(process.env.FISHATLAS_TEST_MATCH)) return;
  try {
    await fn();
    results.push({ name, status: 'PASS' });
    console.log(`PASS ${name}`);
  } catch (error) {
    results.push({ name, status: 'FAIL', detail: error.stack || error.message });
    console.error(`FAIL ${name}: ${error.message}`);
  }
}
function query(target = page) { return new URL(target.url()).searchParams; }
function sameListUrl(left, right) {
  const a = new URL(left), b = new URL(right);
  const keys = new Set([...a.searchParams.keys(), ...b.searchParams.keys()]);
  return a.origin === b.origin && a.pathname === b.pathname && [...keys].every((key) =>
    JSON.stringify(a.searchParams.getAll(key).sort()) === JSON.stringify(b.searchParams.getAll(key).sort()));
}
function fixtureData(name) { return JSON.parse(fs.readFileSync(path.join(root, `data/${name}.json`), 'utf8')); }

await test('English and Chinese type tabs, empty snail state and category cleanup', async () => {
  await open('lang=en&view=database');
  assert.match(await page.locator('.results-head h1').innerText(), /Aquatic life/i);
  assert.equal((await cardIds()).length, 10);
  await open('lang=zh-CN&view=database');
  await page.screenshot({ path: path.join(artifacts, 'database-1440-zh-CN.png'), fullPage: true });
  await open('lang=zh-CN&view=database&type=fish&category=tetra&color=red&sort=length');
  assert.match(await page.locator('.results-head h1').innerText(), /水族库/);
  await expectIds(['neon-tetra', 'ember-tetra']);
  const shrimpTab = page.locator('[data-type="shrimp"]');
  assert.equal(await shrimpTab.count(), 1, 'Shrimp type control is missing');
  await shrimpTab.click();
  await expectIds(['red-cherry-shrimp']);
  assert.equal(query().get('type'), 'shrimp');
  assert.equal(query().has('category'), false, 'Switching type must remove inapplicable tetra');
  assert.equal(query().get('color'), 'red');
  assert.equal(query().get('sort'), 'length');
  await page.locator('[data-type=""]').click();
  assert.equal(query().has('type'), false);
  assert.equal(query().get('color'), 'red', 'All type must preserve other filters');
  assert.equal(query().get('sort'), 'length');
  await open('lang=en&view=database&type=snail');
  await expectIds([]);
  assert.match(await page.locator('.empty-state').innerText(), /No entries in this group yet/i);
  assert.equal(query().get('type'), 'snail', 'Valid empty type should remain in URL');
  await page.locator('#clear-filters').click();
  assert.equal(query().has('type'), false);
  assert.equal((await cardIds()).length, 10);
});

await test('fixed category, color, layer and sorting semantics', async () => {
  await open('lang=en&view=database&type=fish&category=tetra&color=red');
  await expectIds(['neon-tetra', 'ember-tetra']);
  assert.equal(await page.locator('.active-chip').count(), 2);
  assert.equal(await page.locator('[data-type="fish"]').getAttribute('aria-pressed'), 'true');
  await open('lang=en&view=database&type=shrimp&category=shrimp');
  await expectIds(['red-cherry-shrimp']);
  await open('lang=en&view=database&category=gourami');
  await expectIds(['honey-gourami', 'betta-splendens']);
  await open('lang=en&view=database&category=betta');
  await expectIds(['betta-splendens']);
  await open('lang=en&view=database&layer=bottom');
  await expectIds(['panda-corydoras', 'red-cherry-shrimp', 'bristlenose-pleco']);
  await open('lang=en&view=database&layer=top');
  await expectIds(['honey-gourami', 'guppy', 'betta-splendens', 'zebra-danio']);
  await open('lang=en&view=database&sort=length');
  const care = new Map(fixtureData('care_profiles').map((item) => [item.species_id, item.adult_length_cm]));
  const lengths = (await cardIds()).map((id) => care.get(id));
  assert.deepEqual(lengths, [...lengths].sort((a, b) => a - b));
  await page.reload({ waitUntil: 'networkidle' });
  assert.equal(query().get('sort'), 'length');
});

await test('oxygen and flow are independent, unknown values never pass explicit filters', async () => {
  const fixture = await browser.newPage();
  watch(fixture, 'fixture');
  const species = fixtureData('species');
  const localizations = fixtureData('species_localizations');
  const care = fixtureData('care_profiles');
  const neon = care.find((item) => item.species_id === 'neon-tetra');
  const ember = care.find((item) => item.species_id === 'ember-tetra');
  neon.husbandry = { ...neon.husbandry, oxygen_need: 'high', flow_preference: ['gentle'] };
  ember.husbandry = { ...ember.husbandry, oxygen_need: 'standard', flow_preference: ['strong'] };
  const unknown = { ...species.find((item) => item.id === 'neon-tetra'), id: 'qa-unknown', primary_colors: [], reproduction_mode: 'unknown', body_shape_tags: [], social_pattern: [] };
  species.push(unknown);
  for (const locale of ['en', 'zh-CN']) {
    const source = localizations.find((item) => item.species_id === 'neon-tetra' && item.locale === locale);
    localizations.push({ ...source, species_id: unknown.id, common_name: 'QA unknown' });
  }
  care.push({ ...neon, species_id: unknown.id, water_layers: [], water_layer: 'unknown', difficulty: 'unknown', husbandry: { ...neon.husbandry, oxygen_need: 'unknown', flow_preference: [] } });
  const fixtureHits = new Set();
  for (const [name, body] of [['species', species], ['species_localizations', localizations], ['care_profiles', care]]) {
    await fixture.route(new RegExp(`/data/${name}\\.json(?:\\?.*)?$`), (route) => {
      fixtureHits.add(name);
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
    });
  }
  try {
    await open('lang=en&view=database&type=fish&category=tetra&oxygen=high', fixture);
    assert.deepEqual([...fixtureHits].sort(), ['care_profiles', 'species', 'species_localizations']);
    await expectIds(['neon-tetra'], fixture);
    await open('lang=en&view=database&type=fish&category=tetra&flow=strong', fixture);
    await expectIds(['ember-tetra'], fixture);
    await open('lang=en&view=database&type=fish&category=tetra&oxygen=high&flow=strong', fixture);
    await expectIds([], fixture);
    await open('lang=en&view=database&type=fish&category=tetra', fixture);
    assert((await cardIds(fixture)).includes('qa-unknown'));
    for (const condition of ['oxygen=standard', 'flow=gentle', 'layer=mid', 'color=red', 'reproduction=egg_laying']) {
      await open(`lang=en&view=database&type=fish&category=tetra&${condition}`, fixture);
      assert(!(await cardIds(fixture)).includes('qa-unknown'), `Unknown passed ${condition}`);
    }
  } finally { await fixture.close(); }
});

await test('cards expose precise essentials without invented media', async () => {
  await open('lang=en&view=database');
  const neon = page.locator('.species-card[href*="neon-tetra"]');
  const text = await neon.innerText();
  for (const expected of ['Paracheirodon innesi', '4 cm', '45 L', '22', '26', '8']) assert(text.includes(expected), `Neon card missing ${expected}`);
  assert(!/>8 cm|Large/.test(text), 'Card shows broad size class instead of precise length');
  const media = await page.locator('.species-card .card-media').evaluateAll((nodes) => nodes.map((el) => ({
    text: el.textContent.trim(),
    images: [...el.querySelectorAll('img')].map((img) => img.getAttribute('src')),
    glyphs: el.querySelectorAll('.fish-glyph, .fake-fish, .fish-illustration').length,
  })));
  assert.equal(media.length, 10);
  assert(media.every(({ text, images, glyphs }) => images.every(Boolean) && (images.length || /no photo|暂无照片|placeholder|占位/i.test(text)) && glyphs === 0), 'Media state is misleading or contains a pseudo image');
  await page.screenshot({ path: path.join(artifacts, 'database-1440-en.png'), fullPage: true });
  await open('lang=en&view=database&color=red&reproduction=livebearing');
  await expectIds([]);
  await open('lang=en&view=database&reproduction=livebearing');
  await expectIds(['guppy']);
  assert.match(await page.locator('.species-card').innerText(), /livebear/i);
  await open('lang=en&view=database');
  assert.match(await neon.locator('.card-fact-grid').innerText(), /Group/i);
  assert.match(await page.locator('.species-card[href*="betta-splendens"] .card-heading .tag').innerText(), /Betta/i);
  assert.match(await page.locator('.species-card[href*="honey-gourami"] .card-heading .tag').innerText(), /Labyrinth fish/i);
  await open('lang=zh-CN&view=database');
  assert.match(await page.locator('.species-card[href*="betta-splendens"] .card-heading .tag').innerText(), /斗鱼/);
});

await test('group modes, care notes, alerts and shared guides', async () => {
  const cases = [
    ['neon-tetra', /at least\s*8/i, /10.*recommend|recommend.*10/i],
    ['honey-gourami', /single.*pair|pair.*single/i, /pair|territor/i],
    ['betta-splendens', /single|singly/i, /territor|fin/i],
    ['red-cherry-shrimp', /10|suggested/i, /shrimp|biofilm/i],
  ];
  for (const [id, group, note] of cases) {
    await open(`lang=en&species=${id}`);
    const body = await page.locator('.detail-hero, .detail-section').allInnerTexts();
    const joined = body.join('\n');
    assert.match(joined, group, `${id} group requirement`);
    assert.match(joined, note, `${id} care or alert`);
    assert(!/calculator data status|content status|ready for calculator/i.test(joined), `${id} leaks calculator status`);
  }
  await open('lang=en&species=neon-tetra');
  const detail = await page.locator('main').innerText();
  assert.equal((detail.match(/4 cm/g) || []).length, 1, 'Adult length appears more than once');
  assert.match(await page.locator('.identity-tags').innerText(), /egg.laying/i);
  assert.match(await page.locator('main').innerText(), /omnivore/i);
  assert.equal(await page.locator('.recognition').count(), 1, 'Color and body recognition details are missing');
  await page.locator('.recognition summary').click();
  assert.match(await page.locator('.recognition').innerText(), /blue|red/i);
  assert(!/quarantine.{0,24}7 days|7.day quarantine|\b\d+\s*W\s*\/\s*L\b/i.test(detail));
  const guides = page.locator('.guide-list details');
  assert.equal(await guides.count(), 2, 'Shared health/maintenance guides are missing');
  await guides.first().locator('summary').focus();
  await page.keyboard.press('Enter');
  assert(await guides.first().evaluate((el) => el.open), 'Guide cannot be opened by keyboard');
  await page.screenshot({ path: path.join(artifacts, 'detail-neon-en.png'), fullPage: true });
  await open('lang=en&species=honey-gourami');
  assert.match(await page.locator('.field-notes').innerText(), /hardness.*confirmation/i);
  await open('lang=zh-CN&species=honey-gourami');
  assert.match(await page.locator('.field-notes').innerText(), /硬度范围仍待确认/);
  await open('lang=zh-CN&species=betta-splendens');
  assert.match(await page.locator('.alert-list').innerText(), /缸盖/);
  await open('lang=zh-CN&species=red-cherry-shrimp');
  assert.match(await page.locator('.alert-list').innerText(), /进水口/);
  await open('lang=en&species=ember-tetra');
  assert.match(await page.locator('.compat-grid').innerText(), /Neon Tetra/i);
  await open('lang=en&species=harlequin-rasbora');
  assert.equal(await page.getByRole('heading', { name: 'Mixing & cautions' }).count(), 0, 'Harlequin has an empty mixing section');
});

await test('responsive columns, text bounds and mobile filter keyboard', async () => {
  for (const [width, expectedColumns] of [[1440, 3], [1280, 3], [1024, 2], [390, 1]]) {
    await page.setViewportSize({ width, height: 844 });
    for (const lang of width === 390 ? ['en', 'zh-CN'] : ['en']) {
      await open(`lang=${lang}&view=database`);
      const geometry = await page.evaluate(() => {
        const grid = document.querySelector('.results-grid');
        const cards = [...grid.querySelectorAll('.species-card')];
        return {
          columns: getComputedStyle(grid).gridTemplateColumns.split(' ').length,
          pageOverflow: document.documentElement.scrollWidth > innerWidth + 1,
          cardsOverflow: cards.some((card) => card.scrollWidth > card.clientWidth + 1),
          namesOverflow: cards.some((card) => [...card.querySelectorAll('h2,h3,.card-title')].some((name) => name.scrollWidth > name.clientWidth + 1)),
        };
      });
      assert.equal(geometry.columns, expectedColumns, `${width}px ${lang}: ${JSON.stringify(geometry)}`);
      assert.equal(geometry.pageOverflow, false, `${width}px ${lang} page overflow`);
      assert.equal(geometry.cardsOverflow, false, `${width}px ${lang} card overflow`);
      assert.equal(geometry.namesOverflow, false, `${width}px ${lang} name overflow`);
      await page.screenshot({ path: path.join(artifacts, `database-${width}-${lang}.png`), fullPage: true });
      if (width === 390) {
        const shell = page.locator('.filter-shell');
        assert.equal(await shell.evaluate((el) => el.open), false);
        await shell.locator('summary').first().focus();
        await page.keyboard.press('Enter');
        assert(await shell.evaluate((el) => el.open));
        await page.screenshot({ path: path.join(artifacts, `database-390-${lang}-expanded.png`), fullPage: true });
      }
    }
  }
  await page.setViewportSize({ width: 1440, height: 900 });
});

await test('long English and Chinese names remain inside mobile cards', async () => {
  const fixture = await browser.newPage({ viewport: { width: 390, height: 844 } });
  watch(fixture, 'long-name fixture');
  const localizations = fixtureData('species_localizations');
  for (const item of localizations.filter((entry) => entry.species_id === 'neon-tetra')) {
    item.common_name = item.locale === 'en' ? 'Verylongcontinuousfreshwateraquariumorganismname' : '特别特别特别特别特别特别特别长的淡水水族生物名称';
  }
  let localizationRequests = 0;
  await fixture.route(/\/data\/species_localizations\.json(?:\?.*)?$/, (route) => {
    localizationRequests += 1;
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(localizations) });
  });
  try {
    for (const lang of ['en', 'zh-CN']) {
      await open(`lang=${lang}&view=database`, fixture);
      assert(localizationRequests > 0, 'Long-name localization fixture was not loaded');
      const card = fixture.locator('.species-card[href*="neon-tetra"]');
      const overflow = await card.evaluate((el) => ({
        card: el.scrollWidth > el.clientWidth + 1,
        name: [...el.querySelectorAll('h2,h3,.card-title')].some((node) => node.scrollWidth > node.clientWidth + 1),
        page: document.documentElement.scrollWidth > innerWidth + 1,
      }));
      assert.deepEqual(overflow, { card: false, name: false, page: false }, `${lang} long name`);
    }
  } finally { await fixture.close(); }
});

await test('fresh mobile context renders each detail once through full-page scrolling', async () => {
  const mobile = await browser.newPage({ viewport: { width: 390, height: 844 } });
  watch(mobile, 'mobile detail');
  try {
    for (const [lang, id] of [['en', 'panda-corydoras'], ['zh-CN', 'betta-splendens']]) {
      await open(`lang=${lang}&species=${id}`, mobile);
      const metrics = await mobile.evaluate(() => ({
        width: document.documentElement.scrollWidth,
        height: document.documentElement.scrollHeight,
        viewHeight: innerHeight,
        main: document.querySelectorAll('main').length,
        hero: document.querySelectorAll('.detail-hero').length,
        title: document.querySelectorAll('.detail-hero h1').length,
      }));
      assert.equal(metrics.width, 390, `${lang} detail horizontal overflow`);
      assert.deepEqual([metrics.main, metrics.hero, metrics.title], [1, 1, 1], `${lang} detail duplicated in DOM`);
      assert(metrics.height > metrics.viewHeight * 2, `${lang} detail unexpectedly short`);
      await mobile.screenshot({ path: path.join(artifacts, `detail-390-${lang}-${id}-full.png`), fullPage: true });
      await mobile.evaluate(() => scrollTo(0, Math.round(document.documentElement.scrollHeight / 2)));
      await mobile.screenshot({ path: path.join(artifacts, `detail-390-${lang}-${id}-middle.png`) });
      console.log(`MOBILE ${lang} ${id}: ${JSON.stringify(metrics)}`);
    }
  } finally { await mobile.close(); }
});

await test('list context survives detail, in-page return, reload and browser history', async () => {
  await open('lang=en&view=database&type=fish&difficulty=easy&sort=tank');
  const before = page.url();
  const target = page.locator('.species-card[href*="zebra-danio"]');
  await target.scrollIntoViewIfNeeded();
  const scrollBefore = await page.evaluate(() => scrollY);
  assert(scrollBefore > 50, `List did not offer meaningful scroll: ${scrollBefore}`);
  await target.click();
  assert.equal(query().get('species'), 'zebra-danio');
  await page.reload({ waitUntil: 'networkidle' });
  await page.locator('.back-link, .breadcrumb a').last().click();
  assert(sameListUrl(page.url(), before), 'In-page return changed list query');
  await expectIds(['neon-tetra', 'ember-tetra', 'harlequin-rasbora', 'honey-gourami', 'panda-corydoras', 'guppy', 'betta-splendens', 'zebra-danio']);
  assert.equal(query().get('sort'), 'tank');
  await page.waitForFunction((expected) => Math.abs(scrollY - expected) < 40, scrollBefore, { timeout: 2000 });
  assert(Math.abs((await page.evaluate(() => scrollY)) - scrollBefore) < 40, 'In-page return lost list scroll');
  await page.goBack({ waitUntil: 'networkidle' });
  assert.equal(query().get('species'), 'zebra-danio');
  await page.goBack({ waitUntil: 'networkidle' });
  assert(sameListUrl(page.url(), before), 'Browser back changed list query');
  await page.waitForFunction((expected) => Math.abs(scrollY - expected) < 40, scrollBefore, { timeout: 2000 });
  assert(Math.abs((await page.evaluate(() => scrollY)) - scrollBefore) < 40, 'Browser back lost list scroll');
  await open('lang=en&species=neon-tetra&return=https%3A%2F%2Fexample.org%2Fescape');
  await page.locator('.back-link, .breadcrumb a').last().click();
  assert.equal(new URL(page.url()).origin, new URL(base).origin, 'External return URL was followed');
});

await test('P1 calculator remains a planning placeholder and browser has no exceptions', async () => {
  await open('lang=en&view=calculator');
  assert.match(await page.locator('.calculator-placeholder').innerText(), /P1|coming soon/i);
  assert.deepEqual(exceptions, []);
});

const resultPath = process.env.FISHATLAS_TEST_MATCH
  ? path.join(root, 'qa-artifacts/cache-fix', `aquatic-${process.env.FISHATLAS_TEST_MATCH.replace(/[^a-z0-9]+/gi, '-').toLowerCase()}-results.json`)
  : path.join(artifacts, 'aquatic-acceptance-results.json');
fs.writeFileSync(resultPath, JSON.stringify({ base, results, exceptions }, null, 2));
await browser.close();
const failed = results.filter((result) => result.status === 'FAIL').length;
console.log(`${results.length - failed}/${results.length} passed`);
process.exitCode = failed ? 1 : 0;
