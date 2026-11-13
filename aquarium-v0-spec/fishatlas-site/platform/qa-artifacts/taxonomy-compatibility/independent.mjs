import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import * as filters from '../../src/components/site/library-filters.ts';
import * as format from '../../src/components/site/format.ts';
const release = JSON.parse(await fs.readFile(new URL('../../src/data/catalog.json', import.meta.url), 'utf8'));
const living = release.catalog.filter(e => ['fish','plant','shrimp','snail'].includes(e.kind));
const checks = [];
async function check(name, fn) { try { checks.push({name,status:'PASS',details:await fn()}); } catch(e) { checks.push({name,status:'FAIL',detail:e.message}); } }
const normalized = s => filters.normalizeFilters(s, living, release.categories);
const result = s => filters.filterEntities(living, normalized(s), 'en', release.categories);
await check('Valid marine and brackish URLs remain valid zero-result selections', () => {
  for(const water of ['marine','brackish']) { const p=normalized(`type=fish&water=${water}`); assert.equal(p.get('water'), water); assert.equal(p.get('type'),'fish'); assert.equal(result(p.toString()).length,0); assert.equal(normalized(p.toString()).toString(),p.toString()); }
  assert.equal(normalized('water=bogus').has('water'),false);
});
await check('Old shrimp and snail URLs are exact nonempty intersections with water', () => {
  for(const kind of ['shrimp','snail']) {const actual=result(`type=${kind}&water=freshwater`).map(e=>e.id).sort(); const expected=living.filter(e=>e.kind===kind).map(e=>e.id).sort(); assert(expected.length>0); assert.deepEqual(actual,expected);}
  assert.deepEqual(result('type=invertebrate&water=freshwater').map(e=>e.id).sort(),living.filter(e=>['shrimp','snail'].includes(e.kind)).map(e=>e.id).sort());
});
await check('Aggregate scope removes fish categories and retains both invertebrate categories', () => {
  assert.equal(normalized('type=invertebrate&category=tetra,shrimp,snail&water=freshwater').get('category'),'shrimp,snail');
  assert.equal(normalized('type=shrimp&category=tetra,shrimp,snail').get('category'),'shrimp');
});
await check('Water is data-backed, missing water stays unknown, future water options sort stably', () => {
  assert.equal(living.length,18); assert.deepEqual(filters.availableWaterTypes(living),['freshwater']);
  const unknown=structuredClone(living[0]); delete unknown.discovery.waterTypes;
  assert.deepEqual(filters.traitValues(unknown,'water'),[]);
  assert.equal(filters.filterEntities([unknown],new URLSearchParams('water=freshwater'),'en',release.categories).length,0);
  const marine=structuredClone(living[0]); marine.discovery.waterTypes=['marine'];
  const brackish=structuredClone(living[0]); brackish.discovery.waterTypes=['brackish'];
  assert.deepEqual(new Set(filters.availableWaterTypes([brackish,marine,...living])),new Set(['freshwater','marine','brackish']));
});
await check('Exactly three approved undirected recommendations, no hidden old rule enters output', () => {
  assert.equal(release.catalog.length,26); assert.equal(release.pairRules.filter(r=>r.visibility==='hidden').length,12);
  const pairs=release.pairRules.filter(r=>r.visibility==='recommended').map(r=>[r.a,r.b].sort().join('|')).sort();
  assert.deepEqual(pairs,[['neon-tetra','ember-tetra'],['neon-tetra','panda-corydoras'],['red-cherry-shrimp','ramshorn-snail']].map(x=>x.sort().join('|')).sort());
  for(const locale of ['zh-CN','en']) for(const entity of living) {const rows=format.recommendedMixing(release,entity.id,locale); assert(rows.length<=3); assert.equal(new Set(rows.map(r=>r.other.id)).size,rows.length); for(const {rule} of rows)assert(rule.enabled&&rule.visibility==='recommended');}
  const panda=release.pairRules.find(r=>r.visibility==='recommended'&&[r.a,r.b].includes('panda-corydoras'));
  for(const locale of ['zh-CN','en'])assert(/22[–-]25/.test([panda.reason[locale],panda.condition[locale],panda.action[locale]].join('')));
});
await check('Unpublished rule and unpublished partner are removed before cap and deduplication', () => {
  const rule=structuredClone(release.pairRules.find(r=>r.visibility==='recommended'));
  rule.publishedLocales=['zh-CN'];
  let fixture={...release,pairRules:[rule]};
  assert.equal(format.recommendedMixing(fixture,rule.a,'en').length,0);
  assert.equal(format.recommendedMixing(fixture,rule.a,'zh-CN').length,1);
  delete rule.publishedLocales;
  fixture={...release,pairRules:[rule],catalog:release.catalog.map(e=>e.id===rule.b?{...e,publishedLocales:['zh-CN']}:e)};
  assert.equal(format.recommendedMixing(fixture,rule.a,'en').length,0);
  assert.equal(format.recommendedMixing(fixture,rule.a,'zh-CN').length,1);
});
await fs.writeFile(new URL('./independent-results.json',import.meta.url),JSON.stringify({time:new Date().toISOString(),checks},null,2));
for(const c of checks)console.log(JSON.stringify(c));
if(checks.some(c=>c.status==='FAIL'))process.exitCode=1;
