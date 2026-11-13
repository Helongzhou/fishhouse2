# 水族库升级验收记录

日期：2026-11-06。检查对象为获批的 `AQUATIC-UPGRADE-PLAN.md` 冻结版，站点地址 `http://127.0.0.1:4173/`。

## 结果

- 升级浏览器验收：`tests/aquatic-acceptance.mjs` **10/10 通过**，结果见 [`qa-artifacts/aquatic/aquatic-acceptance-results.json`](qa-artifacts/aquatic/aquatic-acceptance-results.json)。全程记录的 JavaScript `pageerror` 为 0。
- 旧筛选回归：`tests/filter-acceptance.mjs` 最终连续两次 **13/13 通过**，结果见 [`qa-artifacts/filter-acceptance-results.json`](qa-artifacts/filter-acceptance-results.json)。旧脚本仅按新详情 DOM 调整定位，保留 OR/AND、数值、未知值、URL、历史、移动端与 P1 语义检查。
- 独立数据验收 `tests/aquatic-data-acceptance.mjs` 通过。Node 与 Python 校验器均通过：10 物种、20 条本地化、10 条饲养资料、4 条混养规则；`node --check app.js` 和升级测试脚本语法检查通过。
- 当前未发现升级范围内未解决的阻断问题。上述浏览器结论只覆盖现有 10 个示例物种、英语和简体中文。

## 已验证行为

| 范围 | 实际结果 |
|---|---|
| 类型与分类 | 鱼、虾、螺 URL 与中英入口可用；虾仅樱花虾，螺显示真实空态。切换类型清除不适用的 `category`，保留颜色和排序；“全部”只清类型，“清空”清筛选。旧 `category=gourami` 返回蜜丽丽和斗鱼，`category=betta` 仅斗鱼，卡片首分类分别显示 Labyrinth fish、Betta / 斗鱼。 |
| 筛选与数据缺失 | 同维 OR、跨维 AND、组合水层与排序固定结果通过。隔离 Playwright 响应夹具验证“高溶氧+缓流”和“常规溶氧+强流”各自命中，组合条件取交集；未知氧气、水流、水层、颜色及繁殖值不通过明确筛选。正式 JSON 未被夹具修改。 |
| 卡片与资料 | 卡片显示精确体长、最小缸体、水温和群体，Group / 群体标签适用于单养；繁殖条件命中后可见。无照片状态明确，未出现伪鱼图。详情中霓虹灯至少 8 / 建议 10、蜜丽丽有条件成对、斗鱼单养、虾建议 10 起养均通过；体长只展示一次，原卵生、食性、可展开识别特征保留。蜜丽丽 GH 待确认备注双语可见，斗鱼加盖及虾入水口提醒可见；有混养规则的火焰灯保留混养段，无提醒和规则的三角灯不出现空段。 |
| 指南与交互 | 两条中英共用指南可用键盘展开；未见统一检疫天数、固定换水比例或 W/L 功率建议。列表进入详情、详情刷新、页面内返回及浏览器后退保留筛选、排序和真实点击前的滚动位置；外部 `return` 不会跳出本站。 |
| 视口 | 1440 和 1280 为三列、1024 两列、390 中英单列。卡片和超长中英名称无横向溢出；390px 筛选可键盘展开且焦点保留。全新 390px 上下文的熊猫鼠英文、斗鱼中文详情各只有一个 `main`、一个详情 hero 和一个标题，完整截图宽 390px、高 2345/2320px，与页面高度一致。 |

## 截图核对

- [1440 英文列表](qa-artifacts/aquatic/database-1440-en.png)、[1440 中文列表](qa-artifacts/aquatic/database-1440-zh-CN.png)、[1280 英文列表](qa-artifacts/aquatic/database-1280-en.png)、[1024 英文列表](qa-artifacts/aquatic/database-1024-en.png)：列数、文字边界和信息层级已核对。
- [390 中文列表](qa-artifacts/aquatic/database-390-zh-CN.png)、[390 英文列表](qa-artifacts/aquatic/database-390-en.png)及相应展开筛选截图：单列无横溢，标题和卡片数值可读。
- [英文熊猫鼠详情全页](qa-artifacts/aquatic/detail-390-en-panda-corydoras-full.png)、[中段](qa-artifacts/aquatic/detail-390-en-panda-corydoras-middle.png)、[中文斗鱼详情全页](qa-artifacts/aquatic/detail-390-zh-CN-betta-splendens-full.png)：内容连续且未重复。先前一次从桌面直接 resize 后的 Chrome 全页截图曾出现重复页首；全新移动上下文截图、实际 DOM 数量及中段视口截图均未复现，判定为该次抓图合成现象，而非页面重复渲染。

## 环境与边界

测试使用本机 Node `/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node`、Playwright 模块和 Chrome channel。可用 `FISHATLAS_URL`、`PLAYWRIGHT_MODULE`、`CHROME_CHANNEL`、`CHROME_EXECUTABLE` 覆盖浏览器环境。一次旧脚本首次首屏等待超时，后续同站请求与两次完整串行重跑均正常；脚本现会在等待失败时记录 URL 与主区域文本，便于下次定位。

网站仍为静态样例原型；正式物种照片尚无，图片仅有占位或暂无照片状态。P1 鱼缸计算器仍是规划页面，未验证或运行配缸算法、疾病诊断或设备功率公式。
