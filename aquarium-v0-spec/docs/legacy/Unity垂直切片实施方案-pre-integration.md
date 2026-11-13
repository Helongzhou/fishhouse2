# Unity URP 垂直切片实施方案

版本：V0.1  
目标：用一条可玩闭环验证“布景 → 注水 → 稳缸 → 闯缸 → 观察反馈 → 保存重载”的停留感。  
范围：60 缸、三角模板、底砂/硬景/3 种阴性草、瀑布过滤、1 组闯缸鱼、1 组主养鱼、基础投喂和水质、本地存档、Steam 最小接入。

## 垂直切片的完成定义

玩家从新档进入，在 20 分钟内完成一次布景和注水；在 1–2 个游戏日内看到氮循环、水质警报、投喂反馈和鱼群行为变化；完成闯缸后买到主养鱼；退出游戏再进入，缸的布景、鱼、植物、水质、金币和游戏日全部恢复。

切片只验证一条质量路线，不追求先铺满物种。鱼虾行为、相机和水质反馈必须先达到“愿意停下来观察”的质量，再扩展数量。

## 模块边界

| 模块 | 负责什么 | 不负责什么 | 主要接口 |
|---|---|---|---|
| AquariumScene | 60 缸、玻璃、水面、底部和可放置区域 | 不计算水质，不保存业务数据 | `IAquariumBounds` |
| BuildMode | 底砂、石头、沉木、植物的预览、吸附、撤销、确认 | 不决定物种状态，不推进游戏日 | `IBuildCommand`, `IBuildSurface` |
| SimulationClock | 实时 Tick、游戏日、暂停、1x/2x/4x | 不直接驱动动画速度 | `ISimulationClock` |
| WaterSimulation | 温度、pH、KH、GH、氨、亚硝酸盐、硝酸盐、藻和氧 | 不处理 UI 排版 | `IWaterState`, `IWaterModifier` |
| LivestockSimulation | 鱼虾个体状态、需求、风险、死亡/恢复 | 不做寻路渲染，不直接播放 VFX | `ILivestockAgent`, `IStimulusReceiver` |
| BehaviorBrain | 感知、记忆、Utility 选择和游动目标 | 不拥有水质真值 | `IBehaviorBrain` |
| FeedingSystem | 食物粒子、投喂事件、鱼群聚集反馈和过量投喂 | 不直接修改氨值 | `IFeedAction`, `FoodEvent` |
| Presentation | 鱼虾动画、VFX、音效、UI、相机 | 不写业务数值 | 订阅模拟事件 |
| Progression | 开缸阶段、稳缸门、闯缸门、主养鱼门 | 不把锁定状态写死在 UI | `IProgressionGate` |
| SaveSystem | 本地存档、备份、版本迁移、异常恢复 | 不依赖 Steam 才能读档 | `ISaveProvider` |
| SteamBridge | 启动、成就、云存档可选能力探测 | 不阻断离线核心流程 | `ISteamBridge` |

## 数据资产（ScriptableObject）

所有静态配置使用 ScriptableObject；运行时状态使用普通 C# 数据结构，避免把场景对象当作存档。

```text
Assets/_Game/Data/
  Tanks/TankDefinition_60L.asset
  Templates/Template_Triangle.asset
  Buildables/BuildableDefinition_*.asset
  Plants/PlantDefinition_*.asset
  Livestock/FishDefinition_*.asset
  Livestock/ShrimpDefinition_*.asset
  Equipment/EquipmentDefinition_*.asset
  Simulation/WaterRuleSet_V0.asset
  Progression/ProgressionGraph_V0.asset
  Economy/EconomyTable_V0.asset
```

关键字段：

- `TankDefinition`：容量、水体 Bounds、游层高度、相机锚点、最大装饰实体数。
- `BuildableDefinition`：Prefab、占地 Bounds、允许表面、吸附规则、价格、撤回价值。
- `PlantDefinition`：光照需求、成长曲线、耗氮/产氧系数、可附着表面。
- `LivestockDefinition`：尺寸、游层、温度/pH 范围、生物负荷、食物偏好、群游范围、恐惧系数、好奇系数。
- `EquipmentDefinition`：过滤容量、噪声、光照等级、开关状态、维护修正。
- `WaterRuleSet`：所有单位、阈值、日变化速率、换水比例、离线推进上限。

## 运行时状态

```csharp
AquariumRuntimeState {
    string saveId;
    int schemaVersion;
    TankRuntimeState tank;
    BuildRuntimeState[] placedObjects;
    LivestockRuntimeState[] livestock;
    PlantRuntimeState[] plants;
    EquipmentRuntimeState[] equipment;
    WaterRuntimeState water;
    ProgressionRuntimeState progression;
    EconomyRuntimeState economy;
    SimulationRuntimeState clock;
}
```

每只鱼虾有稳定的 `entityId`、物种 ID、个体性格种子、当前位置、当前需求、压力值、饥饿值、最后进食时间和最近事件记忆。位置只作为恢复用快照，重载后由行为脑在 0.5–1 秒内重新接管，避免保存出现在玻璃外。

## Tick 与时间压缩

- 观察 Tick：每秒 10 次逻辑更新，渲染帧率独立；鱼虾移动、感知和事件反应使用逻辑 Tick。
- 水质 Tick：每 1 游戏分钟结算一次；水质数值使用双精度或定点小数，避免 4x 累积误差。
- 1 游戏日的真实长度必须在常量中定义；垂直切片建议先用 8 分钟/游戏日，便于 20 分钟内看到变化。
- 1x/2x/4x 只改变水质、成长、需求和日历结算频率；鱼虾动画不简单乘速，仍以真实时间播放。
- 暂停时停止所有模拟 Tick，不接受投喂、换水和布景命令；UI 与相机可继续响应。
- 离线推进默认不超过 1 个游戏日；根据保存时的 `lastUtc` 计算，异常退出和系统改时钟时取安全上限。

## 鱼虾行为实现

行为采用“Utility 评分 + 带噪声的目标选择”，不使用固定顺序脚本。每 0.5–1.5 秒重新评估一次，评估间隔本身也有轻微随机偏移。

候选目标包括：巡游、觅食、靠近同伴、远离刺激、躲藏、贴底清藻、休息、观察玩家、回到安全游层。每个目标得分由需求、环境、个体性格、近期记忆和随机扰动共同决定；选择后设置 2–8 秒的短期承诺，防止每帧抖动。

### 互动事件

- **开灯惊吓**：灯光从关到开时产生 `LightShock`，按恐惧系数给个体施加压力和短暂逃逸方向；鱼虾优先进入石缝/沉木背后，随后以分段概率重新探头、结伴、恢复巡游。连续快速开关增加耐受冷却，防止玩家刷出重复反应。
- **误判投喂**：玩家靠近玻璃、移动鼠标或打开投喂 UI 时产生低强度 `PlayerApproach`；部分个体会向前聚集，但没有食物时 1–3 秒后失望散开。真实投喂才产生 `FoodScent`，并按食物类型、饥饿值和游层吸引目标。
- **实际投喂**：食物粒子进入水体后，鱼按距离、饥饿、群体关系和性格竞争；少量鱼先到，其他鱼看到邻近个体进食后提高跟随概率，形成自然聚集而非瞬移到固定点。
- **虾清藻**：虾从藻量高、距离近且安全的表面中选择目标；同一表面被清理后会换点，不连续重复同一条路径。
- **观察者效应**：相机靠近会提高部分好奇个体的靠近权重，同时提高胆小个体的保持距离权重；效果有上限和冷却，避免玩家只要推近就永远触发。

## 布景与场景组织

```text
Assets/_Game/Scenes/
  Bootstrap.unity
  Aquarium_V0_Triangle.unity
Assets/_Game/Prefabs/
  Aquarium/Aquarium_60L.prefab
  Buildables/Substrate_*.prefab
  Buildables/Hardscape_*.prefab
  Plants/Plant_*.prefab
  Livestock/Fish_*.prefab
  Livestock/Shrimp_*.prefab
  Equipment/Filter_HangOn_*.prefab
  Presentation/WaterSurface.prefab
```

场景只存固定灯光、缸体、相机锚点和布景容器。可购买物件由 `BuildRuntimeState` 实例化；鱼虾由 `LivestockSpawner` 根据存档生成，禁止把运行时鱼对象作为唯一真相。

## 相机与观察

- 采用轨道相机 + 玻璃内观察限制体积；推近距离受缸体 Bounds 和玻璃碰撞约束。
- 相机靠近鱼时只发送观察刺激，不显示独立放大镜 UI。
- 视线遮挡使用低成本层级遮罩，避免玻璃折射和粒子造成大规模 Physics 查询。
- 相机目标可锁定鱼群 2–3 秒，但不自动替玩家追踪，避免观察变成看过场。

## 存档与 Steam 最小接入

- 本地存档格式：JSON 或二进制均可，但必须带 `schemaVersion`、校验和和写入时间。
- 写入流程：写临时文件 → flush → 原子替换主档 → 保留 3 个轮换备份。
- 每次加载先校验主档，失败后按最近备份回退，并在 UI 给出可读提示。
- 迁移类按版本注册：`SaveMigration_001_to_002`；禁止在运行时静默丢字段。
- Steam 初始化失败、未登录、云存档不可用都不阻断本地存档。
- 云存档放在本地存档之上，首个垂直切片只接成就和能力探测；云同步和冲突 UI 可留到 P1。

## 性能目标

- 目标配置：1080p、中档 PC、URP；满缸 16 条小型鱼 + 6 只虾 + 40 个植物/硬景实例时保持 60 FPS 目标，最低不低于 45 FPS。
- 逻辑行为使用集中式 Tick 和空间分区，不给每只鱼挂高频 Update + 多个 Physics 查询。
- 鱼虾寻点采用预采样水体节点或低频 Nav 目标，不在玻璃水体内运行完整 NavMesh。
- 水草动画、气泡和水面使用 GPU/批处理友好的 Shader/VFX；远处对象降低动画频率。
- 垂直切片阶段就记录 CPU/GPU frame time、GC Alloc、加载时间和存档写入时间。

## 开工里程碑

### M0：技术基线（3–5 个工作日）

- 建立 Unity URP 项目、输入系统、场景加载和日志。
- 建立 ScriptableObject 定义、运行时状态和空存档读写。
- 用假数据跑 1x/2x/4x Tick，并能重载状态。

### M1：布景闭环（5–8 个工作日）

- 60 缸、三角模板、底砂覆盖、主石/辅石/沉木、3 种阴性草。
- 放置预览、吸附、撤销、确认注水；相机推近和玻璃边界。
- 先用占位模型验证操作手感和镜头距离。

### M2：水质与过滤（4–6 个工作日）

- 瀑布过滤、灯光开关、氮循环最小模型、换水和风险提示。
- 录制空缸、闯缸、过量投喂三条水质曲线，确认不会软锁。

### M3：鱼虾观察（6–10 个工作日）

- 1 组闯缸鱼、1 组主养鱼、虾占位。
- Utility 行为、个体种子、开灯惊吓、玩家靠近误判投喂、真实投喂聚集。
- 以 30 分钟观察测试验证重复行为、穿模、聚集和恢复。

### M4：存档与发布切片（3–5 个工作日）

- 存档备份、版本迁移、异常退出恢复。
- Steam 初始化、成就能力探测、离线启动。
- 性能剖析、首轮 QA、生成可试玩构建。

里程碑工作日为程序主线估算，未包含最终鱼草模型、绑定、动画、音效、UI 美术和商店素材制作。

## 垂直切片验收

- 新档 20 分钟内完成三角布景并注水。
- 开灯后至少 60% 的鱼/虾在 1 秒内出现方向变化，3–10 秒内出现分批恢复，不能全部同步。
- 玩家靠近但未投喂时，至少一部分鱼靠近并在 1–3 秒后散开；真实投喂时聚集延迟有差异。
- 同一物种连续观察 10 分钟，巡游路径、觅食目标和响应时机不出现固定循环。
- 保存、退出、重载后，布景、水质、鱼虾个体种子和游戏日一致。
- 1080p 中档机满缸目标 60 FPS，GC 峰值、存档时间和加载时间有记录。
