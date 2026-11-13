import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { chromium } from '/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs';
const out = fileURLToPath(new URL('.', import.meta.url));
const base = process.env.CATALOG_UI_BASE || 'http://127.0.0.1:4175';
const results = [], errors = [];
const browser = await chromium.launch({ headless: true, executablePath: '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome' });
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
page.on('pageerror', e => errors.push(e.message));
async function test(name, fn) { try { await fn(); results.push({ name, status: 'PASS' }); } catch (e) { results.push({ name, status: 'FAIL', detail: e.message }); } console.log(JSON.stringify(results.at(-1))); }
const noPlanner = async () => { assert.equal(await page.locator('a[href*="designer"], .designer-workspace, .template-card').count(), 0); };
const noOverflow = async () => assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
await test('Homepage bilingual search, four kinds and no planner', async () => {
  for (const locale of ['zh-CN', 'en']) {
    await page.goto(`${base}/${locale}`); await page.locator('.category-links').waitFor();
    assert.equal(await page.locator('.category-links a').count(), 4);
    assert.equal(await page.locator('.featured-grid .entity-card').count(), 6);
    assert(await page.locator('.hero-photo img').evaluate(e => e.complete && e.naturalWidth > 0));
    await noPlanner(); await noOverflow();
    await page.screenshot({ path: `${out}home-${locale}-1440.png`, fullPage: true, style: ".entity-card { content-visibility: visible !important; } nextjs-portal { display: none !important; }" });
  }
});
await test('Category link opens fish library, desktop three columns', async () => {
  await page.goto(`${base}/zh-CN`); await page.locator('.category-links a').first().click();
  await page.waitForURL('**/aquatic?type=fish'); await page.locator('.entity-card').first().waitFor();
  assert.equal(await page.locator('.entity-card').count(), 9);
  assert.equal(await page.locator('.library-layout .entity-grid').evaluate(e => getComputedStyle(e).gridTemplateColumns.split(' ').length), 3);
  assert(await page.locator('.filter-shell').evaluate(e => e.open)); await noPlanner();
});
await test('Filter URL, detail return, language change and separate care dimensions', async () => {
  const query = 'type=fish&social=schooling&body=slender';
  await page.goto(`${base}/zh-CN/aquatic?${query}`); await page.locator('.entity-card').first().waitFor();
  assert.equal(await page.locator('.type-tabs button[aria-pressed=true]').textContent(), '鱼类');
  const before = page.url();
  await page.locator('.entity-card .card-title').first().click(); await page.locator('.detail-page').waitFor();
  const back = await page.locator('.back-link').getAttribute('href'); assert.equal(new URL(back, base).href, before);
  await page.locator('.language-link').click(); await page.waitForURL('**/en/aquatic/**');
  assert((await page.locator('.back-link').getAttribute('href')).includes('/en/aquatic?'));
  await page.locator('.back-link').click(); await page.locator('.entity-card').first().waitFor();
  assert(page.url().includes('social=schooling')); await noPlanner();
});
await test('Retired designer and calculator URLs redirect to localized library', async () => {
  for (const [route, target] of [['/zh-CN/designer?template=planted-community', '/zh-CN/aquatic'], ['/en/designer?add=neon-tetra', '/en/aquatic'], ['/?view=calculator&lang=en', '/en/aquatic']]) {
    await page.goto(base + route); await page.waitForURL(base + target); await page.locator('.entity-card').first().waitFor(); await noPlanner();
  }
});
await test('Mobile 390/320 home, library, detail and material pages fit', async () => {
  for (const width of [390, 320]) {
    await page.setViewportSize({ width, height: 844 });
    for (const path of ['/zh-CN', '/en', '/zh-CN/aquatic?type=fish', '/en/aquatic/neon-tetra', '/zh-CN/materials']) {
      await page.goto(base + path); await page.locator('.site-footer').waitFor(); await noOverflow(); await noPlanner();
      if (path.includes('?type=')) {
        assert.equal(await page.locator('.filter-shell').evaluate(e => e.open), false);
        await page.locator('.filter-shell > summary').click(); assert(await page.locator('.filter-form input').first().isVisible());
      }
    }
    await page.goto(`${base}/zh-CN`); await page.screenshot({ path: `${out}home-zh-${width}.png`, fullPage: true, style: ".entity-card { content-visibility: visible !important; } nextjs-portal { display: none !important; }" });
  }
});
if (process.env.CATALOG_UI_FEEDING === '1') await test('All fish feeding in both languages, wafer exception and no fish profile on other kinds', async () => {
  const release = JSON.parse(await fs.readFile(new URL('../../src/data/catalog.json', import.meta.url), 'utf8'));
  for (const e of release.catalog.filter(e => e.kind === 'fish')) {
    for (const locale of ['zh-CN', 'en']) {
      await page.goto(`${base}/${locale}/aquatic/${e.id}`); await page.locator('.feeding-guidance').waitFor();
      const feeding = await page.locator('.feeding-guidance').textContent();
      assert(feeding.includes(locale === 'en' ? 'Adult reference' : '成鱼参考'));
      assert(feeding.includes(e.care.feeding.advice[locale]));
      if (e.care.feeding.pelletDiameterMm) assert(feeding.includes(`${e.care.feeding.pelletDiameterMm.min}–${e.care.feeding.pelletDiameterMm.max} mm`));
      else { assert(feeding.includes(locale === 'en' ? 'wafers' : '沉底片')); assert(!feeding.includes('mm')); }
      await noOverflow();
    }
  }
  for (const id of ['red-cherry-shrimp', 'anubias-nana']) {
    await page.goto(`${base}/zh-CN/aquatic/${id}`); assert.equal(await page.locator('.feeding-guidance').count(), 0);
  }
});
await test('No browser exceptions', async () => assert.deepEqual(errors, []));
await fs.writeFile(`${out}results.json`, JSON.stringify({ base, time: new Date().toISOString(), browser: 'Installed headless Google Chrome; desktop/mobile viewport emulation', results, errors }, null, 2));
await browser.close();
if (results.some(r => r.status === 'FAIL')) process.exitCode = 1;
