import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { chromium } from '/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs';
const base='http://127.0.0.1:4174',checks=[];
const browser=await chromium.launch({headless:true,executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'});
const page=await browser.newPage({viewport:{width:390,height:844}});
for(const locale of ['zh-CN','en'])for(const kind of ['shrimp','snail']){
 const name=`${locale} ${kind} chip display and aggregate removal`;
 try{
  const q='a',label=kind==='shrimp'?(locale==='en'?'Shrimp':'虾类'):(locale==='en'?'Snails':'螺类');
  await page.goto(`${base}/${locale}/aquatic?type=${kind}&water=freshwater&q=${q}&sort=name`);await page.locator('.site-footer').waitFor();
  const chip=page.locator('.active-filters button').filter({hasText:label});assert.equal(await chip.count(),1);await chip.click();
  await page.waitForURL(u=>u.searchParams.get('type')==='invertebrate');const after=new URL(page.url());assert.equal(after.searchParams.get('water'),'freshwater');assert.equal(after.searchParams.get('q'),q);assert.equal(after.searchParams.get('sort'),'name');
  const labels=await page.locator('.active-filters button').allTextContents();assert(!labels.some(x=>/Shrimp|Snails|虾类|螺类|虾螺/.test(x)));assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
  checks.push({name,status:'PASS',query:after.search,remainingChips:labels});
 }catch(e){checks.push({name,status:'FAIL',detail:e.message});}
}
await browser.close();await fs.writeFile(new URL('legacy-chip-results.json',import.meta.url),JSON.stringify({time:new Date().toISOString(),base,checks},null,2));for(const c of checks)console.log(JSON.stringify(c));if(checks.some(c=>c.status==='FAIL'))process.exitCode=1;
