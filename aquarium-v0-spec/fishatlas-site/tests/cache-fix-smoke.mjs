import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE || '/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs');
const root = fileURLToPath(new URL('..', import.meta.url));
const artifacts = path.join(root, 'qa-artifacts/cache-fix');
const base = process.env.FISHATLAS_URL || 'http://127.0.0.1:4173/';
const version = '20261106-aquatic-2';
fs.mkdirSync(artifacts, { recursive: true });

const browser = await chromium.launch({ headless: true, channel: process.env.CHROME_CHANNEL || 'chrome', ...(process.env.CHROME_EXECUTABLE ? { executablePath: process.env.CHROME_EXECUTABLE } : {}) });
const errors = [];
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  page.on('pageerror', (error) => errors.push(error.message));
  const jsonResponses = [];
  page.on('response', (response) => {
    if (new URL(response.url()).pathname.startsWith('/data/')) jsonResponses.push(response);
  });
  await page.goto(new URL('?lang=zh-CN&species=ember-tetra', base).href, { waitUntil: 'networkidle' });
  await page.locator('.detail-hero').waitFor();
  const resources = await page.evaluate(() => ({
    css: document.querySelector('link[rel="stylesheet"]')?.href,
    js: document.querySelector('script[type="module"]')?.src,
    heroBackground: getComputedStyle(document.querySelector('.detail-copy')).backgroundColor,
    careDisplay: getComputedStyle(document.querySelector('.care-row')).display,
  }));
  assert.equal(new URL(resources.css).searchParams.get('v'), version);
  assert.equal(new URL(resources.js).searchParams.get('v'), version);
  assert.equal(resources.heroBackground, 'rgba(0, 0, 0, 0)', 'Old dark detail hero CSS is still active');
  assert.equal(resources.careDisplay, 'grid', 'Old care row CSS is still active');
  assert.match(await page.locator('.care-row').nth(0).innerText(), /溶氧\s*常规/);
  assert.match(await page.locator('.care-row').nth(1).innerText(), /水流\s*缓流/);
  assert.equal(jsonResponses.length, 7, `Expected seven JSON responses, got ${jsonResponses.length}`);
  for (const response of jsonResponses) {
    assert.equal(response.status(), 200, response.url());
    assert.equal(new URL(response.url()).searchParams.get('v'), version, response.url());
    assert.match(await response.headerValue('cache-control'), /no-store/);
  }
  const detailJsonCount = jsonResponses.length;
  await page.screenshot({ path: path.join(artifacts, 'ember-detail-1440-zh-CN.png'), fullPage: true });
  await page.goto(new URL('?lang=zh-CN&view=database', base).href, { waitUntil: 'networkidle' });
  const columns = await page.locator('.results-grid').evaluate((el) => getComputedStyle(el).gridTemplateColumns.split(' ').length);
  assert.equal(columns, 3);
  await page.screenshot({ path: path.join(artifacts, 'database-1440-zh-CN.png'), fullPage: true });

  const mobile = await browser.newPage({ viewport: { width: 390, height: 844 } });
  mobile.on('pageerror', (error) => errors.push(error.message));
  await mobile.goto(new URL('?lang=zh-CN&species=ember-tetra', base).href, { waitUntil: 'networkidle' });
  await mobile.locator('.detail-hero').waitFor();
  const widths = await mobile.evaluate(() => ({ viewport: innerWidth, document: document.documentElement.scrollWidth }));
  assert.deepEqual(widths, { viewport: 390, document: 390 });
  await mobile.screenshot({ path: path.join(artifacts, 'ember-detail-390-zh-CN.png'), fullPage: true });
  assert.deepEqual(errors, []);
  const result = { status: 'PASS', version, resources, detailJsonCount, columns, widths, pageErrors: errors };
  fs.writeFileSync(path.join(artifacts, 'cache-fix-smoke-results.json'), JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result));
} finally {
  await browser.close();
}
