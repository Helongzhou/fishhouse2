import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import {chromium} from '/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs';
const base='http://127.0.0.1:4174', out=new URL('./',import.meta.url), evidence=[];
const browser=await chromium.launch({headless:true,executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'});
const page=await browser.newPage({viewport:{width:1440,height:1000}});
async function check(name,fn){try{evidence.push({name,status:'PASS',details:await fn()});}catch(e){evidence.push({name,status:'FAIL',detail:e.message});}}
const rgb=s=>s.match(/[\d.]+/g).slice(0,3).map(Number);
const luminance=c=>rgb(c).map(v=>{v/=255;return v<=.04045?v/12.92:((v+.055)/1.055)**2.4;}).reduce((s,v,i)=>s+v*[.2126,.7152,.0722][i],0);
const ratio=(a,b)=>{a=luminance(a);b=luminance(b);return (Math.max(a,b)+.05)/(Math.min(a,b)+.05);};
async function style(locator){return locator.evaluate(e=>{const s=getComputedStyle(e);let n=e,bg;do{bg=getComputedStyle(n).backgroundColor;n=n.parentElement;}while(bg==='rgba(0, 0, 0, 0)'&&n);return {color:s.color,background:bg,outline:s.outlineStyle,outlineWidth:s.outlineWidth};});}
await check('Final bilingual shortcut and related controls contrast',async()=>{
 const samples=[];
 for(const locale of ['zh-CN','en']){
  await page.goto(`${base}/${locale}/aquatic?type=fish&social=schooling`);await page.locator('.preset-bar button').first().waitFor();
  for(const selector of ['.preset-bar button','.type-tabs button','.active-chip','.library-search button','.filter-form button']){
   const ls=page.locator(selector);
   for(let i=0;i<await ls.count();i++){
    const el=ls.nth(i);if(!await el.isVisible())continue;
    const normal=await style(el); assert(ratio(normal.color,normal.background)>=4.5,`${locale} ${selector} ${i} normal`);
    await el.hover(); const hover=await style(el);assert(ratio(hover.color,hover.background)>=4.5,`${locale} ${selector} ${i} hover`);
    samples.push({locale,selector,index:i,normalRatio:+ratio(normal.color,normal.background).toFixed(2),hoverRatio:+ratio(hover.color,hover.background).toFixed(2)});
   }
  }
  const btn=page.locator('.preset-bar button').first(); await btn.hover();await page.mouse.down(); const active=await style(btn); await page.mouse.up();assert(ratio(active.color,active.background)>=4.5);
  await page.waitForURL(u=>u.searchParams.get('tank')==='40');
  const selected=page.locator('.preset-bar button[aria-pressed=true]');assert.equal(await selected.count(),1);const ss=await style(selected);assert(ratio(ss.color,ss.background)>=4.5);
  await page.keyboard.press('Tab');
  const focused=page.locator(':focus');const focus=await style(focused);assert.notEqual(focus.outline,'none');assert(parseFloat(focus.outlineWidth)>0);
 }
 return samples;
});
await check('Final diet translation and care-note deduplication',async()=>{
 const catalog=JSON.parse(await fs.readFile(new URL('../../src/data/catalog.json',import.meta.url)));
 const labels={omnivore:['杂食','Omnivore'],grazer:['刮食','Grazer'],carnivore:['肉食','Carnivore'],herbivore:['草食','Herbivore']};
 const pages=[];
 for(const e of catalog.catalog.filter(e=>e.care))for(const [locale,index] of [['zh-CN',0],['en',1]]){
  await page.goto(`${base}/${locale}/aquatic/${e.id}`);await page.locator('.detail-page').waitFor();
  const diet=page.locator('.detail-columns .facts > div').filter({has:page.locator('dt',{hasText:locale==='en'?'Diet':'食性'})});
  if(e.care.diet.knowledgeState==='known')assert.equal((await diet.locator('dd').innerText()).trim(),labels[e.care.diet.value][index],e.id+' diet');
  const structured=Object.values(e.discovery?.husbandry?.notes?.[locale]||{});
  const extra=await page.locator('.detail-columns .care-note').allInnerTexts();
  for(const note of structured) assert(!extra.some(text=>text.includes(note)),e.id+' repeated structured note');
  if(!structured.length && e.care.notes[locale]) assert(extra.includes(e.care.notes[locale]),e.id+' retained original care note');
  assert.equal(await page.locator('.feeding-guidance').count(),e.kind==='fish'?1:0);
  pages.push({id:e.id,locale,diet:await diet.locator('dd').innerText(),extraNotes:extra});
 }
 return pages;
});
await browser.close();await fs.writeFile(new URL('final-ui-spot.json',out),JSON.stringify({time:new Date().toISOString(),base,evidence},null,2));
for(const {name,status,detail}of evidence)console.log(JSON.stringify({name,status,detail}));if(evidence.some(e=>e.status==='FAIL'))process.exitCode=1;
