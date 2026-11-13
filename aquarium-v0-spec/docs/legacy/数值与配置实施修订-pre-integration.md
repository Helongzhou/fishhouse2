# V0 数值与配置实施修订

版本：V0.2  
用途：给程序、数值和 QA 作为第一版可执行契约。本文先冻结“单位、公式、门槛和样例”，再把内容数据拆成类型化表；具体鱼种的真实参数可以在垂直切片验证后微调。

## 先修正的现有矛盾

| 项目 | 当前问题 | V0.2 决定 |
|---|---|---|
| 三角模板解锁 | `template_triangle.unlock_gate=gate_substrate`，但流程要求先选模板再铺底砂 | 模板解锁门改为 `none`；模板是新建缸时的起点 |
| 新手路线经济 | 验收脚本的底砂+主石+辅石+沉木+瀑布过滤+中光灯+3 种草约 1390 金币，超过 1200 起始金币 | 推荐教学路线改用低光灯、沉木为可选；强制必选项合计 1110 金币，起始 1200，保留 90 金币应急余量 |
| 稳缸门 | “过滤余量≥0”允许零余量通过 | 稳缸门要求 `filter_margin >= 20%`，且氨和亚硝酸盐连续 24 个游戏小时低于阈值 |
| 硬景数量 | 单一 `min_count/max_count` 无法表达主石、辅石和沉木 | 改用 `placement_requirements` 子表，分别记录槽位和数量 |
| 设备字段 | `filter_load` 同时被当成过滤负荷和过滤容量 | 统一为 `filter_capacity`；生物使用 `bioload_point` |
| 时间字段 | `unlock_day` 与 `unlock_gate` 关系不清 | 门槛判定顺序固定为：前置 gate → 最早游戏日 → 水质/经济条件 |

## 单位和时间基准

- 水量单位：L；V0 缸体有效水量 `54L`（60L 缸按 90% 水位计算）。
- 游戏时间：1 个游戏日 = 24 个游戏小时 = 12 分钟现实时间（1x）。
- 速度：暂停、1x、2x、4x；2x/4x 只加速日历模拟和生物需求计时，布景输入与相机保持实时。
- 模拟步长：内部固定 1 游戏小时；渲染可按帧更新，但数值结算只在小时边界发生。
- 所有数值先用浮点计算，UI 按显示精度四舍五入；存档保存未四舍五入值。

## 水质与生态公式

### 1. 生物负荷与密度

```text
total_bioload = Σ(个体 bioload_point × 数量 × stress_multiplier)
stocking_ratio = total_bioload / tank_bioload_capacity
```

V0 的 60 缸 `tank_bioload_capacity = 12`。

- `stocking_ratio <= 0.80`：安全。
- `0.80 < ratio <= 1.00`：提醒；鱼的压力缓慢增加。
- `1.00 < ratio <= 1.20`：危险；氨生成率 ×1.5，藻风险 ×1.2。
- `ratio > 1.20`：严重超载；禁止购买新鱼，若连续 24 游戏小时未处理则进入高死亡风险。

`stress_multiplier` 默认 1.0；温度、pH、混养冲突、缺氧等每项最多叠加到 1.5，避免单一因素瞬间杀鱼。

### 2. 过滤余量

```text
effective_filter_capacity = Σ(filter_capacity × maintenance_factor)
filter_margin = (effective_filter_capacity - total_bioload) / tank_bioload_capacity
```

- `maintenance_factor`：滤材正常 1.0；堵塞 0.7；损坏 0.0。
- 稳缸门：`filter_margin >= 0.20`。
- `filter_margin < 0` 时氨生成率额外 ×1.5。
- 过滤升级只改变容量，不直接清除已有氨/亚硝酸盐。

### 3. 氮循环

每个游戏小时：

```text
ammonia += (feed_load + fish_waste) × (1 - biofilter_rate)
nitrite += ammonia × nitrifying_stage_1 - nitrite_processing
nitrate += nitrite × nitrifying_stage_2
```

V0 初始参数：

- 空缸 `ammonia=0.00ppm, nitrite=0.00ppm, nitrate=5ppm`。
- 新缸菌群 `biofilter_rate=0.10`；每游戏日无超载时 +0.15，最高 0.95。
- 换水 30%：氨、亚硝酸盐、硝酸盐同时 ×0.70；不会重置菌群。
- 安全阈值：氨 `<0.25ppm`、亚硝酸盐 `<0.10ppm`、硝酸盐 `<40ppm`。
- 稳缸门要求氨和亚硝酸盐连续 24 游戏小时低于安全阈值。

### 4. 灯光、草状态和藻

```text
light_score = light_output × photoperiod_hours / plant_demand
plant_health_delta = base_growth × light_fit × water_fit - algae_competition
algae_delta = nutrient_factor × light_excess_factor - shrimp_grazing
```

V0 默认光照时长 8 游戏小时/日：

- `light_score 0.60–1.40`：草健康区间。
- `<0.60`：草生长暂停，冒氧效果关闭。
- `>1.40` 且硝酸盐 >20ppm：藻增长 ×1.5。
- 灯光不是“越高越好”；高光灯必须配合较高过滤余量或换水频率。
- 冒氧只作为视觉反馈，不直接替代过滤；当 `light_score >= 0.80` 且草健康 ≥60 时开启粒子。

### 5. 温度和硬度

- 每种生物有 `temp_min/max`、`ph_min/max`、`kh_min/max`、`gh_min/max`。
- 处于范围内：`stress_multiplier=1.0`；超出范围每 1°C 或 0.5 pH 级增加 0.1，封顶 1.5。
- V0 不模拟复杂离子变化；KH/GH 只通过换水和物品修正，先用于兼容性判断与 UI 教学。

## 换水、投喂与风险恢复

- 基础投喂每次 `feed_load=0.15`；正常投喂每天 1 次；过量投喂每多 1 次额外 +0.15。
- 换水服务消耗 25 金币，降低三种氮化物 30%；同一游戏日第二次换水效果降为 15%，防止刷洗取代养护。
- 风险恢复不瞬间完成：水质回到安全阈值后，鱼压力再经过 12 游戏小时回落；草健康经过 2 游戏日恢复。
- V0 不设置不可逆“全缸死亡”；严重超载只进入高风险状态，玩家仍可通过换水、减密度、升级过滤挽救。

## 进度门定义

| gate | 前置 | 最早日 | 数值条件 | 玩家可见说明 |
|---|---|---:|---|---|
| gate_substrate | none | 0 | 底砂覆盖率 ≥85% | 铺满缸底后继续 |
| gate_hardscape | gate_substrate | 0 | 主石 ≥1、辅石 ≥1；无碰撞穿模 | 先形成稳定结构 |
| gate_plants | gate_hardscape | 0 | 不同阴性草 ≥3 种；草槽占用 ≥3 | 先给生态留出基础植被 |
| gate_fill | gate_plants | 0 | 布景确认；水位 90% | 注水后进入养缸阶段 |
| gate_cycle | gate_fill | 3 | 氨<0.25、亚硝酸盐<0.10 连续 24 小时；过滤余量≥20% | 水质稳定，可以试水 |
| gate_trial | gate_cycle | 4 | 选择闯缸鱼；密度 ≤80% | 先用低风险鱼测试 |
| gate_mainfish | gate_trial | 6 | 闯缸鱼存活；水质安全连续 24 小时；密度≤80% | 解锁主养鱼 |

## 经济修订

### 教学路线

强制购买：天然溪砂 120 + 青龙石中 160 + 青龙石小 80 + 瀑布过滤 260 + 低光灯 220 + 矮生椒草 90 + 铁皇冠 110 + 莫丝 70 = **1110 金币**。  
起始金币 1200，剩余 90 金币，刚好覆盖 5 次基础投喂（8×5=40）和 2 次换水（25×2=50）。

沉木、深色细砾、中光灯、大和藻虾是可选或后段内容；验收脚本不得把它们写成强制项。

### 收入和支出

- 起始金币：1200。
- 闯缸通过奖励：450。
- V0 不增加随机金币掉落；收入只来自里程碑和首次观察目标，避免刷钱破坏节奏。
- 观察目标奖励：首次正常投喂 30、首次完成换水 30、首次看到冒氧 50、首次看到虾清藻 60；每项只发一次。
- 物品回收价统一为购买价 50%，出售不应成为主要收入来源。

## 类型化数据表

### Tank

`id, display_name, gross_volume_l, effective_volume_l, bioload_capacity, water_level_default, camera_bounds, allowed_water_type`

### Template

`id, tank_id, unlock_gate, substrate_zones, hardscape_slots, plant_slots, default_camera, recommended_route_id`

### Item

`id, item_type, display_name, cost, sell_price, unlock_gate, asset_key, placement_rule`

### Fish

`id, display_name, adult_size_cm, bioload_point, school_min, school_max, swim_zone, temperament, food_type, temp_min/max, ph_min/max, activity_profile, behavior_profile`

### Plant

`id, display_name, light_demand, co2_demand, growth_rate, algae_affinity, oxygen_output, attach_rule, compatible_surfaces`

### Equipment

`id, equipment_type, display_name, filter_capacity, light_output, noise_level, unlock_gate, maintenance_factor, placement_rule`

### ProgressionGate

`id, prerequisite_gate, earliest_day, numeric_conditions_json, unlocks_json, player_explanation_key`

### Economy

`id, source_type, amount, repeatable, cooldown_game_hours, trigger_condition, sink_category`

## 关键验收样例

1. **教学路线经济**：新档购买强制项后余额 90；不会出现负数；可完成 5 次投喂和 2 次换水。
2. **零余量不过门**：瀑布过滤被拆除后 `filter_margin=-1.0`，即使氨为 0 也不能通过 gate_cycle。
3. **稳缸连续性**：氨在第 23 小时低于 0.25、 第 24 小时升回 0.30，门槛重新计时，不得提前解锁。
4. **过量投喂**：连续 3 次过量投喂后氨上升，换水只降低 30%，不会一键清零。
5. **灯光与冒氧**：低光灯下 `light_score<0.60` 时冒氧关闭；中光灯和 3 种阴性草满足条件后出现冒氧。
6. **时间压缩一致性**：同一模拟日用 1x、2x、4x 运行，水质结果误差 <0.5%；动画只改变播放速率，不跳过交互反馈。
