# Unity 6 独立联调副本

日期：2026-11-05；交付审计更新：2026-11-06。2022.3.62f3官方包虽然深度签名通过，但被本机macOS27 Gatekeeper以Unnotarized拒绝；不绕过系统，安装事实由安装负责人记录。用户已通过Hub正常安装6000.6.8f1；安装负责人已核验签名、公证与license正常，负责人冻结该Editor。当前实际验收候选为 `unity6-validation`（Unity 6000.6.8f1 / URP 17.6.0），原 `unity`（2022.3.62f3 / URP 14.0.11）仅为保留基线。实际 PackageInfo、Editor batch、Play smoke与macOS ARM64构建已有通过证据；独立应用操作和发布验收分别记录。

## 官方依据

| 来源 | 本次实际读取 |
|---|---|
| [Unity发行API](https://services.api.unity.com/unity/editor/release/v1/releases?version=6000.6.8f1&architecture=ARM64&platform=MAC_OS) | 6000.6.8f1，revision237b6593792b，2026-11-03，SUPPORTED、recommended=true |
| [官方Graphics 6000.6分支package.json](https://raw.githubusercontent.com/Unity-Technologies/Graphics/6000.6/staging/Packages/com.unity.render-pipelines.universal/package.json) | URP17.6.0，core/shadergraph/universal-config依赖均17.6.0 |
| [官方6000.6 URP要求](https://docs.unity3d.com/6000.6/Documentation/Manual/urp/requirements.html) | 对应Unity6.6手册的版本与系统要求；不能用旧URP14代表Unity6兼容 |
| [官方URP升级指南](https://docs.unity3d.com/6000.6/Documentation/Manual/urp/upgrade-guides.html) | URP17.2及以后参见主Unity升级章节，仍需实际项目升级检查 |
| [官方URP17.6源码](https://raw.githubusercontent.com/Unity-Technologies/Graphics/6000.6/staging/Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRenderPipelineAsset.cs) | 仍有public Create(ScriptableRendererData)；本项目Prepare生成策略可继续使用 |

公开packages.unity.com元数据请求只返回旧版本，17.6.0直链返回404；它不能证明候选包已解析。本次17.6.0候选最初来自官方6000.6源码分支；随后 batch-04 与 play-03 已通过实际 PackageInfo 验证项目精确解析到17.6.0。没有伪造 packages-lock 或以安装目录包版本替代项目解析证据。

## 可复现准备

项目根执行：

```sh
python3 tools/prepare_unity6_validation.py
```

默认目录为`unity6-validation`，仅当不存在时创建；再次准备使用`--destination`指定新的空目标，不覆盖已有联调结果。脚本只复制`unity/Assets/_Game`（含最新Editor/Validation）、Packages manifest和必要ProjectSettings，不复制Library/Temp/Logs/导入缓存/Build。生成源SHA-256清单并核对原2022目录未变。复制的完整行为、生态、玩法、语言资源和场景均保留，不新增玩法。

当前可复现差异包含候选 Editor/URP、`GraphicsSettings.defaultRenderPipeline`、下述 U04 底砂存在性/schema2 补丁，以及 macOS ARM64 Mono 构建/报告入口、官方OFL随包字体与持久独立IMGUI字体样式/CalcHeight布局。验收器另含类型化存档比较和等待 Editor 启动完成再进入 Play 的 gate。准备工具在新副本应用产品及 macOS 构建补丁；其生成的 `not_run` 只描述准备当时，不能覆盖后续运行报告。验收工具从每个工程的 ProjectVersion 和 manifest 读取预期，要求 PackageInfo 精确匹配；记录按 project 目录分开。

## 迁移边界

- 新副本不复用URP14导入产物。Prepare将生成renderer、pipeline及opaque/transparent资源材质，并指派Graphics/Quality。
- 保留Unity6默认RenderGraph；源码没有自定义ScriptableRenderPass/Blit，不为规避验收强制打开兼容模式。
- 保留IMGUI/legacy input和现有3D流程。真正输入、字体、渲染、shader变体、JsonUtility双精度/继承字段、完整存档和低帧率推进均以真实Editor结果为准。
- 14项科学/发布pending、9个planned语言、269键目录、8/6/8教学范围与无离线推进规则不变。
- 进程/GUI由负责人统一排期；准备脚本不启动Unity。实际运行结果归属于 Unity 6 候选，原2022历史不改写。

## 当前证据

报告位于 `verification/unity/unity6-validation/`，时间为UTC。当前实际候选为冻结的build-04源码与产物。

| 层次 | 结果与范围 |
|---|---|
| 纯C#核心 | `unity6-source-regression-01`：Core netstandard2.1构建零警告/错误；行为49、兼容42、生态287、玩法47、集成49，共474项通过。30项输入hash当前仍一致。 |
| 当前Editor batch | `unity6-batch-07`：41/41、exit0、0错误/0警告；含真实PackageInfo Unity6000.6.8f1 / URP17.6.0、Resources随包字体加载和完整8/6/8→280路线。其8项记录源hash当前一致。 |
| 历史Editor Play smoke | `unity6-play-03`：19/19、exit0、90次Editor update、errors为空。实际Awake/生产session/22实体、相机及collider、0.5秒host frame等于15个30Hz frame；该报告早于最新字体/UI修复。 |
| 当前macOS构建 | `unity6-macos-build-04`：Succeeded、exit0；Mono2x / ARM64 / None，0错误/0警告、93,129,825字节、3.375545秒；8项记录源hash当前一致。 |
| 本地封装 | 当前主程序与UnityPlayer hash匹配architecture.json，直接lipo均为arm64；deep/strict签名再验exit0，为adhoc、无TeamIdentifier，非Developer ID发布签名或公证。 |
| 实际app | manual-player-02已启动，部分键盘/存档与英文4K画面证据如下；1280鼠标、中文及其他未覆盖路径继续验收。 |
| Windows / Steam | Windows模块未安装，无Windows构建/机器或Steam发布验收。 |

batch-07复验九表实际JsonUtility、无底砂/检查点语义、undo/redo和非法底砂拒绝、干缸/完全暂停、非零挑战和连续窗口、首批8 Ember+6虾、食物及每动物摄入氮、备份与原子恢复、450奖励只发一次、全路线8 Ember+8 Neon+6虾及280金币。en/zh-CN各183个标量UI键格式化通过；不等于9个planned语言或全部画面通过。跨JsonUtility有限double容差为 `1e-12 * max(1, abs(a), abs(b))`，结构/ID/离散状态/钱包/整数账本/RNG严格一致；batch-07五组最大绝对误差依次为0、2.78e-17、4.44e-16、2.22e-16、8.88e-16，不宣称位级或JSON文本相同。

随包字体为Noto Sans CJK SC Regular2.004，来自官方commit `f8d157532fbfaeda587e826d4cd5b21a49186f7c`，OTF SHA-256 `2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b`；`.meta`明确includeFontData=1。两个入口只用Resources.Load，缺失报错，清理时不Destroy共享Font。许可为该commit的Sans/LICENSE原文，SHA-256 `6a73f9541c2de74158c0e7cf6b0a58ef774f5a780bf191f2d7ec9cc53efe2bf2`；NOTICE准确记录Adobe版权、Google商标与来源。实际.app中的 `Contents/Resources/ThirdPartyNotices/NotoSansCJK/OFL-1.1.txt` 与NOTICE均为普通可读文本，hash与tools源文件一致。早期带占位copyright的通用模板已替换，不作为交付许可。

`manual-player-02/analysis-01`对已记录快照作只读审计：五项状态检查通过。01/02/03暂停快照完整字节一致，时钟、钱包90、8 Ember+6虾、12份食物及23张receipt均不变；04只增加一次开灯receipt；05只增加一次12份投喂receipt、外部氮输入1.44 mgN。04→05期间累计13餐/摄入1.56 mgN包含原有食物，不能全部归因于新投喂；LastFoodId是感知标记，不是摄入归属账本。05→06恢复比较2258个严格值、1433个decimal值，无业务/结构/ID/RNG/整数差异，80处codec差异最大4e-15，均在批准容差内。7份快照与4张截图共11项hash匹配；当前分析脚本后来修改，报告保留的executed-analyzer.py hash与当时执行记录一致。

负责人记录的Space、F/L、F5/F9操作时间线与09–12截图支持暂停冻结、暂停拒绝F/L、恢复后开灯/投喂及读取恢复。09–12的英文标题、状态和长商品名已完整，英文4K标签裁边已修好；截图可见实际水箱与生物，不等于全游戏渲染/物理导航/性能通过。`07-zh-paused.png`实际仍为英文，不能作为中文或鼠标语言切换通过。`15-en-1280-loaded-paused.png`与1280日志另证明英文1280×800客户端的可见标题/状态和商品换行可读，F9已载入暂停、亮灯、14动物/90金币状态。中文切换、鼠标菜单、zoom、right-drag和glass仍未验收。

初始migration-source-manifest是42项源文件的准备历史，not_run不是当前运行状态。当前原unity对这42项仅Validation验收器后续改变，产品Core、UnityAdapter及2022/URP14基线保留。旧batch-04/Play报告只认证各自版本；当前batch07/build04各记录8项字体/样式/helper输入hash，不冒称全工程快照。每份证据按其hash与实际覆盖范围解释。

## 失败与修复历史

- `unity6-batch-01`：前9项通过后，真实JsonUtility将null inline `Layout.Substrate`还原成零值对象，恢复触发非法布局；没有写成全面通过。
- U04只修改Unity6候选：Gameplay增加 `HasSubstrate`、快照schema2，只规范化明确不存在且严格为空的inline对象，真实非法底砂仍拒绝；undo/redo、clone、refund/set和checkpoint均保持存在性。旧schema1原型档直接拒绝，不迁移；World2/Session1不变。
- 补丁 `tools/unity6_patches/inline-substrate-schema2.patch` 的SHA-256为 `6f0e0f286f18e1bc908eb4c2375e1b170adde665a9afc52e8e656e3d3cb618c8`。新目标由准备工具应用，禁止覆盖正在联调的副本。
- `unity6-batch-02/03`：16/17，U04语义已通过，JSON文本全等检查因7个植物X坐标的浮点规范化失败，例如 `-0.13999999999999999`→`-0.14`，最大约1ULP/2.78e-17米。经负责人同意改为上述类型化比较后，batch-04为40/40通过；没有放松离散值和非法档校验。
- `unity6-play-01/02`：各17/18、exit1。均记录一次 `ArgumentOutOfRangeException`，来自 `UnityEditor.Search.SearchDatabase.<EnumerateAll>` / `GetDefaultSearchDatabase` / `SearchInit.IndexationOnStartup`；严格“无未捕获引擎或adapter错误”检查失败，历史保留。
- 后续定位为测试驱动过早进入Play。修复只等待正常Editor启动、首个delayCall、索引ready且非updating/Loading Complete及额外idle update；不删除索引、不改Search设置、不屏蔽错误。play-03记录3.047秒索引仍Loading、13.351秒正常ready/Complete后放行，19/19通过、errors为空。

14项科学/发布pending仍未关闭；9个planned语言、269键目录、8/6/8教学范围与无离线推进规则不变。

## 本轮交付状态

本轮交付为安装与macOS候选的部分联调通过：474项纯C#核心、当前batch-07的41项Editor检查、当前build-04及arm64架构/本地adhoc签名完整性有通过证据；英文4K与1280窗口、部分键盘/存档流程已有分项实测。历史Play-03的19项smoke早于字体/UI修改，不作为最新UI回归；不写全面验收通过。

manual-player-01的锁屏及恢复属于历史。manual-player-02已实际启动build-04；CUA重置重连后坐标操作仍报noWindowsAvailable，Raise有SCStreamError -3812片段，AX click(0)返回成功，按键与截图可用；这不证明坐标点击送达。只读input-environment-diagnostic记录Player全窗口6、on-screen窗口0；CGSession字段和Guardian存在不足判定锁屏，也未证明坐标事件到达游戏。用户前台请求未获答复，本轮结束鼠标尝试；不能判定产品鼠标bug，也不能判其通过。中文切换、鼠标菜单、zoom、right-drag、glass待新的manual-player-03复验；截图07实际英文，不计中文通过。

manual-player-02/restore-verification.json与save-manifest确认恢复完成：primary、bak1..3、tmp五路径均回到测试前不存在，after保留primary及三份backup并逐一hash匹配，fixture未变；PID53279已退出且无Editor/Player。两份相关plist在清理前后整文件hash不变且无aquarium.language键，只能说明实际UI/日志为en，不能写成保存了显式en偏好或具有会话前prefs基线。最终player-1280-final.log及stdout已归档并核对hash；1280退出有native线程prematurely-finalized诊断，历史4K有IMK mach-port错误，均保留，不称Player全日志零错误。构建/Editor报告的0错误/0警告仅指各自报告范围。

Windows构建/机器、Steam发布、完整视觉性能及14项科学/发布pending未通过；仅en/zh-CN目录ready，9个planned与QA伪本地化不等于11语言翻译完成。后续按 `manual-player-02/next-manual03-retest.md` 使用新manual-player-03目录，重新stage已留存的05或06暂停档继续；最终输入结论见 `actual-input-outcome.md`。禁止复用已restored的manifest。
