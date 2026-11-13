# Unity6 本机构建与人工验收

固定Editor6000.6.8f1、URP17.6.0，所有Editor/Player进程由负责人排期；以下命令不得与同工程运行重叠。原2022基线不用于本机构建。

当前产物为`unity6-macos-build-05`，ARM64 Mono、BuildOptions.None，BuildReport Succeeded且0errors/0warnings。build05修复空缸/关灯可见性，1280中文真实Player空缸及独立注水fixture的off/on截图通过；侧角、右拖及新光照4K未验。详见[本次实现与证据报告](../verification/unity/unity6-validation/visibility-fix-01/implementation-report.md)。历史batch-07通过41/41，未为build05重跑；Play-03也保留其原版本范围。UI采用持久独立样式，显式随包字体、字号、wordWrap、padding及CalcHeight高度，保持identity matrix。不要再使用build-03的GUI.matrix整屏缩放方案；它未通过人工字形裁边验收。

字体为Noto Sans CJK SC Regular 2.004，官方来源与SHA在`Assets/_Game/Resources/Fonts/NOTICE.txt`；`.otf.meta`明确includeFontData=1。`OFL-1.1.txt`为官方固定commit的Sans/LICENSE原文，SHA-256为`6a73f9541c2de74158c0e7cf6b0a58ef774f5a780bf191f2d7ec9cc53efe2bf2`，已替换早期通用模板。构建将许可与NOTICE作为普通文本放入`.app/Contents/Resources/ThirdPartyNotices/NotoSansCJK`并刷新本地ad-hoc签名；build-05已deep/strict验证通过。仅当前拉丁与简体中文资源用于本轮验收，planned locales没有因此完成。

## 准备新副本

```sh
python3 tools/prepare_unity6_validation.py --destination /tmp/aquarium-unity6-new
```

目标必须不存在。准备脚本应用底砂schema2、macOS构建、随包字体/验收器、当前UI与visibility-lighting补丁，复制字体及真实许可/NOTICE，并记录各补丁与源文件SHA-256，不覆盖已有`unity6-validation`及其Editor生成设置。

## ARM64 Mono构建

项目根执行；证据目录应为新批次。

```sh
mkdir -p verification/unity/unity6-validation/unity6-macos-build-new
'/Applications/Unity/Hub/Editor/6000.6.8f1/Unity.app/Contents/MacOS/Unity' \
  -batchmode -quit \
  -projectPath '/Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/unity6-validation' \
  -executeMethod Aquarium.UnityEditorTools.AquariumProjectSetup.BuildMacOS \
  -aquariumBuildOutput '/Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/unity6-validation/Builds/macOS/AquariumJournal.app' \
  -aquariumBuildReport '/Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/verification/unity/unity6-validation/unity6-macos-build-new/build-report.json' \
  -logFile '/Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/verification/unity/unity6-validation/unity6-macos-build-new/editor.log'
```

入口明确使用StandaloneOSX、OSArchitecture.ARM64、Mono2x、createXcodeProject=false、BuildOptions.None，无WaitForManagedDebugger。BuildReport非Succeeded会抛错；命令exit、report结果与实际产物都须检查。当前`unity6-macos-build-05`已实际成功（93,130,911字节、3.470339秒、0错误/0警告），精确argv、源hash、exit0、日志和报告保存在同名verification目录。主程序名从Info.plist的CFBundleExecutable读取，本批为`Aquarium Journal`：

```sh
file 'unity6-validation/Builds/macOS/AquariumJournal.app/Contents/MacOS/Aquarium Journal'
lipo -archs 'unity6-validation/Builds/macOS/AquariumJournal.app/Contents/MacOS/Aquarium Journal'
file 'unity6-validation/Builds/macOS/AquariumJournal.app/Contents/Frameworks/UnityPlayer.dylib'
lipo -archs 'unity6-validation/Builds/macOS/AquariumJournal.app/Contents/Frameworks/UnityPlayer.dylib'
```

两者本批均仅arm64。Windows/MacIL2CPP未装；现有Windows入口不代表可构建或已验收。此处不安装模块、不发布产物。

## 暂存真实存档并恢复

companyName与productName均为`Aquarium Journal`。macOS Player优先复用已存在的Editor目录`~/Library/Application Support/Aquarium Journal/Aquarium Journal`；若该目录不存在，官方API所述Player目录为`~/Library/Application Support/unity.Aquarium Journal.Aquarium Journal`。执行前应核实实际目录，不盲目在两个目录同时写档。

使用batch-04真实`stocked-with-food-and-checkpoint.json`，包含8 Ember、6樱花虾、wallet90、食物、个体储备与非递归检查点，SHA-256为`523cb867ae4b53d73cad4cb5498e0fda6abbad80c988cff5785f0124cc19807a`。这是中途14生物状态；完整8/6/8→280由真实batch/Play报告另行验证，不把fixture冒充完成状态。

build-04在1280×800使用288px面板和14px显式字号；3840×2160使用960px面板和24px字号，camera起点分别为312/1280=24.375%、984/3840=25.625%。商品名称可换行，其可用宽度扣除实际price的CalcSize、scrollbar、box与margin；商品高度和label/status高度按该字体真实CalcHeight/lineHeight计算。静态尺寸证据位于`verification/unity/unity6-validation/unity6-macos-build-04/responsive-layout-check.json`；Player.log首次Repaint记录`AQUARIUM_UI_LAYOUT`实际字体指标与矩阵，英文4K实际截图09–12已确认标题/状态/长商品名完整；英文1280窗口亦有截图15；鼠标/中文仍待新的复验。

Editor和Player退出后才暂存；每次选择新证据目录。工具重新检查primary、bak1..3、tmp的实际存在性，复制原文件并记录hash，然后复制fixture。保留原语言PlayerPrefs。

```sh
python3 tools/manual_player_save.py stage \
  --fixture verification/unity/unity6-validation/unity6-batch-04/stocked-with-food-and-checkpoint.json \
  --save-directory '/Users/zhouhelong/Library/Application Support/Aquarium Journal/Aquarium Journal' \
  --evidence verification/unity/unity6-validation/manual-player-new
```

`manual-player-01`曾按负责人授权暂存，manifest与stage-verification记录原五个文件均不存在，暂存hash与fixture相同。随后两次CUA检查均被系统锁屏阻止，人工GUI没有运行结果。现已按负责人通知恢复：五个文件重新全部不存在，无Editor/Player进程，未改PlayerPrefs；暂存fixture保留在`after/`且hash一致。证据为`restore-verification.json`。下一轮使用新的manual证据目录重新stage，不复用已恢复的manifest。

应用Awake总是新档，需点击现有“读取/Load”按钮（或F9），才走实际AtomicSaveStore→JsonUtility→Restore→RebuildLayout。不要用注入内存会话替代此验收。Save/F5会写盘；应用没有退出自动保存。

人工验收结束并退出Player，负责人通知后恢复同一批次：

```sh
python3 tools/manual_player_save.py restore \
  --evidence verification/unity/unity6-validation/manual-player-new
```

工具先把验收后的五个实际文件复制到`after/`并记录hash，再恢复原文件或原absence；原备份hash不匹配会拒绝。已在临时目录实测“原档全无”和“原primary/bak2/tmp存在”两种情况，均能完整恢复并保留验收产物，重复restore不改文件。工具不操作PlayerPrefs。

## Editor GUI备用入口

若需单独检查Editor，只在获分配的独占时段运行，不加batchmode或quit：

```sh
'/Applications/Unity/Hub/Editor/6000.6.8f1/Unity.app/Contents/MacOS/Unity' \
  -projectPath '/Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/unity6-validation' \
  -executeMethod Aquarium.UnityEditorTools.AquariumProjectSetup.OpenScene \
  -logFile '/Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/verification/unity/unity6-validation/manual-player-01/editor-gui.log'
```

等待Editor正常启动索引完成后再点击Play；Play-03已验证此生命周期。真实.app的输入、字体、渲染质量以人工验收记录为准。14项科学/发布pending和9planned语言不因本机构建通过而解除。

## 当前人工证据范围

`manual-player-02/analysis-01`对已记录快照作只读审计：五项状态检查通过。01/02/03暂停快照完整字节一致，时钟、钱包90、8 Ember+6虾、12份食物及23张receipt均不变；04只增加一次开灯receipt；05只增加一次12份投喂receipt、外部氮输入1.44 mgN。04→05期间累计13餐/摄入1.56 mgN包含原有食物，不能全部归因于新投喂；LastFoodId是感知标记，不是摄入归属账本。05→06恢复比较2258个严格值、1433个decimal值，无业务/结构/ID/RNG/整数差异，80处codec差异最大4e-15，均在批准容差内。7份快照与4张截图共11项hash匹配；当前分析脚本后来修改，报告保留的executed-analyzer.py hash与当时执行记录一致。

负责人记录的Space、F/L、F5/F9操作时间线与09–12截图支持暂停冻结、暂停拒绝F/L、恢复后开灯/投喂及读取恢复。09–12的英文标题、状态和长商品名已完整，英文4K标签裁边已修好；截图可见实际水箱与生物，不等于全游戏渲染/物理导航/性能通过。`07-zh-paused.png`实际仍为英文，不能作为中文或鼠标语言切换通过。`15-en-1280-loaded-paused.png`与1280日志另证明英文1280×800客户端的可见标题/状态和商品换行可读，F9已载入暂停、亮灯、14动物/90金币状态。中文切换、鼠标菜单、zoom、right-drag和glass仍未验收。

旧camera式`min(.45,312/Screen.width)`在3840宽为312/3840=8.125%，不是固定45%。build03的GUI.matrix缩放仍裁边，已由build04持久独立样式/identity matrix替代；旧失败证据保留。

本轮交付为安装与macOS候选的部分联调通过：474项纯C#核心、历史batch-07的41项Editor检查、当前build-05及arm64架构/本地adhoc签名完整性有通过证据；英文4K与1280窗口、部分键盘/存档流程已有分项实测。历史Play-03的19项smoke早于字体/UI修改，不作为最新UI回归；不写全面验收通过。

manual-player-01的锁屏及恢复属于历史。manual-player-02已实际启动build-04；CUA重置重连后坐标操作仍报noWindowsAvailable，Raise有SCStreamError -3812片段，AX click(0)返回成功，按键与截图可用；这不证明坐标点击送达。只读input-environment-diagnostic记录Player全窗口6、on-screen窗口0；CGSession字段和Guardian存在不足判定锁屏，也未证明坐标事件到达游戏。用户前台请求未获答复，本轮结束鼠标尝试；不能判定产品鼠标bug，也不能判其通过。中文切换、鼠标菜单、zoom、right-drag、glass待新的manual-player-03复验；截图07实际英文，不计中文通过。

manual-player-02/restore-verification.json与save-manifest确认恢复完成：primary、bak1..3、tmp五路径均回到测试前不存在，after保留primary及三份backup并逐一hash匹配，fixture未变；PID53279已退出且无Editor/Player。两份相关plist在清理前后整文件hash不变且无aquarium.language键，只能说明实际UI/日志为en，不能写成保存了显式en偏好或具有会话前prefs基线。最终player-1280-final.log及stdout已归档并核对hash；1280退出有native线程prematurely-finalized诊断，历史4K有IMK mach-port错误，均保留，不称Player全日志零错误。构建/Editor报告的0错误/0警告仅指各自报告范围。

Windows构建/机器、Steam发布、完整视觉性能及14项科学/发布pending未通过；仅en/zh-CN目录ready，9个planned与QA伪本地化不等于11语言翻译完成。后续按 `manual-player-02/next-manual03-retest.md` 使用新manual-player-03目录，重新stage已留存的05或06暂停档继续；最终输入结论见 `actual-input-outcome.md`。禁止复用已restored的manifest。
