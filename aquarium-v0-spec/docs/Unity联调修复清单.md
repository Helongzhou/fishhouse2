# Unity 联调修复清单

日期：2026-11-05；交付审计更新：2026-11-06。当前真实联调目标固定Unity6000.6.8f1（revision `237b6593792b`，Apple Silicon）/ URP17.6.0，工程为`unity6-validation`。原`unity`保留2022.3.62f3（revision `96770f904ca7`）/ URP14.0.11源码基线；该版本因本机Gatekeeper拒绝而未继续运行。前批f1安全警报及随后f3安装历史不改写。安装/EOL/macOS兼容边界见安装负责人记录。科学与发布14项pending、9个planned语言状态保持不变，不扩大Steam发布范围。

## 职责

| 范围 | 负责人 |
|---|---|
| 官方Hub/Editor安装及安装记录 | sol ecology |
| 新增Editor/Validation工具及verification/unity证据 | sol gameplay |
| 现有Core/UnityAdapter/Editor setup/Packages/ProjectSettings实际修复 | sol integration |
| Unity进程时段、统一复验与结案 | 父agent |

同工程的Unity进程由负责人统一排期。此清单中的静态检查、源码验证与真实编辑器结果分开记录；未取得真实日志时不宣称导入、Play、渲染或构建通过。

## 首轮静态检查与修复

| ID | 问题与影响 | 最小修复 | 当前证据 |
|---|---|---|---|
| U01 | 入口将unscaledDeltaTime截为0.25s；2fps会丢一半真实模拟时间 | Update传完整frame elapsed；AdvanceSimulation按Core允许的≤60s分块；继续使用30Hz行为/独立生态日，未引入UTC离线推进 | Play-03真实Adapter的0.5s推进与15×1/30s结果exact一致；最新build04已有部分键盘输入证据；完整键鼠路径未验收 |
| U02 | URP Resources材质已生成但active pipeline为空时，入口仍无条件clone URP基材；存在builtin管线粉色风险 | 只有active pipeline存在时使用URP opaque/water资源；无pipeline时保留Standard fallback | 真实URP资源导入/相机通过；build04英文4K截图已显示水箱与生物、非粉屏，完整场景/性能继续验收 |
| U03 | 四角非共面时，原4顶点/2三角底砂和Core双线性高度不一致，可能显示石/虾穿砂 | 改为16×8网格，采样同一双线性高度；153顶点，不改坡度规则或行为坐标 | 静态最坏范围saddle误差0.195mm；真实MeshCollider创建通过，贴砂画面仍待人工验收 |

应用暂停或失焦时不推进；恢复首帧丢弃可能包含挂起间隔的delta，玩家主动暂停字段不变。AdvanceSimulation输入契约为0–3600真实秒，较长host帧按≤60秒分块；拒绝非有限/负值/超界，防止异常输入造成无界循环。没有UTC差额补算。

## 验收范围与当前边界

- 固定版本包解析、导入编译、Prepare的URP renderer/pipeline、Graphics/Quality指派与材质资源已由真实batch验收。
- Aquarium.unity的相机、3D根节点、生物与底砂Collider已由历史Play验收；build04实际截图已显示水箱与生物，完整贴砂/物理导航/性能仍未验收。
- JsonUtility目录/语言及随包字体Resources加载已通过；英文4K标签裁边已修好，英文1280窗口已分项实测，鼠标与中文/其他语言逐屏仍待验收。
- 新档、检查点、食物与个体储备的JsonUtility往返续算及语义恢复/备份已由batch-04验收；保留所述double往返边界。
- 入口长帧与固定分帧exact一致已由历史Play-03验收；build04真实Space/F/L/F5/F9输入有下述分项证据，不宣称所有键鼠路径通过。

## 本批实际验证

静态修复完成后，按变更运行相关源码检查；真实Editor证据见下方Unity6各批次记录。已运行 `python3 tools/check_localization.py`：269键覆盖通过，en/zh-CN为ready，9planned仍不导出。

## Unity6独立候选

2022f3被本机Gatekeeper拒绝后，用户通过Hub安装受支持的6000.6.8f1（237b6593792b），负责人冻结Editor并安排独立`unity6-validation`项目验收。原2022产品Core/UnityAdapter/版本基线保留，Validation工具可独立更新；URP17.6.0已与安装随包metadata及真实PackageInfo匹配。初始迁移改副本版本和defaultRenderPipeline API，随后增加下述U04修复，详情见[Unity6迁移联调说明](Unity6迁移联调说明.md)。真实运行期间产品源码冻结，不以安装成功替代导入/Play/存档结论。

## Unity6 batch-01真实失败与U04修复

证据：`verification/unity/unity6-validation/unity6-batch-01/report.json`及`editor.log`。真实Editor6000.6.8f1与PackageInfo URP17.6.0精确匹配；编译无错误，Prepare、资源材质、启动场景、运行时配置前9项通过。随后新档JsonUtility→Restore在ValidateSavedLayout失败，尚未到Play或画面验收。

U04：Unity普通inline class不保留null，空Layout.Substrate变成零高对象。仅Unity6副本新增HasSubstrate，并在Set/Refund/Clone/Undo/Redo同步；Restore候选层将false且严格空载荷规范化为null，不修改输入快照。true缺失/零高/NaN或false附带非空载荷仍拒绝。当前布局、撤销/重做历史和重试检查点统一处理。

GameplaySnapshot提升schema2并只接受2，旧schema1原型档明确拒绝，不迁移、不静默抹掉旧底砂。外层Session schema1及行为schema2保持。其他合法快照inline class均必需；RetryCheckpoint已有显式Has标志。同批产品修复不回写2022基线。

未改既有测试；临时工程将原Gameplay/Integration检查链接Unity6副本Core，47/49项通过，属于纯C#回归。

## U04真实复验与JsonUtility数值边界

`unity6-batch-02`及`unity6-batch-03`真实Editor报告均通过前16项。U04已实测通过：新档无底砂、空编辑检查点及重试、撤销到底砂不存在后序列化、重做恢复真实底砂均保持正确语义；真实存在但高度无效的底砂仍被拒绝。证据位于`verification/unity/unity6-validation/`下对应批次的`report.json`及原始JSON。此结果不代表整批或Play/渲染验收通过。

batch-03保留原始exact断言，填水且含非递归检查点的恢复成功，但JSON逐字比较失败。`roundtrip-difference.json`确认仅7处植物Position.X（含检查点及其历史）由`-0.13999999999999999`变为`-0.14`、或`-0.09999999999999999`变为`-0.1`；最大变化1 ULP，约2.78×10^-17米。该差异来自真实JsonUtility浮点往返；不能宣称JSON逐字一致、double位级一致或无损二进制往返，也无需据此改写产品存档。

负责人已批准仅跨JsonUtility的验收采用类型感知比较：结构、离散字段、ID、金币、进度、账本整数及RNG状态严格一致；有限连续double的允许误差为`1e-12 * max(1, abs(a), abs(b))`，逐条记录差异。暂停/分帧等未跨JsonUtility的exact断言保持不变，非法存档拒绝检查保持不变。batch-04真实40/40通过、0errors/0warnings，包含完整8/6/8→280、checkpoint/食物/个体储备往返续算；最大double差异为World.Agent.NextProbe的约1.42×10^-14。

## Play启动索引竞争与复验

Play-01/02均通过17项游戏/Adapter检查，但总结果失败于UnityEditor.Search启动索引的List越界，完整失败报告保留。只读官方6000.6源码与安装6000.6.8f1 DLL确认：Search启动初始化通过delayCall执行；原验收入口在executeMethod内立即请求Play，而Search.index尚不存在。SetupDefaultIndexFirstUse看到isPlayingOrWillChangePlaymode会直接返回，随后EnumerateAll访问空列表首项。属于验收启动生命周期问题，无需修改游戏行为或清空Library。

gameplay工具改为持续记录所有错误、留在EditMode让正常delayCall和索引初始化完成；只读检查Search.index及默认DB ready、非updating，连续两次空闲更新后才进入Play，保留超时/API缺失失败及跨域日志。Play-03真实19/19通过、0errors；索引由Loading正常进入Complete后才Play。证据为`verification/unity/unity6-validation/unity6-play-03`。没有关闭索引、过滤错误或改全局偏好；人工输入、字体、渲染质量仍待验收。

## 本机macOS构建

Unity6副本新增BuildMacOS入口，明确StandaloneOSX、OSArchitecture.ARM64、Mono2x、createXcodeProject=false、BuildOptions.None；Prepare后保存设置，输出BuildReport摘要/步骤消息，失败抛错。不冒充现有Windows入口可用，不装新模块，不发布。新目录准备脚本通过`macos-arm64-mono-build.patch`复现入口，临时新项目与当前helper字节一致；不覆盖已有副本。

`unity6-macos-build-01`实际exit0、BuildReport Succeeded、0errors/0warnings，耗时16.717秒，报告总大小76,685,191字节。产物`unity6-validation/Builds/macOS/AquariumJournal.app`的主程序和UnityPlayer.dylib经file及lipo核验均仅arm64，命令/源码hash/日志/BuildReport/架构证据保存在同名verification目录。manual-player-01随后曾备份实际absence并暂存batch-04真实14生物fixture，但两次CUA检查均被系统锁屏阻止，未取得.app人工GUI结果；按负责人通知，已保留fixture至after并恢复原五个存档文件全部不存在的状态，hash核对一致、无Editor/Player进程，语言PlayerPrefs未改。下一轮使用新的manual证据目录重新暂存。人工验收步骤和独立备份恢复工具见`tools/README-unity6.md`。

## 随包字体修复与build-02

实机build-01的Player.log暴露运行时依赖系统Noto Sans，造成字体加载错误和IMGUI显示残影。Unity6副本现随包加入官方Noto Sans CJK SC Regular（OFL 1.1，来源commit、版权、覆盖范围和SHA-256见`Assets/_Game/Resources/Fonts/NOTICE.txt`），Unity导入的`.meta`明确`includeFontData: 1`。英文与zh-CN共用该字体的拉丁及简体中文覆盖；其他planned locale仍需独立字形/布局QA，不宣称已完成。

`AquariumGameEntry`和独立demo均按`font_group`从`Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular")`加载，缺失资源直接抛错；已移除`Font.CreateDynamicFontFromOSFont`及所有系统字体族回退。Editor batch新增Resources.Load非空检查。真实`unity6-batch-05`通过41/41、0errors/0warnings，检查详情含字体名`Noto Sans CJK SC`；编辑器日志无Unable-to-load-font-face或动态字体调用。

`unity6-macos-build-02`实际exit0、BuildReport Succeeded、Mono2x/ARM64/BuildOptions.None、0errors/0warnings，产物总大小93,127,793字节。新主程序和UnityPlayer.dylib均经file/lipo核验仅arm64，字体资源随包进入构建；命令、源hash、字体hash、meta、BuildReport和架构证据保存在同名verification目录。此为当时的构建层证据，之后实际截图发现布局问题；不将build02成功构建写作人工UI通过。

## 4K响应式UI修复与build-03

人工截图发现固定288px面板在4K下商品/按钮不可完整读取。最初对“固定45%相机起点”的诊断不准确：原式`min(.45,312/Screen.width)`在3840宽实际为8.125%。build-03曾尝试1280×800设计坐标的GUI.matrix缩放、360设计宽面板和260宽商品按钮；人工复验仍发现label裁边，该方案未通过，现由下面的build-04替代。失败截图/旧计算证据保留。

静态响应式计算证据覆盖1280×800（scale1、camera起点30%）和当前3840×2160（scale2.7、camera起点27%），见`verification/unity/unity6-validation/unity6-macos-build-03/responsive-layout-check.json`。`unity6-batch-06`通过41/41、0errors/0warnings，Adapter离线编译0；准备脚本新副本复现与当前GameEntry字节一致。

`unity6-macos-build-03`实际exit0、BuildReport Succeeded、Mono2x/ARM64/BuildOptions.None、0errors/0warnings，产物总大小93,128,305字节。主程序和UnityPlayer.dylib经file/lipo核验均仅arm64，字体资源和响应式代码已进入构建；命令、源hash、BuildReport和架构证据保存在同名verification目录。其后人工4K复验仍有裁边，未通过；当前以build04及下方实际截图为准。

## 持久字体样式与build-04

gameplay只读官方6000.6 IMGUI源码确认：OnGUI返回后才执行部分GUILayout测高，布局项保存GUIStyle引用；如果finally中把共享style字体/字号改回默认，测量与Repaint绘制会使用不同指标。build-04改用持久独立GUISkin和GUIStyle，font显式设为随包Noto，fontSize、fixedHeight=0、wordWrap和padding保持一致，finally只恢复skin引用，不再改回布局项引用的样式字段；不使用GUI.matrix缩放。

1280×800保留288px面板、14px字号，3840×2160改用960px面板、24px字号；camera和鼠标边界均按实际面板加左右间距计算，起点分别24.375%/25.625%。长商品名允许换行，名称宽扣除实际价格CalcSize、scrollbar及样式边距，按钮高度使用同宽CalcHeight向上取整；所有label/status至少留足真实lineHeight+padding和CalcHeight。首次Repaint通过`AQUARIUM_UI_LAYOUT`记录实际分辨率、字体/字号/lineHeight与矩阵，供人工运行核对。

字体许可已纠正为官方固定commit的`Sans/LICENSE`原文（SHA-256 `6a73f9541c2de74158c0e7cf6b0a58ef774f5a780bf191f2d7ec9cc53efe2bf2`），早期通用OFL模板不作为最终交付许可。NOTICE保留字体精确版权、商标、2.004版本、字体及许可来源与hash；普通可读许可/NOTICE随包位于`Contents/Resources/ThirdPartyNotices/NotoSansCJK`。BuildMacOS复制后刷新本地ad-hoc seal，deep/strict签名验证exit0。两个入口均不销毁Resources共享Font，只清理缓存。

batch-07真实通过41/41、0errors/0warnings；build-04真实exit0、BuildReport Succeeded、ARM64/Mono2x/None、0errors/0warnings，报告总大小93,129,825字节。file/lipo核验主程序和UnityPlayer仅arm64；构建命令绑定全部字体/样式/Setup源码及许可hash，准备脚本新目录七文件与当前源码字节一致。构建完成时Editor/Player已退出；随后负责人已实际启动build04开展人工验收。当前状态以下方manual-player-02证据为准，静态布局计算和batch不替代截图。

## build-04实际app分项复验

`manual-player-02/analysis-01`对已记录快照作只读审计：五项状态检查通过。01/02/03暂停快照完整字节一致，时钟、钱包90、8 Ember+6虾、12份食物及23张receipt均不变；04只增加一次开灯receipt；05只增加一次12份投喂receipt、外部氮输入1.44 mgN。04→05期间累计13餐/摄入1.56 mgN包含原有食物，不能全部归因于新投喂；LastFoodId是感知标记，不是摄入归属账本。05→06恢复比较2258个严格值、1433个decimal值，无业务/结构/ID/RNG/整数差异，80处codec差异最大4e-15，均在批准容差内。7份快照与4张截图共11项hash匹配；当前分析脚本后来修改，报告保留的executed-analyzer.py hash与当时执行记录一致。

负责人记录的Space、F/L、F5/F9操作时间线与09–12截图支持暂停冻结、暂停拒绝F/L、恢复后开灯/投喂及读取恢复。09–12的英文标题、状态和长商品名已完整，英文4K标签裁边已修好；截图可见实际水箱与生物，不等于全游戏渲染/物理导航/性能通过。`07-zh-paused.png`实际仍为英文，不能作为中文或鼠标语言切换通过。`15-en-1280-loaded-paused.png`与1280日志另证明英文1280×800客户端的可见标题/状态和商品换行可读，F9已载入暂停、亮灯、14动物/90金币状态。中文切换、鼠标菜单、zoom、right-drag和glass仍未验收。

本轮交付为安装与macOS候选的部分联调通过：474项纯C#核心、当前batch-07的41项Editor检查、当前build-04及arm64架构/本地adhoc签名完整性有通过证据；英文4K与1280窗口、部分键盘/存档流程已有分项实测。历史Play-03的19项smoke早于字体/UI修改，不作为最新UI回归；不写全面验收通过。

manual-player-01的锁屏及恢复属于历史。manual-player-02已实际启动build-04；CUA重置重连后坐标操作仍报noWindowsAvailable，Raise有SCStreamError -3812片段，AX click(0)返回成功，按键与截图可用；这不证明坐标点击送达。只读input-environment-diagnostic记录Player全窗口6、on-screen窗口0；CGSession字段和Guardian存在不足判定锁屏，也未证明坐标事件到达游戏。用户前台请求未获答复，本轮结束鼠标尝试；不能判定产品鼠标bug，也不能判其通过。中文切换、鼠标菜单、zoom、right-drag、glass待新的manual-player-03复验；截图07实际英文，不计中文通过。

manual-player-02/restore-verification.json与save-manifest确认恢复完成：primary、bak1..3、tmp五路径均回到测试前不存在，after保留primary及三份backup并逐一hash匹配，fixture未变；PID53279已退出且无Editor/Player。两份相关plist在清理前后整文件hash不变且无aquarium.language键，只能说明实际UI/日志为en，不能写成保存了显式en偏好或具有会话前prefs基线。最终player-1280-final.log及stdout已归档并核对hash；1280退出有native线程prematurely-finalized诊断，历史4K有IMK mach-port错误，均保留，不称Player全日志零错误。构建/Editor报告的0错误/0警告仅指各自报告范围。

Windows构建/机器、Steam发布、完整视觉性能及14项科学/发布pending未通过；仅en/zh-CN目录ready，9个planned与QA伪本地化不等于11语言翻译完成。后续按 `manual-player-02/next-manual03-retest.md` 使用新manual-player-03目录，重新stage已留存的05或06暂停档继续；最终输入结论见 `actual-input-outcome.md`。禁止复用已restored的manifest。


## U05 空缸/关灯可见性修复与build-05

用户新档60缸已购两株附生草、1300金币时，右侧缸体近乎黑色。GameEntry只设ambientLight但未明确ambientMode，场景也无可靠skybox/烘焙探针；URP Lit暗色底板、边框、背板缺乏可靠环境补光。候选现显式Flat环境光和linear SH ambient probe，RenderSettings.sun固定为唯一directional，保留关灯.55/开灯1.15；底板/框单独使用可辨中性色，背板背景适量提亮。未加第二directional，未改LightIsOn生态语义、经济、存档路径或build04字体/UI，也未改旧unity/基线。

`unity6-macos-build-05`真实编译/构建Succeeded，ARM64 Mono2x/None、0错误/0警告、93,130,911字节；deep/strict签名验证通过，随包OFL/NOTICE存在。命令、源码hash、日志、BuildReport和签名记录保存在同名目录。本轮没有重跑batch；41/41仍属历史batch-07，19项Play smoke仍属Play-03各自版本。

负责人真实Player截图在`verification/unity/unity6-validation/visibility-fix-01/`：`01-user-empty-fixed.png`证明用户1300金币/两草空缸底、背、框清晰；`02-stocked-light-off.png`证明独立注水生物fixture关灯鱼草仍可辨；`04-stocked-light-on-confirmed.png`证明实际开灯更亮且未曝白。当前为1280中文。`03-stocked-light-on.png`命名有误，实际L被暂停拒绝，不计开灯证据。右拖、侧角及新光照4K未验，不写全面视觉通过；用户进度恢复由负责人另行确认。

复现入口`tools/prepare_unity6_validation.py`新增visibility-lighting.patch及hash；新目录复现与候选GameEntry字节一致。非git变更的source-before、review.diff、source-sha256.json和[实现报告](../verification/unity/unity6-validation/visibility-fix-01/implementation-report.md)留存供审查。
