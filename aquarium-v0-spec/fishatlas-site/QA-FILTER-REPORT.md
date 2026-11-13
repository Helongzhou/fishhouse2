# FishAtlas 筛选验收记录

日期：2026-11-05

## 结果

- 冻结版浏览器验收：`tests/filter-acceptance.mjs` 执行 **13/13 通过**，结果在 [`qa-artifacts/filter-acceptance-results.json`](qa-artifacts/filter-acceptance-results.json)。
- 最后一次微调仅把中文数值框的可见 `Min/Max` 占位词改为“最低/最高”。此后 `node --check app.js` 通过；定点浏览器复验四个中文占位词均正确，390px 英文首屏 `scrollWidth = innerWidth = 390`，页眉各项右边界不超过 390px。未再次执行全部 13 项。
- Node 与 Python 数据校验均通过：10 个物种、20 条本地化、10 个养护参数、4 条混养规则。
- 当前未发现未解决的筛选阻断问题。

## 自动验收范围

| 场景 | 证据 |
|---|---|
| 同维 OR、跨维 AND | `color=blue,orange` 返回 5 条；`category=tetra&color=red&size=small` 仅返回霓虹灯和火焰灯。 |
| 正向标签与数值 | 胎生仅孔雀鱼、大型仅胡子异型；`tank=30`、`phMin=7.8` 返回固定预期物种；`tempMin=29` 显示空结果。 |
| 未知/缺失 | 仅在 Playwright 路由中注入临时未知夹具；无条件时可见，卵生及红色精确筛选均排除，详情不把未知水层/难度误标为中层/中等。正式 JSON 未修改。 |
| URL 与控件 | 重复、无效枚举去重或合并；无效数值 URL 显示错误且隐藏结果，清除可恢复；倒置温度范围阻止应用。 |
| 状态恢复 | 预设、标签移除、语言切换、`html lang`、刷新、后退和多次语言点击均通过；排序按体长/最小缸体升序并保留 URL。 |
| 回归与移动端 | 详情保留原始体长和群养要求；单养显示单养倾向；占位图片明确标记；P1 计算器规划页可打开。390px 中英文页面无横向溢出，折叠筛选可用键盘展开并保留焦点，移动转桌面后面板可见。 |

未知夹具中的空主色、未知水层/难度是刻意模拟不完整资料的浏览器容错测试，不作为正式数据校验通过的依据。

## 截图核对

- [桌面英文鱼种库](qa-artifacts/database-desktop-en.png)：排序、预设、筛选面板和结果卡片无明显重叠；图片状态明确。
- [桌面中文组合筛选](qa-artifacts/database-combination-zh.png)：三个已选条件与两条结果一致。
- [390px 中文筛选收起](qa-artifacts/database-mobile-collapsed.png)、[展开](qa-artifacts/database-mobile-expanded.png)：布局无横溢，筛选选项和卡片文字可读；展开图已在占位词修复后重拍。
- [390px 英文首屏](qa-artifacts/database-mobile-en-first-viewport.png)：页眉和排序控件均在视口内。
- [详情占位图片](qa-artifacts/detail-placeholder-en.png)：明确标注 `PLACEHOLDER IMAGE`，同时展示物种标签和原始养护参数。

## 环境与边界

静态服务器：`http://127.0.0.1:4173/`。验收使用 `/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node`、内置 Playwright 模块和 `/Applications/Google Chrome.app/Contents/MacOS/Google Chrome` 的 headless 模式。脚本支持 `FISHATLAS_URL`、`PLAYWRIGHT_MODULE`、`CHROME_EXECUTABLE` 覆盖。

这轮只验收 P0 鱼种库和 P1 入口回归。鱼缸计算器仍为规划页，没有运行空间、混养或负荷算法；图片仍是占位或暂无照片状态。
