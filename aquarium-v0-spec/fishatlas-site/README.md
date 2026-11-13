# FishAtlas 水族库原型

这是一个无构建依赖的淡水生物资料原型，收录 10 个示例物种，提供英语和简体中文。水族库按鱼、虾、螺分组；当前样例没有螺，直达螺类会显示空结果。鱼缸计算器仍是 P1 规划页，没有运行中的配缸算法。

## 页面与数据

- `index.html`、`app.js`、`styles.css`：首页、水族库、详情和计算器占位页。
- `data/species.json`：物种身份、`organism_type`、爱好者分类和外观标签。`organism_type` 为 `fish`、`shrimp` 或 `snail`；`category_tags` 可多选，不等同于类型。
- `data/species_localizations.json`：中英文名称、摘要、正文及既有筛选标签。
- `data/care_profiles.json`：体长、缸体、水质、水层和 `husbandry` 饲养信息。`water_layers` 可包含多个水层，`water_layer` 是其中的主水层。`husbandry.oxygen_need` 与 `flow_preference` 独立，群体建议由 `group_mode` 结合既有 `school_min`、`school_recommended` 表达；未知值保留为 `unknown`、`null` 或空数组。
- `data/care_guides.json`：新鱼观察与隔离、水质与换水的中英文共用指南。
- `data/compatibility_rules.json`、`data/content_notes.json`：适用条件、混养提醒和编辑备注。
- `data/media_assets.json`：图片状态。样例只有占位和暂无照片状态，没有正式物种照片。
- `data/tank_profiles.json`、`data/tank_plan_items.json`：P1 方案存储预留。`calculator_*` 字段只为后续计算器保留，不从新饲养字段推导数值。
- `schemas/`、`scripts/`：数据契约与 Node、Python 校验器。

水族库支持同维多选取并集、跨维取交集；包括类型、分类、颜色、精确体长区间、水层、溶氧、水流及原有数值条件。排序、筛选和语言写入 URL。切换类型会清除不适用的分类；类型“全部”只清除类型，“清空”清除筛选。详情提供基础条件、饲养环境、喂食布置、混养提醒及可展开的通用指南；从列表进入后可返回原来的筛选、排序与滚动位置。

## 运行与校验

需要 Node.js 18+、Python 3 和静态 HTTP 服务器。项目不需要安装 npm 依赖。

```sh
node scripts/validate-data.mjs
python3 scripts/validate-data.py
node tests/aquatic-data-acceptance.mjs
node --check app.js
python3 scripts/serve-preview.py
```

也可用 `npm run serve`，或用 `--port` 指定端口。预览服务固定从本项目目录提供文件，并为所有响应设置 `Cache-Control: no-store`，避免浏览器复用旧 CSS、JavaScript 或 JSON。打开 [本地站点](http://127.0.0.1:4173/?view=database&lang=zh-CN)。直接打开 `index.html` 可能被浏览器阻止加载 JSON。

另开终端运行浏览器验收：

```sh
node tests/filter-acceptance.mjs
node tests/aquatic-acceptance.mjs
```

两个脚本使用 Playwright 与 Chrome channel；可通过 `FISHATLAS_URL`、`PLAYWRIGHT_MODULE`、`CHROME_CHANNEL` 或 `CHROME_EXECUTABLE` 指向其他环境。旧筛选回归结果写入 `qa-artifacts/`，升级验收结果和截图写入 `qa-artifacts/aquatic/`。参见 [筛选回归报告](QA-FILTER-REPORT.md)与 [水族库升级验收报告](QA-AQUATIC-REPORT.md)。

## 边界

这些 JSON 是前台开发样例，不是最终 CMS 或生产数据库。资料用于饲养决策参考，不提供疾病诊断、药量、统一检疫天数、固定换水比例或加热功率公式。暂无正式物种照片；占位状态不代表真实物种影像。计算器页面不会计算空间、负荷、混养或设备功率。
