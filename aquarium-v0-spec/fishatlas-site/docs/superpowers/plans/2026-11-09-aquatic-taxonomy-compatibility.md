# 水族分类与混养推荐实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use subagent-driven-development to implement this plan task-by-task.

**Goal:** 将水族库按水体扩展为可承载淡水、海水、汽水的分类体系，并让详情页只展示少量明确的混养推荐。

**Architecture:** 在现有静态目录中给生物条目增加 `waterTypes`，将 `water` 作为 URL 可复现的筛选维度；保留 `type=shrimp|snail` 兼容入口，但前台主入口显示“虾螺”。混养规则继续保存在目录中，新增 `visibility` 控制公开展示，详情页只取 `recommended` 并限制数量。

**Tech Stack:** Next.js 16.4.5、React 19.3.1、TypeScript、静态 `src/data/catalog.json`、tsx tests。

**Spec:** `docs/superpowers/specs/2026-11-09-aquatic-taxonomy-compatibility-design.md`

## Global Constraints

- 保留现有 26 条目录、照片、双语字段和鱼类喂食信息。
- 不编造海水或汽水条目；当前生物条目全部标记为 `freshwater`。
- 不删除风险 pair rules，不恢复后台/CMS，不增加来源许可录入流程。
- 旧 `type=shrimp`、`type=snail` URL 必须继续可用；新筛选状态写入 URL。
- UI 继续支持中文和英文、桌面三列及窄屏无横向溢出。

## Review Focus

- 旧筛选 URL 与新增 `water` 参数同时出现时，类型与水体必须取交集且规范化稳定。
- 虾螺入口必须覆盖虾和螺，不能漏掉任一现有条目；旧虾/螺入口仍可直达。
- 推荐混养最多 3 个，风险/谨慎规则不能因为 `enabled` 继续泄露到前台。
- 没有推荐时不显示空的“混养提醒”标题或空卡片。
- 水体选项来自当前目录，海水/汽水未有内容时不能出现 0 条伪入口。

---

### Task 1: 目录合同与内容迁移

**Files:**
- Modify: `platform/src/domain/types.ts`
- Modify: `platform/src/data/catalog.json`
- Modify: `platform/tests/catalog/content.test.ts`
- Create: `platform/docs/TAXONOMY-COMPATIBILITY-CONTENT.md`

**Interfaces:**
- Produces `WaterType`, `DiscoveryTraits.waterTypes`, `PairRule.visibility`.
- Current catalog assigns `freshwater` to all 18 biological entries; materials keep existing `material.waterTypes`. For this release `waterTypes` means the normal display/keeping water, not every breeding-stage salinity requirement.
- Existing risk rules default to `hidden`; only approved recommendation records use `recommended`.

- [x] **Step 1: Add failing content assertions** for water type coverage, supported visibility values, maximum three recommended rules per entity, and no recommended rule pointing to a missing entity.
- [x] **Step 2: Run `pnpm test`** and confirm the new assertions fail against the old contract.
- [x] **Step 3: Update types and catalog data** without changing entity IDs or existing feeding fields. Use bilingual reason/condition/action for three recommendation pairs: neon–ember, neon–panda cory (state the 22–25 °C overlap), and red cherry shrimp–ramshorn snail. Keep the recommendation set small and practical.
- [x] **Step 4: Run `pnpm test`** and record the content report.

### Task 2: Filter and taxonomy behavior

**Files:**
- Modify: `platform/src/components/site/library-filters.ts`
- Modify: `platform/src/components/site/library.tsx`
- Modify: `platform/src/components/site/format.ts`
- Modify: `platform/src/app/(site)/[locale]/page.tsx`
- Test: `platform/tests/catalog/taxonomy-filters.test.ts`

**Interfaces:**
- `traitValues(entity, "water", categories)` returns `waterTypes`.
- `water` is a multi-value URL filter validated against the supported enum, including valid values with zero current matches. Visible options come from active entries within the selected type scope.
- The public type tabs use `fish`, `plant`, and a UI-only `invertebrate` grouping; legacy `shrimp` and `snail` remain accepted query values.

- [x] **Step 1: Add focused tests** for water filtering, invertebrate grouping, old shrimp/snail URLs, and no empty water options.
- [x] **Step 2: Implement `WaterType` labels and filtering**; make `water` available only for aquatic library entries, while materials retain their existing type flow. Support the UI-only `type=invertebrate` aggregate while preserving exact `shrimp`/`snail` queries.
- [x] **Step 3: Update type tabs and homepage links** to fish, plants, invertebrates; show water options from the selected type’s active entries rather than hardcoded empty categories or already filtered results.
- [x] **Step 4: Run focused tests and typecheck**.

### Task 3: Recommended mixing presentation

**Files:**
- Modify: `platform/src/components/site/detail.tsx`
- Modify: `platform/src/styles/site.css`
- Test: `platform/tests/catalog/mixing-recommendations.test.ts`

**Interfaces:**
- Detail resolves only enabled rules with `visibility === "recommended"`, deduplicates by other entity, and slices to three.
- Existing warning rules remain in the JSON but are not rendered in the public detail page.

- [x] **Step 1: Add a focused render/data test** covering a fish with recommendations, a fish with no recommendations, and a rule that is only caution/hidden.
- [x] **Step 2: Implement the filtered, capped recommendation section** with the existing bilingual reason/condition/action treatment.
- [x] **Step 3: Run focused tests and inspect desktop/mobile detail layout**.

### Task 4: Integration and production acceptance

**Files:**
- Modify: `platform/docs/CATALOG-RUNTIME-DELIVERY.md` or create `platform/docs/TAXONOMY-COMPATIBILITY-DELIVERY.md`
- Create: `platform/qa-artifacts/taxonomy-compatibility/`

- [x] **Step 1: Stop the managed service before build.**
- [x] **Step 2: Run `pnpm test`, `pnpm typecheck`, and `pnpm build`; restart the managed 4174/4173 service.**
- [x] **Step 3: Verify Chinese/English homepage, fish/plant/invertebrate tabs, freshwater filter, old shrimp/snail links, and detail return URLs.**
- [x] **Step 4: Verify recommended mixing count/content and absent warning-only sections across representative fish; check 390px and desktop screenshots.**
- [x] **Step 5: Run the relevant HTTP smoke checks with PostgreSQL still stopped and record all results.**

## Handoff

The root agent owns final acceptance. Sol owners must report the exact tests they ran and must not start a competing production service or edit another owner’s files.

Completed locally 2026-11-09. Final evidence and rulings: `docs/implementation/2026-11-09-taxonomy-compatibility-progress.md`; independent report: `platform/qa-artifacts/taxonomy-compatibility/review.md`.
