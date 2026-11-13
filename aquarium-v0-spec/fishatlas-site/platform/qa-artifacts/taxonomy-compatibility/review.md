# 独立产品与技术验收

状态：通过。冻结源码独立逻辑 6/6、生产代表交互 5/5、最后小修定点检查 4/4 通过，无遗留问题。

审读范围：2026-11-09 aquatic taxonomy / compatibility spec、plan、Task 1 内容报告、数据合同及目录。仅修改本验收目录；未修改产品源码，未启动、停止或重启服务。

## 已通过的内容审查

- 26 条实体、26 份原媒体对象保留；8 个材料对象与迁移前基线逐字段相同。
- 18 个生物条目均明确填写 freshwater；未把缺失水体作为淡水推测。
- 12 条旧 pair rule 去掉新增 visibility 后，与基线逐对象完全一致；新增 visibility 均为 hidden。
- 仅新增 3 对双向推荐：neon–ember、neon–panda、cherry shrimp–ramshorn。
- 按原始 care 数据独立计算，推荐温度交集分别为 24–26°C、22–25°C、20–27°C；虾螺的 pH 7–8、GH 6–14 也与原始范围相符。熊猫鼠的必要群养、沉底投喂要求已并入实际展示的 condition。

验证方式：独立 Python 逐字段比较基线与当前数据并打印 5 个对象的 care 范围。无内容阻塞项。

## 验收口径

water 参数只按支持的 freshwater / marine / brackish 枚举规范化。合法但当前无内容的 marine/brackish URL 保持零结果，不得因选项缺失退化为全部。可见水体入口仅来自现有目录中有值的条目。

## 最终独立验证

使用 bundled Node，执行：

```sh
PATH="/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:$PATH" ./node_modules/.bin/tsx qa-artifacts/taxonomy-compatibility/independent.mjs
PATH="/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:$PATH" node qa-artifacts/taxonomy-compatibility/runtime-spot.mjs
```

independent.mjs：6/6 通过，详见 independent-results.json。

- freshwater / marine / brackish 支持枚举规范化，marine/brackish 保持合法零结果且重复规范化稳定；bogus 被移除。
- 旧 shrimp/snail URL 分别等于原子类型全集，invertebrate 等于两者并集，并与 water 取交集。
- 聚合作用域清除 tetra，保留 shrimp/snail 分类；原子 shrimp 清除不适用 snail。
- 缺失 waterTypes 不推定淡水；未来 marine/brackish 样本自动出现在水体选项。
- 恰好 3 对推荐、12 条 hidden、双向去重与最多 3 个对象、熊猫鼠 22–25°C。
- rule 与 partner 的 publishedLocales 均参与可见性判断，未发布英语时不出现英语推荐链接。

runtime-spot.mjs：生产 http://127.0.0.1:4174 上 5/5 通过，详见 runtime-results.json。

- 中英文首页及水族库均采用鱼类 / 水草 / 虾螺，当前水体入口仅全部 / 淡水，目录 18 条。
- 旧 shrimp 返回 1 条、snail 返回 2 条，invertebrate 返回 3 条；marine/brackish 页面均为 0 条并保留已选条件。
- 从鱼类 tetra 切换到虾螺时，category 清除且 water 保留。快捷筛选保留 type 与 water。
- 霓虹灯显示两个推荐名称链接，点击推荐后再返回保留原 from 的 type、water、sort。
- 斗鱼没有推荐模块，桌面 detail-columns 只有一个子元素，宽度 1320 / 1320 px；中文推荐详情与英文无推荐详情在 390 px 无横向溢出。
- 无浏览器 pageerror。

已人工读取 3 张证据图片：no-recommendation-desktop.png、zh-CN-neon-tetra-390.png、en-betta-splendens-390.png。推荐区标题、对象名称与条件排版清晰；无推荐未留下第二列网格。未重复全量图片检查，也未自行执行服务生命周期命令。全套 test/typecheck/build 与 HTTP smoke 由 root/runtime owner 统一执行，本报告不把他人结果列为本代理独立执行。

## 最后小修复验

此前旧 `type=shrimp` / `type=snail` 结果缺少可见的单类范围标识，已补充为可移除的原子类型条件，保持三个一级入口。

最后生产重建后仅运行：

```sh
PATH="/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:$PATH" node qa-artifacts/taxonomy-compatibility/legacy-chip-spot.mjs
```

4/4 通过，详见 legacy-chip-results.json。中文与英文、shrimp 与 snail 两种旧 URL 均显示正确条件；点击移除后切换 `type=invertebrate`，完整保留 `water=freshwater`、`q=a`、`sort=name`。聚合结果只剩淡水条件，没有额外类型条件；390 px 无溢出。

该项已解决，无遗留 P3。此次未重跑此前已通过的独立逻辑和完整代表页面检查。
