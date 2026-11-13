# Aquarium Journal — V0 implementation baseline

Working title / 暂定名：《水草缸日记》。Updated / 更新：2026-11-05。

A PC/Steam single-player freshwater aquascaping game, targeting true 3D in Unity URP. **Localization is extensible through a locale registry, with English as the source, default and final fallback.** English and Simplified Chinese are the initial catalogs; launch languages are not yet frozen. Planned locales are not supported-language claims. Animal and plant names follow the indexed research pages. Stable IDs, scientific names and aliases are kept separately.

本项目以亲手开缸与停留观察为核心。本轮已有类型化配置、首批语言目录、可编译验证的 C# 行为与混养内核，以及 Unity 几何占位演示源码。**尚无经 Unity 编辑器验证的可玩构建，完整 V0 与 Steam 发布尚未完成。**

## Start here / 文档入口

| Document | 内容 |
|---|---|
| [Production baseline](docs/开工基线与任务看板.md) | 当前范围、关键决策、任务进入条件 |
| [V0 acceptance](docs/V0验收清单.md) | 核心逻辑检查与真实游戏验收分开记录 |
| [Configuration contract](data/README.md) | 9 张 CSV、73 条记录、字段和路线说明 |
| [In-game prices](docs/商店绝对价拍板草案.md) | 金币绝对价、套装数量、预算保护与免费养护 |
| [Internationalization](docs/多语言国际化与命名规范.md) | Locale 注册表、英文回退、各语言包、来源命名与发行标准 |
| [Global Steam language plan](docs/Steam全球语言规划.md) | 建议首发11语、分波次优先级与官方映射；不代表已有11语翻译 |
| [Behavior design](docs/鱼虾趣味交互与非刻板行为设计.md) | 物种习性、随机决策、互动与观察测试 |
| [Art reference rules](docs/物种研究与美术参考规范.md) | 鱼虾螺草参考索引、模型结构与待制作状态 |
| [Unity source package](unity/README.md) | 导入步骤、控制、已有功能与限制 |
| [Steam copy](docs/Steam商店页文案.md) | 首批英文/简中未发布草案；发行语言以注册表和构建验收为准 |
| [Current integration](docs/实施批次与验收看板.md) | 本批契约、已消费配置、统一验证命令与待Unity边界 |
| [Verification record](docs/实施状态与验证记录.md) | 本轮实际运行结果与发布阻塞 |

## Current scope / 当前范围

60cm-long tank, one triangle layout, three low-tech plants, a hang-on-back filter, Ember Tetra, Neon Tetra and Red Cherry Shrimp. First stocking follows a **fishless nitrogen-cycle challenge**. Cory behavior and high-conflict fish are isolated development fixtures. Snails remain research candidates.

初始 1500 金币；基础布景 1110，首批火焰灯 120/8尾、樱花虾 180/6只；适应观察奖励 450 后购买主养霓虹灯 260/8尾，最终余额 280。基础饲料与换水免费。数据中的 `shipping` 表示目标默认内容，**不表示已经制作或可以发行**。

Behavior uses real seconds; ecology uses a separate compressed calendar. The current prototype uses 8 real minutes per game day at 1×. Offline progression is disabled by default. Pure C# nitrogen accounting, layout/economic transactions and full-session save recovery now have executable checks. Unity import, Play, visual quality, navigation and the complete built-game loop remain unverified.

## Internationalization / 多语言国际化

`data/localization/locales.json` manages independent locale packages. The initial `en` and `zh-CN` catalogs each contain 269 keys for the currently authored text. `qps-ploc` is QA-only; planned `ja/ko/de/fr/es-ES/es-419/pt-BR/ru/zh-TW` entries do not indicate completed translations. Startup resolves an available saved choice, mapped Steam language, system locale (registered regional parent allowed), then English. Steam SDK language retrieval is not yet connected; the adapter currently passes no Steam value. Release locales must pass text, culture-formatting, font, layout and built-game checks before Steam language flags are selected. New locales do not add species CSV columns.

本轮国际化改造的实际复验另见验证记录；旧双语检查不能证明新增语言、RTL或完整游戏已经支持。

## Checks / 验证

From this directory / 在本目录执行：

```sh
python3 tools/verify_slice.py
```

The single command exports resources, runs all source checks, expects the strict release gate to remain blocked, and writes `verification/latest.json`. Individual checks are also available:

```sh
python3 tools/build_catalogs.py
python3 tools/validate_config.py
python3 tools/check_localization.py
python3 tools/check_localization.py --self-test
./unity/.tools/dotnet/dotnet run --project unity/Verification/BehaviorChecks.csproj --configuration Release
./unity/.tools/dotnet/dotnet run --project compatibility-checks/CompatibilityChecks.csproj --configuration Release
python3 tools/validate_config.py --strict-release
```

The local SDK path is optional; use system `dotnet` if available. **Strict release validation currently fails intentionally** on unresolved source/calibration/ecology/compatibility reviews; implementing a prototype model does not close those reviews. Passing the prototype checks does not establish Unity rendering, visual quality or release readiness.

旧稿保存在 [docs/legacy](docs/legacy/README.md)，不作为实施输入。原交接 Cursor 文档未能读取全文，本轮新增数值和实现方案属于可评审提案；Steam 零售价尚未确定。
