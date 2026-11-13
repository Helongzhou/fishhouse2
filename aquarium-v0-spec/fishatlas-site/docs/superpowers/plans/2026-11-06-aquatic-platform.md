# Aquatic Platform Implementation Plan

> **For agentic workers:** Use the installed subagent-driven-development workflow with independently owned subsystems and independent review. User has authorized Sol execution; do not request the same approval again.

**Goal:** Deliver the approved aquatic catalog, editable CMS, redesigned bilingual site and locally saved freshwater aquarium designer, with an honestly verified local preview.

**Architecture:** Preserve the static prototype at the site root. Build the new application in `platform/`, sharing versioned domain types across Payload CMS, published snapshots, SSR catalog and client designer. Local PostgreSQL listens on localhost:55432; initial app preview uses 4174 before acceptance.

**Tech Stack:** TypeScript, Next.js 16.4.5, Payload 3.93.0, PostgreSQL 16, React/Konva; actual lockfile is authoritative after compatibility checks. Bundled Node 24.22.1.

**Spec:** `docs/reviews/2026-11-06-aquatic-platform/01-产品需求.md` through `05-评审记录.md`.

## Global constraints

- User instruction “安排sol安装技能并实施吧” authorizes the recommended plan and skill/dependency installation. No repeated planning approval gate; public deployment and paid infrastructure are outside current local execution.
- Project is a hobbyist site. Do not add source-URL/license-entry CMS workflows or academic review requirements.
- Keep original site files and unrelated Unity project intact. This directory has no Git repository; do not initialize one merely to satisfy skill commit scaffolding.
- Frontend-design and web-design-guidelines are installed. Writing-plans and subagent-driven-development supply execution guidance. Developer-required parallel work overrides the skill's blanket ban on parallel implementers; owners must not overlap files.
- Shared type is `CatalogRelease`; `getPublicRelease(releaseId?: string): Promise<CatalogRelease>` reads immutable published content. Domain owns its concrete TypeScript types and exported operations; all API changes must notify peers before integration.
- zh-CN/en independent publishing, no undisclosed published-language fallback. No unverified or unknown data becomes an automatic pass.
- Animals use fixed adult dimensions and one instance per individual; plants and piece materials follow unit count. At most two whole-floor substrate layers with front/back thickness.
- Three implementation owners plus root maximum. Root coordinates, reviews and runs acceptance; Sol agents own product changes.

## Review focus

1. Existing old query links, refresh and back-navigation must not mix old styles/data or lose filters.
2. Partial/unknown profiles and unrepresented pair relations must remain unknown when other checks pass.
3. Import, count reduction, duplication and undo must preserve every object's identity and quantity.
4. Two tabs, interrupted save or a newer plan in an older app must not overwrite valid data.
5. Public endpoints must not expose drafts or allow unauthenticated CMS/release writes.

## Task 1: Runnable foundation and real CMS

Owner: `platform_backend`.

Files: `platform/package.json`, lockfile, Next/TS/Payload config, `src/app/(payload)/**`, `src/cms/**`, `src/server/**`, `src/app/api/**`, runtime scripts and env example. Only this owner changes dependencies. Frontend owns its separate root layout under `(site)`.

Consumes: `CatalogRelease`, `CatalogEntity`, `seedRelease`, `validateRelease` from domain. Produces: `src/server/catalog.ts:getPublicRelease(releaseId?)`, authenticated management UI/API, immutable release publication and conditional activation/rollback.

- [ ] Probe actual npm peers and local DB support, lock tested dependencies, start isolated PostgreSQL.
- [ ] Build maintainer authentication and typed categories/parameters/entities/materials/media/localization/template/rule forms; prevent anonymous CRUD/upload/activation.
- [ ] Seed only on deliberate initialization; never silently serve seed when a publication fails.
- [ ] Test draft isolation, bilingual publication, reference/type validation, archive behavior, activation conflicts and rollback. R1 catalog releases must not require an unfinished designer.
- [ ] Provide local bootstrap credentials only in an ignored private file; document start/stop/backup/recovery commands and environment differences from production S3/MFA.
- [ ] Verify TypeScript, build, authenticated edit→publish→public read and anonymous denial on live API. Report unimplemented production infrastructure explicitly.

## Task 2: Domain, seed content and deterministic checks

Owner: `platform_domain`.

Files: `platform/src/domain/**`, `platform/src/data/**`, `platform/tests/domain/**`; content assets in `public/media/**` and owned domain asset scripts.

Consumes: original 10 animals, translations, care and relations. Produces: `types.ts`, `seedRelease`, release validation, Plan operations, evaluation, schema-safe interchange and local persistence. Publish an export index to both other owners.

- [ ] Define stable IDs, units, knowledge/editorial states, locale publication states and immutable snapshots. Preserve old aliases/IDs and isolate calculator coefficients.
- [ ] Migrate existing data and add two snails, six plants and eight material entries with bilingual records; unknown values remain unknown.
- [ ] Test common-range/target checks, 45L geometry example, group modes, explicit pair conditions, minimum size and water volume; oxygen demand is not inferred satisfied from flow/aeration.
- [ ] Add count/geometry command tests: 12 fish+6 shrimp+2 snails=20 logical instances, duplication/delete/undo, adult-size tampering, two substrate layers, rotations and bounds.
- [ ] Add safe JSON snapshot/CSV handling and IndexedDB revision-conflict recovery; test 10 plans, failed transactions, unsupported schema and import original preservation.
- [ ] Obtain and verify real images separately from scene diagrams. Track actual image gaps, never claim 26 photos/52 representations if missing.
- [ ] Freeze and report interface version, tests and remaining content/asset limitations.

## Task 3: Bilingual visual catalog and designer

Owner: `platform_design_frontend`.

Files: `platform/src/app/(site)/**`, `platform/src/components/**`, `platform/src/styles/**`.

Consumes: `getPublicRelease()`, domain index, actual asset manifest. Produces: /[locale], /[locale]/aquatic, /[locale]/aquatic/[slug], /[locale]/materials, /[locale]/designer and root legacy adapter as agreed with backend.

- [ ] Read installed design skills and approved tokens; implement natural field-guide visual language with minimal labels and useful real imagery.
- [ ] Build SSR homepage, catalog, details and material pages; preserve filter OR/AND semantics, sorting, URLs, language, return and scroll behavior.
- [ ] Build unified front/top scene, inventory and accessible coordinates/quantity controls; connect pure domain commands rather than duplicate model state.
- [ ] Connect deterministic check explanations, local plan list/autosave, 20-step undo, import preview and same-revision PNG/CSV/JSON exports.
- [ ] Verify actual 1440/390 layouts, long names, empty/error states, no-drag editing, keyboard focus and public locale absence. Do not show future ecosystem controls as working features.
- [ ] Supply screenshots and issues after real browser validation; template/scene design must not imply biological simulation or photographic output.

## Task 4: Independent integration and acceptance

Owner: root coordinates a fresh Sol reviewer after implementations stabilize; implementers fix their findings.

Files: implementation reports and QA artifacts; test scripts assigned to QA only after scoped handoff.

- [ ] Review each subsystem for requirement compliance and implementation quality; root runs relevant build/domain/API checks once integration stabilizes.
- [ ] Exercise catalog, detail→designer, quantities, two views, rule unknowns, import/export/refresh, admin edit/publish and unauthorized endpoints on real local app.
- [ ] Use browser UI to inspect desktop/mobile rendering and interactive controls. Compare screenshots, then route fixes to their owners.
- [ ] Test version/caching and local persistence failures, not just fresh-browser happy paths.
- [ ] Record release-ready versus internal-preview-only gates separately. Real handset testing, external hobbyist trial and production infrastructure must never be claimed completed without evidence.
- [ ] Start the tested local app and present the usable address, management access instructions and any remaining material gaps. Preserve original prototype for rollback.

## Execution ledger

Progress, rulings, review findings and handoffs live in `docs/implementation/2026-11-06-progress.md`; authoritative FR/TC/WP requirements remain in the approved review package.
