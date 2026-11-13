import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { chromium } from '/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs';
const base='http://127.0.0.1:4174', out=new URL('./',import.meta.url), checks=[], errors=[];
const browser=await chromium.launch({headless:true,executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'});
const page=await browser.newPage({viewport:{width:1440,height:1000}});
page.on('pageerror',e=>errors.push(e.message));
async function check(name,fn){try{checks.push({name,status:'PASS',details:await fn()});}catch(e){checks.push({name,status:'FAIL',detail:e.message});}}
async function visit(path){await page.goto(base+path);await page.locator('.site-footer').waitFor();}
async function noOverflow(){assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));}
await check('Bilingual homepage and library have the three public type entries',async()=>{
 for(const locale of ['zh-CN','en']){
  await visit(`/${locale}`); const links=page.locator('.category-links a'); const hrefs=await links.evaluateAll(es=>es.map(e=>e.getAttribute('href'))); assert(hrefs.some(x=>x.includes('type=invertebrate')));assert(!hrefs.some(x=>/type=(shrimp|snail)\b/.test(x)));
  await visit(`/${locale}/aquatic`); assert.deepEqual((await page.locator('.type-tabs button').allTextContents()).map(x=>x.trim()),locale==='en'?['All','Fish','Plants','Shrimp & snails']:['全部','鱼类','水草','虾螺']);
  assert.equal(await page.locator('.entity-card').count(),18); assert.deepEqual((await page.locator('.water-tabs button').allTextContents()).map(x=>x.trim()),locale==='en'?['All','Freshwater']:['全部','淡水']); await noOverflow();
 }
});
await check('Old exact URLs, aggregate categories and valid empty water links',async()=>{
 const counts={};for(const kind of ['shrimp','snail','invertebrate']){await visit(`/en/aquatic?type=${kind}&water=freshwater`);counts[kind]=await page.locator('.entity-card').count();assert(counts[kind]>0);}
 assert.equal(counts.invertebrate,counts.shrimp+counts.snail);
 for(const water of ['marine','brackish']){await visit(`/en/aquatic?type=fish&water=${water}`);assert.equal(await page.locator('.entity-card').count(),0);assert.equal(await page.locator('.empty-state').count(),1);assert((await page.locator('.active-filters').innerText()).includes(water==='marine'?'Marine':'Brackish'));}
 await visit('/en/aquatic?type=fish&category=tetra&water=freshwater'); await page.locator('.type-tabs button',{hasText:'Shrimp & snails'}).click(); await page.waitForURL(u=>u.searchParams.get('type')==='invertebrate');const u=new URL(page.url());assert.equal(u.searchParams.get('water'),'freshwater');assert.equal(u.searchParams.has('category'),false);assert.equal(await page.locator('.entity-card').count(),counts.invertebrate);
 return counts;
});
await check('Quick filter preserves type and water; recommendation links preserve the original from query',async()=>{
 await visit('/en/aquatic?type=fish&water=freshwater');await page.locator('.preset-bar button').first().click();await page.waitForURL(u=>u.searchParams.get('tank')==='40');let u=new URL(page.url());assert.equal(u.searchParams.get('type'),'fish');assert.equal(u.searchParams.get('water'),'freshwater');
 const from='/en/aquatic?type=fish&water=freshwater&sort=name';await visit(`/en/aquatic/neon-tetra?from=${encodeURIComponent(from)}`);assert.equal(await page.locator('.mixing-note').count(),2);const link=page.locator('.mixing-note h3 a').first();u=new URL(await link.getAttribute('href'),base);assert.equal(u.searchParams.get('from'),from);await link.click();await page.locator('.detail-page').waitFor();assert.equal(await page.locator('.back-link').getAttribute('href'),from);await page.locator('.back-link').click();await page.waitForURL(base+from);
});
await check('No recommendation leaves a full-width care column on desktop; bilingual 390px remains within viewport',async()=>{
 await page.setViewportSize({width:1440,height:1000});await visit('/en/aquatic/betta-splendens');assert.equal(await page.locator('.mixing-note').count(),0);const cols=page.locator('.detail-columns');const bounds=await cols.evaluate(e=>({outer:e.getBoundingClientRect().width,child:e.firstElementChild.getBoundingClientRect().width,children:e.childElementCount}));assert.equal(bounds.children,1);assert(bounds.child>bounds.outer*.95);await cols.screenshot({path:fileURLToPath(new URL('no-recommendation-desktop.png',out))});await noOverflow();
 await page.setViewportSize({width:390,height:844});for(const [locale,id]of [['zh-CN','neon-tetra'],['en','betta-splendens']]){await visit(`/${locale}/aquatic/${id}`);await noOverflow();await page.locator('.detail-columns').scrollIntoViewIfNeeded();await page.screenshot({path:fileURLToPath(new URL(`${locale}-${id}-390.png`,out))});}return bounds;
});
await check('No browser exception',()=>assert.deepEqual(errors,[]));
await browser.close();await fs.writeFile(new URL('runtime-results.json',out),JSON.stringify({time:new Date().toISOString(),base,checks,errors},null,2));for(const c of checks)console.log(JSON.stringify(c));if(checks.some(c=>c.status==='FAIL'))process.exitCode=1;
