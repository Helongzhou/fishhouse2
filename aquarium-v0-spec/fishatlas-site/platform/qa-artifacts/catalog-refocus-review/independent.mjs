import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { chromium } from '/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs';
const base = 'http://127.0.0.1:4174';
const out = new URL('./', import.meta.url);
const catalog = JSON.parse(await fs.readFile(new URL('../../src/data/catalog.json', import.meta.url)));
const baseline = JSON.parse(await fs.readFile(new URL('../../../docs/implementation/inputs/2026-11-06-catalog-before-simplify.json', import.meta.url)));
const evidence = [], errors = [];
async function check(name, fn) { try { const details = await fn(); evidence.push({name, status:'PASS', details}); } catch(e) { evidence.push({name,status:'FAIL',detail:e.message}); } }
await check('Independent baseline comparison', async()=>{
  assert.equal(catalog.catalog.length,26);
  assert.deepEqual(catalog.catalog.map(e=>e.id),baseline.catalog.map(e=>e.id));
  const changed=[];
  for(const before of baseline.catalog){ const after=structuredClone(catalog.catalog.find(e=>e.id===before.id)); if(before.kind==='fish'){ assert.notEqual(after.revision,before.revision); delete after.care.feeding; after.revision=before.revision; changed.push(before.id); } assert.deepEqual(after,before); }
  return {entities:26,fishFeedingAndRevisionOnly:changed};
});
await check('Read APIs reject writes and retired APIs remain unavailable',async()=>{
  const reads=['/api/public/deployment',`/api/public/releases/${catalog.id}/catalog`,`/api/public/releases/${catalog.id}/manifest`,`/api/public/releases/${catalog.id}/entities/neon-tetra`];
  const retired=['/admin','/admin/login','/admin/collections/users','/api/entities','/api/users','/api/graphql','/api/graphql-playground','/api/admin/releases',`/api/admin/releases/${catalog.id}/activate`,`/api/admin/releases/${catalog.id}/rollback`,`/api/public/releases/${catalog.id}/templates`,`/api/public/releases/${catalog.id}/templates/foo`];
  const statuses=[];
  for(const path of reads) for(const method of ['POST','PUT','PATCH','DELETE']) { const r=await fetch(base+path,{method,redirect:'manual'}); assert.equal(r.status,405,`${method} ${path}`); statuses.push({method,path,status:r.status}); }
  for(const path of retired) for(const method of ['GET','POST','PUT','PATCH','DELETE']) { const r=await fetch(base+path,{method,redirect:'manual'}); assert([404,410,405].includes(r.status),`${method} ${path} ${r.status}`); statuses.push({method,path,status:r.status}); }
  return statuses;
});
const browser=await chromium.launch({headless:true, executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'});
const page=await browser.newPage({viewport:{width:1440,height:1000}});
page.on('pageerror', e=>errors.push(e.message));
await check('Bilingual homepage search, result search, empty reset and keyboard access',async()=>{
  const runs=[];
  for(const [locale,word] of [['zh-CN','霓虹灯'],['en','neon']]){
    await page.goto(`${base}/${locale}`);
    await page.keyboard.press('Tab');
    assert.equal(await page.locator(':focus').getAttribute('class'),'skip-link');
    await page.keyboard.press('Enter');
    await page.keyboard.press('Tab'); assert(await page.locator(':focus').evaluate(e=>!!e.closest('main')));
    const input=page.locator('input[type=search]'); await input.fill(word); await input.press('Enter');
    await page.waitForURL(u=>u.pathname.endsWith('/aquatic') && u.searchParams.get('q')===word);
    await page.locator('.entity-card').first().waitFor();
    assert.equal(await page.locator('.entity-card').count(),1);
    assert((await page.locator('.card-title').textContent()).includes(locale==='en'?'Neon':'霓虹'));
    await page.locator('.library-search input').fill('zzzzzzz-no-match'); await page.locator('.library-search input').press('Enter');
    await page.locator('.empty-state').waitFor();
    assert.equal(await page.locator('.entity-card').count(),0);
    await page.locator('.empty-state button').click(); await page.locator('.entity-card').first().waitFor();
    assert.equal(await page.locator('.entity-card').count(),18);
    assert.equal(await page.locator('html').getAttribute('lang'),locale);
    runs.push(locale);
  }
  return {locales:runs,resetResults:18,skipLink:'keyboard reaches main'};
});
await check('All nonfish details exclude fish diameter in both languages; controls have labels and images have alt',async()=>{
  let pages=0;
  for(const entity of catalog.catalog.filter(e=>e.kind!=='fish')) for(const locale of ['zh-CN','en']){
    await page.goto(`${base}/${locale}/aquatic/${entity.id}`); await page.locator('.detail-page').waitFor();
    assert.equal(await page.locator('.feeding-guidance').count(),0,entity.id);
    assert.equal(await page.locator('img:not([alt])').count(),0,entity.id);
    assert(!(await page.locator('body').innerText()).includes(locale==='en'?'Pellet diameter':'颗粒直径'),entity.id);
    pages++;
  }
  for(const route of ['/zh-CN','/en/aquatic?type=fish','/en/materials']){
    await page.goto(base+route); await page.locator('.site-footer').waitFor();
    assert.equal(await page.locator('img:not([alt])').count(),0);
    const unnamed=await page.locator('input:not([type=hidden]),select,textarea').evaluateAll(es=>es.filter(e=>!e.getAttribute('aria-label')&&!e.getAttribute('aria-labelledby')&&!e.closest('label')&&!(e.id&&document.querySelector(`label[for="${e.id}"]`))).map(e=>e.outerHTML));
    assert.deepEqual(unnamed,[]);
    assert.equal(await page.locator('h1').count(),1);
  }
  return {detailPages:pages,controlsAndImages:'named',pageH1:'single'};
});
await check('320px feeding visual capture',async()=>{
  await page.setViewportSize({width:320,height:844});
  await page.goto(base+'/en/aquatic/neon-tetra'); await page.locator('.feeding-guidance').waitFor();
  await page.locator('.feeding-guidance').scrollIntoViewIfNeeded();
  assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
  await page.screenshot({path:fileURLToPath(new URL('feeding-en-320.png',out))});
  return 'feeding-en-320.png';
});
await check('No browser exceptions',async()=>assert.deepEqual(errors,[]));
await browser.close();
await fs.writeFile(new URL('independent-results.json',out),JSON.stringify({base,time:new Date().toISOString(),evidence,errors},null,2));
for(const {name,status,detail} of evidence) console.log(JSON.stringify({name,status,detail}));
if(evidence.some(e=>e.status==='FAIL'))process.exitCode=1;
