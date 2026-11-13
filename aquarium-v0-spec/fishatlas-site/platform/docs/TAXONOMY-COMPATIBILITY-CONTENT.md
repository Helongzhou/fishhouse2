# 水体与混养内容迁移

Task 1 已完成，内容修改范围仅为类型合同、静态目录、本文件与内容测试，未修改 UI 或筛选代码。内容实施阶段未构建或操作服务；双方冻结后，另按 root 授权完成下述统一生产切换。

## 数据合同

- 新增 `WaterType = 'freshwater' | 'marine' | 'brackish'` 与 `DiscoveryTraits.waterTypes: WaterType[]`。
- 当前 18 个生物条目（9 鱼、6 水草、1 虾、2 螺）均为 `['freshwater']`。8 个原本没有 `discovery` 的水草/螺只增加最小 `waterTypes` 对象；其他 discovery 字段改为可选，避免虚构分布、繁殖或分类资料。已有 discovery 字段原值保留。
- 8 个材料条目逐字段保持不变，继续使用既有 `material.waterTypes`。
- `PairRule.visibility` 支持 `recommended | caution | hidden`。全部 12 条旧规则保留原 ID、内容、decision 和 enabled 状态，仅增加 `visibility: 'hidden'`。
- 新目录 ID 为 `catalog-2026-11-09-taxonomy-compatibility`；时间、规则版本与语言修订标识随新内容更新，避免复用旧目录 ID。

## 推荐规则

依照确认后的设计，独立新增 3 条双语推荐，均 enabled、visibility=recommended、decision=conditional：

| 组合 | 内容边界 |
| --- | --- |
| 霓虹灯—火焰灯 | 温和的小型群游灯鱼；按共有的 24–26°C 规划，条件文案明确要求两种鱼各自满足群养数量并保留游泳空间。 |
| 霓虹灯—熊猫鼠 | 中层与底层活动互补；明确限定共有的 22–25°C，条件文案明确要求充氧水质、圆润底砂、各自满足群养数量与鼠鱼专用沉底投喂。 |
| 樱花虾—扁卷螺 | 成熟稳定的淡水观赏缸；按共有的 20–27°C、pH 7–8、GH 6–14 规划，保留生物膜、躲避处与少量补饲。 |

推荐按无方向 pair 存储，供展示层从两侧对称解析。霓虹灯有 2 个推荐对象，其他关联生物各 1 个，均低于每条目 3 个的上限。旧霓虹灯—火焰灯规则仍以 hidden 保留；推荐采用独立的新 ID，不覆盖原判断。推荐文字给出积极的适配理由与养护条件，没有把警告规则改名为推荐。

全部 26 个实体、已有照片路径和 9 鱼喂食字段保持不变。迁移脚本逐一核对了喂食与媒体的前后相等，并确认材料对象完全相等。

## 验证

使用 bundled Node 24.22.1：

```sh
export PATH="/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:$PATH"
PNPM=/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/bin/fallback/pnpm
"$PNPM" test
"$PNPM" exec tsx --test tests/catalog/content.test.ts
```

先增加断言并运行 `pnpm test`：新水体断言因水草没有 `discovery.waterTypes` 失败；规则断言因旧规则没有 `visibility` 失败，验证了红灯。

完成迁移后，内容测试 4/4 通过，覆盖原始条目/媒体/双语/旧规则保留、鱼类喂食约束、18 条淡水覆盖、合法 visibility、无缺失推荐对象、双语推荐文字与每实体不超过 3 个。熊猫鼠组合的中英文条件均检查 `22–25°C`。

内容初次完成时，完整 `pnpm test` 因并发 Task 2/3 尚未提供导出而暂有两个测试文件导入失败；各 owner 冻结后统一执行的最终全套结果为 16/16 通过，含本 Task 4 项与 UI/筛选 12 项。

Root 展示合同补充后，已将群养与鼠鱼沉底投喂的必要要求纳入中英文 `condition`，确保只显示 reason + condition 时约束仍完整；内部 action 原文保留。


## 统一生产切换

双方最终冻结后，按 root 授权严格执行 `service stop → pnpm test → pnpm typecheck → pnpm build → service start`。16 项测试、类型检查及构建均通过。

13 项关键 HTTP 烟雾通过：双语首页、带淡水的分组入口、旧虾/螺链接、三组推荐相关详情页、当前目录数据、admin 410、后台修改 API 404 与 4173 保留路径/查询的 308 跳转。HTTP 当前目录确认 26 条实体、18 条淡水生物、3 条推荐与 12 条隐藏规则。未重复执行 75 图片全量探针。

PostgreSQL 55432 端口仍关闭。脱离工具会话的生产进程为 web PID 32156、redirect PID 32157，二者状态均为 running。证据：`.runtime/evidence/taxonomy-runtime-smoke.json`。生产 URL 为 http://127.0.0.1:4174/zh-CN 与 /en，已通知 root 可进行独立视觉验收。

### Legacy 单类标识最终小修

独立验收通过后，UI 为旧 `type=shrimp|snail` 增加可见单类 chip；其 focused 测试由 UI owner 执行（taxonomy 9/9）。冻结后再次按 `service stop → build → typecheck → service start` 切换，两项构建检查通过。只补 5 项关键 HTTP 检查，确认中文虾类、英文螺类 chip 可见，合并入口不重复出现单类 chip，详情与 deployment 正常；未重跑 13 项全套烟雾或 75 图片探针。

最终生产 PID 为 web 35758、redirect 35760，均持续 running；数据库端口 55432 仍关闭。最终烟雾证据：`.runtime/evidence/taxonomy-final-chip-smoke.json`。
