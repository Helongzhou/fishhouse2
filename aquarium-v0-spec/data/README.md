# V0 类型化配置：字段契约与使用边界

版本：0.2；日期：2026-11-05。这里的 9 张表取代旧混合 `docs/legacy/v0_config-v0.1.csv` 作为垂直切片的数据输入。旧数值文档中的水质公式没有实施；水质质量守恒模型和生态资料仍待主方案冻结。

## 文件与编码

所有 CSV 使用 **UTF-8-SIG（带 BOM）**，便于 Excel 直接打开中文。首行字段名为英文；布尔为 `true/false`；数值用半角小数点、无千位逗号；ID 为稳定英文标识。空字符串表示字段不适用；门槛 `none` 表示无前置条件，不是一个实际 gate。

| 文件 | 主键 | 用途 |
|---|---|---|
| tanks.csv | tank_id | 缸体尺寸、水量设计值、负荷预算、模拟契约状态 |
| items.csv | item_id | 售价、整组数量、退款、解锁和默认范围 |
| species.csv | species_id | 鱼、虾、螺、草的物种占位与行为引用 |
| plants.csv | id | 植物位置、生长形态、种植规则、光照与CO₂，三个候选及两个后续参考 |
| behavior_profiles.csv | behavior_profile_id | 趣味交互的首轮游戏参数 |
| gates.csv | gate_id | 状态迁移和事件门槛 |
| economy.csv | economy_id | 发钱账本、预算保护、免费重试政策 |
| route.csv | route_id + step_index | 唯一教学路线及资金演算；V0 仅一条路线 |
| compatibility.csv | rule_id | 体系隔离、黑名单和待研究混养关系 |

`content_status` 的含义：`shipping` 是**计划纳入 V0 默认内容**，不是“已完成发布审核”；`test_only` 只在开发者模式中加载；`planned` 是后续内容，默认商店和默认场景都不加载。只有 `shipping` 可以出现在强制路线。植物资料表用更明确的 `shipping_candidate` 对应三个现有基础商品，尚需资料与种植交互验收。物种的 `inclusion_scope=reference_only` 只供资料库和类型预留，不能生成商品。螺目前属于此类，未扩大 V0 制作范围。

玩家可见内容通过稳定本地化键读取，**默认语言en**；语言范围由`localization/locales.json`注册表动态管理。CSV保留的`display_name_en/display_name_zh_cn`是首批英文与简中命名输入，新增语言写独立语言包，不再扩加CSV列。旧`display_name`仅兼容内部读取，禁止作为玩家文本的自动回退。英文源生物名与来源页面对应：Ember Tetra、Neon Tetra、White Cloud Mountain Minnow、Red Cherry Shrimp已核实名称；白云是凉水参考支线，不进入默认热带缸。植物以Tropica页面学名作英文源名称；螺页只有Nerite snail群名，不能冒填具体种。其他语言可使用本地名称或保留学名，不强迫等于英文或简中CSV值；仍保留规范学名和来源关联。名称核实不表示完整生态与发布数据已审核。

物种 `source_status=shipping_pending` 表示研究或发布复核未完成；`research_verified` 只表示研究来源得到核验。这里的群体规模、行为概率、负荷点和过滤点均为**游戏调参**，不构成现实水族饲养指导。来源资料不会直接作为待售数值数据库批量复制。

## 尺寸、负荷和模型

**60 缸是长度 60cm，不是 60L。** 教学缸内部尺寸为 60×30×36cm，毛容积 64.8L；扣除水位、底砂、硬景后，54L 是此教学场景的有效水量设计值，不是每种布景自动得到的实测容量。

`game_bioload_capacity` 与 `game_filter_capacity` 都是无量纲游戏点。首批火焰灯8尾×0.375=3点、主养霓虹灯8尾×0.6=4.8点、虾6只×0.1=0.6点，合计 **8.4/12=70%占用，余30%**。这些是游戏调参，不按整数凑预算。本表只检查路线预算，不能据此绕过氮循环挑战。`simulation_model_id=pending_mass_balance_v1` 与 `simulation_ready=false` 明确禁止把旧稿公式当作现成模拟器。

## 字段说明

### tanks.csv

| 字段 | 类型/单位 | 规则 |
|---|---|---|
| tank_id / display_name_en / display_name_zh_cn / content_status | 字符串 | 主键、首批命名输入和装载范围；扩展语言在locale包维护 |
| inner_length_cm / inner_width_cm / inner_height_cm | 正数/cm | 内部几何尺寸；与毛容积交叉校验 |
| gross_volume_l | 正数/L | 三个尺寸之积÷1000 |
| effective_volume_l_design | 正数/L | 水位和造景后有效设计值，必须低于毛容积 |
| water_type | 枚举 | freshwater / saltwater；V0 默认淡水 |
| game_bioload_capacity | 正数/游戏点 | 总负荷的设计预算 |
| simulation_model_id / simulation_ready | ID / bool | 模拟契约标识；未完成时不可据此宣称水质系统可发布 |
| notes | 字符串 | 设计说明，禁止被程序解析成条件 |

### items.csv

| 字段 | 类型/单位 | 规则 |
|---|---|---|
| item_id / display_name_en / display_name_zh_cn / item_kind / content_status | 字符串 | 商品ID、首批命名输入、类别、装载范围；扩展语言不加CSV列 |
| species_id | 引用/可空 | 鱼虾草商品所引用的 species；硬景与设备为空 |
| unlock_gate_id | gate ID / none | 所有基础设备、三种草和模板所需物品初始可购；没有未湿缸的按天锁 |
| purchase_price_coin | 非负整数/金币 | **每次购买价**，不是每个体价 |
| units_per_purchase / unit_name | 正整数/字符串 | 每次购买交付的尾、只、丛、件等数量；禁止默认按1解释 |
| post_confirm_sell_price_per_purchase_coin | 非负整数/金币 | 一整份交易单位的回收价，不得高于购价；个体拆卖暂不开放 |
| pre_first_confirm_refund_ratio | 0–1 | 当前必须为1；首次布景确认前按实际支付账本全额退款 |
| game_filter_capacity | 非负数/游戏点 | 仅过滤设备使用；无现实流量含义 |
| game_light_output_class | none / low / mid / high | 游戏光照等级；低光教学路线允许条件化、艺术化冒氧展示 |
| route_role | 枚举 | required_base / required_trial / required_shrimp / required_mainfish / care / optional / debug |
| notes | 字符串 | 说明，不参与计算 |

`fish_trial_pack` 保留技术ID，现为 **Ember Tetra (8 fish) / 火焰灯（8尾）**，120金币/8尾；`shrimp_pack` 是180金币/6只；`fish_main_pack` 是Neon Tetra，260金币/8尾。白云金丝已移为planned/reference_only，不能继续沿用旧稿白云6尾路线。背包先按购买批次记账，避免退款、出售和重新加载时按尾数重复结算。

### species.csv

| 字段 | 类型/单位 | 规则 |
|---|---|---|
| species_id / display_name_en / display_name_zh_cn / scientific_name | 字符串 | 稳定ID、首批命名输入和资料识别名；其他locale使用各自名称键 |
| actor_kind | fish / shrimp / snail / plant | 模型支持四类，具体内容仍受范围控制 |
| content_status / inclusion_scope | 枚举 | shipping/test_only/planned；v0_default/reference_only |
| water_type / behavior_profile_id | 枚举/引用 | 体系隔离与行为参数引用 |
| game_bioload_per_individual | 非负数/游戏点 | 按每尾/只计；植物为0，不代表真实生态负荷为零 |
| game_school_min / game_school_max | 非负整数/个体 | 首轮游戏群体参数；真实习性来源待审核 |
| game_light_demand_class | none / low / mid / high | 植物使用；不能独立推导现实灯具功率 |
| source_status / ecology_reference | 状态/可空字符串 | 当前 shipping_pending；来源核验后改 research_verified 并附参考链接 |
| source_name_en / source_id / aliases / source_verification | 字符串/引用/多值/状态 | 页面原文名、统一来源ID、别名和核验进度；name_verified_ecology_pending代表名称有依据、完整生态仍待审核；group_name_only不能被当作具体种确认 |
| notes | 字符串 | 已知边界与研究待办 |

植物中文名已纠正：`anubias_nana` 为矮水榕。旧稿中的“矮生椒草”“水榕绿温蒂”“绿宫廷椒草”和把火焰灯写成红莲灯的名称均不沿用。温蒂椒草、辣椒榕尚未加入本次默认内容。火焰灯Hyphessobrycon amandae现为教学首批候选，群体8–10尾的研究建议用于设定8尾套装；0.375负荷点只属于游戏参数。

### plants.csv

位置、生长形态与养护需求是独立维度，不能把“前景”等同“高光高CO₂”，也不能把“后景”等同“容易”。多值用半角 `|`，与CSV的列分隔符逗号区分。

| 字段 | 类型/单位 | 规则 |
|---|---|---|
| id | 引用/ID | 3个shipping_candidate必须与species和现有plant商品配对；planned只供资料参考 |
| display_name_en / display_name_zh_cn / scientific_name | 字符串 | 英文优先；学名与来源原文分开保留 |
| source_name_en / source_id / source_verification | 字符串/引用/状态 | 来源原文名称、父级sources目录的统一来源ID和核验进度；资料尚未全冻结时不标发布完成 |
| aliases | 多值字符串 | 只供搜索，不覆盖来源名称；别名不意味着另一物种 |
| placement_tags | 多选枚举 | foreground / midground / background / hardscape / surface；按布局尺度可有多个标签 |
| growth_form | 枚举 | rhizome / moss / rosette / stem / stolon / floating；本轮只用前三类，其余为schema预留 |
| planting_method | 枚举 | attach_rhizome_exposed / attach_moss / rooted_crown_exposed；分别对应根茎附着、莫丝绑附、根入砂但冠部不深埋 |
| allowed_surfaces | 多选枚举 | rock / wood / substrate；浮水等后续类型不得仅靠空表面自动创建玩法 |
| rhizome_exposed | true / false / n/a | 水榕、铁皇冠和辣椒榕为true；莫丝没有根茎，为n/a；椒草用冠部种植规则，不能误用水榕规则 |
| light_requirement / co2_requirement | low / medium / high | 独立的资料与设计等级；low不等于必须额外注入CO₂；不能从位置标签反推 |
| growth_speed | slow / medium / fast | 首轮资料值，须与统一来源复核，不直接决定经济产出 |
| content_status | shipping_candidate / planned / test_only | 三个基础植物候选；温蒂椒草和辣椒榕planned，无新增商品、资产或路线义务 |

固定来源ID为tropica_anubias_nana、tropica_java_fern、tropica_java_moss、tropica_crypt_wendtii和tropica_buce_bukit_kelam；页面URL由父级sources目录统一维护，避免在各张表里复制多个不一致链接。

### behavior_profiles.csv

| 字段 | 类型/单位 | 规则 |
|---|---|---|
| behavior_profile_id / content_status / actor_kind | 字符串/枚举 | 行为参数标识与适用类型 |
| time_domain | real_seconds | 开灯惊吓、恢复、观察聚集使用真实秒，不能随4x日历变成瞬间动画 |
| startle_sensitivity01 | 0–1 | 光线突变事件的敏感度调参 |
| recovery_min_real_s / recovery_max_real_s | 非负数/真实秒 | 恢复时长采样范围；min≤max |
| approach_probability_min01 / approach_probability_max01 | 0–1 | 靠近响应的可调概率范围；不是所有物种必然聚集 |
| approach_cooldown_real_s / min_behavior_real_s | 非负数/真实秒 | 相同互动冷却和最短动作长度 |
| supports_schooling | bool | 允许调用群聚行为，不等于所有个体同步 |
| parameter_basis / notes | 字符串 | 当前都是待试玩的游戏调参；不得冒称物种实证行为 |

父行为方案可继续细化多动机、记忆和个体差异；这张最小表暂不承诺已经包含完整 Utility AI 参数。

### gates.csv

| 字段 | 类型/单位 | 规则 |
|---|---|---|
| gate_id / content_status / prerequisite_gate_id | ID/枚举/引用 | 前置关系必须无环 |
| event_key | 字符串 | 核心系统发出的明确事件；不能由UI直接伪造通过 |
| requires_wet_tank | bool | 注水以前不得开始稳缸、试水、养护的日历条件 |
| min_stable_game_hours | 非负数/游戏小时 | 连续观察窗的参数占位；不是按天自动解锁 |
| condition_contract_status | defined / pending_simulation_contract | cycle/mainfish仍须由守恒模拟器提供真实判据 |
| notes | 字符串 | 注释，不代替可执行条件 |

门槛链：布景确认→注水→**无鱼非零氮源挑战**并稳定→首批火焰灯8尾入缸适应→观察通过→主养霓虹灯8尾入缸。原文“闯缸”在游戏中只解读为完成挑战后的首批适应观察，不用活鱼验证有毒水。不能用“空缸氨和亚硝酸盐都是0”自动通过；游戏小时与真实秒的换算由主技术方案冻结。

### economy.csv

| 字段 | 类型/单位 | 规则 |
|---|---|---|
| economy_id / content_status / event_key | ID/枚举/字符串 | 经济事件标识 |
| amount_coin | 非负整数/金币 | 只有初始1500和闯缸450为正收入 |
| repeatable / required_gate_id | bool/引用 | 正收入不得重复领取；奖励领取账本必须存档 |
| policy | 枚举字符串 | 具体事务政策，见下方实现要求 |
| notes | 字符串 | 产品说明 |

`once_per_save_ledger`：奖励ID写入领取账本，与金币变化一起提交；重复事件无操作。`exact_purchase_ledger_refund`：退款依据实际购买价及购买批次，扣库存和加余额不可分开保存。`reserve_remaining_required_cost`：教学尚未完成时，可选购买后余额必须足以覆盖**下一笔阶段奖励前**所有未买强制物品。

`restore_snapshot_transactionally`：失败免费重试恢复确认检查点的场景、钱包、库存、个体、进度、购买批次和奖励账本。不得额外赠送可出售补偿鱼，也不能只回滚鱼而保留出售所得。检查点进入危险阶段前创建；没有检查点时回到首次布景编辑并全额退款。恢复是替换，不是累加；同一快照反复恢复不得积累物品或金币。

### route.csv

| 字段 | 类型/单位 | 规则 |
|---|---|---|
| route_id / step_index | ID/正整数 | 当前唯一teaching_triangle；步骤从1连续排列 |
| action | grant / purchase / care / complete_gate | 奖励、购买、免费养护和过门事件 |
| item_id / purchase_count | 可空引用/非负整数 | 有商品操作时≥1；个体数量=交易次数×units_per_purchase |
| economy_id | 可空引用 | grant操作必填 |
| required_gate_id / completes_gate_id | 引用/可空引用 | 操作前置和本步完成的门槛 |
| required_for_v0 | bool | 当前路线全部true；可选物品不混进强制脚本 |
| notes | 字符串 | 调试说明 |

教学路线基础物品1110，首批火焰灯8尾120、虾6只180合计1410；初始1500余90。基础投喂5次与换水2次均0金币。适应观察通过奖励450后余额540，主养霓虹灯8尾套装260，最终280。原先40饲料+50换水的90金币不再收费，保留为玩家余量。沉木140是可选项，未过门前会被预算保护拦住，不能花掉强制鱼虾资金。

### compatibility.csv

| 字段 | 类型/单位 | 规则 |
|---|---|---|
| rule_id / content_status | ID/枚举 | 规则与装载范围 |
| subject_kind/id / object_kind/id | 枚举/引用 | 支持tank、species、water_type；引用必须存在 |
| decision | block / prototype_allow | block用于范围/黑名单；prototype_allow只是切片临时允许，不是发布混养推荐 |
| source_status / notes | 状态/说明 | product_scope为既定产品范围；pending_review为待研究 |

## 多语言国际化配置

`data/localization/locales.json`为唯一语言注册表，`schema_version=1`、`default_language='en'`、`fallback_language='en'`。每个独立语言包是`data/localization/<code>.json`，格式为键→文本对象；默认英文包定义完整键空间。Unity导出到`Resources/Localization/<code>.json`，使用`{language,entries:[{key,value}]}`；注册表原样导出到同目录的`locales.json`。

| 注册项 | 规则 |
|---|---|
| code / native_name / english_name | 稳定语言代码、本地名和英文名；选择器由注册表枚举 |
| culture / steam_language | 格式化文化区与Steam映射独立于locale代码，例如es-419使用es-MX与latam；Steam API码允许空值（Game Support Only语言仍可手动或由OS选语）；非空API映射须大小写无关唯一 |
| direction / font_group | ltr或rtl，以及字体分组；存在字段不等于已完成字体覆盖、排版或RTL视觉验收 |
| status | ready为完整语言包；draft允许缺键；planned仅规划；qa仅开发测试 |
| fallback | en根节点为`''`；其他条目引用已注册、非planned的后继，整条链须到en；自环和回退环均拒绝 |
| release_required | 必须发布的语言包，缺文件或缺键直接失败；qa与planned不能设置为true |

当前口径：**2 ready（en、zh-CN，均release_required=true）、9 planned、1 QA（qps-ploc）**。首发规划11种真实语言：en、zh-CN、zh-TW、ja、ko、de、fr、es-ES、es-419、pt-BR、ru。尚未翻译的9种只登记规划，不要求存在JSON，也不导出或显示为可用译文；伪本地化不是第三种发行语言。

新增语言时，添加注册项及`<code>.json`即可，无需改业务代码或数据表。生成器读取现有ready/draft翻译，不用英文文本覆盖译文；draft缺键产生警告，运行时沿注册表回退至英文，导出仍保留稀疏键，不能伪装成已翻译完成。ready或release_required语言必须全键覆盖；未知键、空文本、参数名/次数/格式不匹配均拒绝。

`qps-ploc`从英文自动生成全部键，增加重音、括号与约35%的膨胀填充，保留格式占位符、转义括号、标记和生物规范名；用于发现截断、缺字与硬编码。生物规范名的精确来源约束作用于英文源，不限制其他正式语言采用本地名称。`sources/references.json`仅指向注册表，不再维护第二份固定语言列表；CSV和美术进度不被目录生成器写入。

`compatibility-checks/reason-catalog.json`可继续作为首批原因文本的authoring来源；英文源变动后同步`en.json`和所有必需语言，其余翻译在各locale包维护。当前目录有192个源键；语言包ready仅说明目录完整，字体、UI排版和平台构建仍需实测。

```sh
python3 tools/build_catalogs.py
python3 tools/check_localization.py
python3 tools/check_localization.py --self-test
```

生成器打印状态计数和覆盖率。自检在临时项目中接入第三种语言，验证翻译保留、draft稀疏回退、缺文件/缺键、参数错误、未知键和回退环拒绝；还验证空Steam API码的Game Support Only语言可ready、重复非空映射被拒绝。不改正式支持列表，不宣称新增真实翻译。

## 验证与交付门槛

```sh
python3 tools/validate_config.py
python3 tools/validate_config.py --json
python3 tools/validate_config.py --strict-release
```

普通模式验证编码、重复ID、数值、引用、门槛无环、初始物品可购、整组数量（8火焰灯+8霓虹灯+6虾）、强制路线资金、免费养护、奖励不可刷、默认内容隔离和负荷预算。`--strict-release` 还会将研究/兼容性/模拟契约待办视为发布阻塞，因此当前应明确失败。普通模式通过只说明**数据与路线可以接入原型**，不能替代模拟器、真实行为来源、存档事务和失败重试的集成测试。

## 本轮运行时导出

`python3 tools/build_runtime_config.py` 在原CSV校验通过后，将九表转为 `unity/Assets/_Game/Resources/Configuration/runtime.json`，带schema1和九表原始字节SHA-256 ContentVersion。C# `Core/Configuration` 使用显式字段DTO，不在Unity运行时解析CSV或按显示名称推断规则。导出器不改变CSV、来源资料、语言源或pending状态。

本批已消费：缸体尺寸/设计水量；商品价格/份数/回收价/范围/解锁/路线角色；植物种法/支撑面/根茎；路线必购数量/奖励前后资金保留；初始与适应奖励金额；默认行为引用/惊吓敏感度/群游开关；两生态门连续稳定时长。生态引擎目前共用稳定时长，因此两门不相等会拒绝启动，不能静默忽略其一。

导出不等于所有参数已驱动：behavior的接近概率、恢复区间、冷却与最短动作时长尚未完整映射至既有行为权重；物种生态来源、compatibility表临时关系不替代真实温度/pH/GH与成体空间参数；过滤点/负荷点仍为预算，不是硝化处理速度。氮模型其余系数是冻结的原型代码调参，未标定为真实水族预测。CSV的simulation_ready=false及全部审核标记保留，源码守恒模型完成不等于发布审核完成。
