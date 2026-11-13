# Unity 安装与实机联调记录

当前可用编辑器为官方 Hub 安装的 **Unity 6000.6.8f1 / ARM64**：完整签名、严格签名和 Gatekeeper 公证均通过，`-version` 实际调用返回 0，官方 CLI 许可状态为 active。**2022.3.62f3 仅保留下载与诊断，未安装/启动**。`unity` 保留 2022.3.62f3 / URP14.0.11 基线；实际已进行核心、Editor batch、Play smoke与macOS构建验收的候选为独立 `unity6-validation`（Unity 6000.6.8f1 / URP17.6.0）。独立应用、人工操作与发布状态见下文，不混用两份工程的结论。

## 安装

2026-11-05。本轮基线原为 Unity 2022.3.62f1；查询官方安全公告后，负责人已冻结同分支最小安全补丁 **2022.3.62f3（96770f904ca7，ARM64）** / URP **14.0.11**。当时决定不安装有警示的旧编辑器、先保持2022同分支；后续f3遭Gatekeeper拒绝，负责人改为保留2022基线，并用用户Hub正常安装的Unity6作独立联调。项目版本由集成负责人同步。安装开始时主机为 macOS 27.0（26A425）、Apple Silicon arm64，可用磁盘约 252 GiB；未发现现有 Unity Hub/Editor 安装或标准位置许可证文件，未读取/输出任何许可证内容、账号凭据或令牌。

官方来源：

- [Unity 官方发行 API](https://services.api.unity.com/unity/editor/release/v1/releases?version=2022.3.62f1&architecture=ARM64&platform=MAC_OS)：revision `4af31df58517`，发行日 2025-05-07，架构 ARM64。
- [Unity 2022.3.62f1 Apple Silicon 官方安装包](https://download.unity3d.com/download_unity/4af31df58517/MacEditorInstallerArm64/Unity-2022.3.62f1.pkg)：官方元数据大小 4,504,607,345 字节。
- [Unity Hub ARM64 官方镜像](https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup-arm64.dmg)：本轮下载大小 166,058,559 字节。

安装缓存位于 `/Users/zhouhelong/Library/Caches/CodexUnityInstall/`。后续实际下载、签名核验、安装路径与授权结果记录于以下各节；下载、安装、激活与项目运行分开记录。

官方发行 API 当前将 2022.3.62f1 标记为 **Security Alert**，说明此版本构建的应用有已知安全问题，并要求发布前更新；支持已于 2026-05-08 结束。见 [Unity 官方安全公告](https://unity.com/security/sept-2025-01)。该风险触发了本轮 62f1→62f3 安全补丁升级，不等同宣称整个 2022.3 分支仍在支持期，也不代替真实构建与发布验收。

### 安全补丁与系统要求

[Unity 安全公告](https://unity.com/security/sept-2025-01)列出 CVE-2025-59489，2022.3 LTS 的首个修复版本为 2022.3.62f2；公开发行 API 当前最新可用 Apple Silicon 补丁为 2022.3.62f3（2025-10-28），其 label 为空，62f1 为 Security Alert。另有公告列出的 xLTS 修复版本，未将其当作本机已具备授权/下载资格的版本。

- [62f3 官方 ARM64 元数据](https://services.api.unity.com/unity/editor/release/v1/releases?version=2022.3.62f3&architecture=ARM64&platform=MAC_OS)。
- [62f3 官方安装包](https://download.unity3d.com/download_unity/96770f904ca7/MacEditorInstallerArm64/Unity-2022.3.62f3.pkg)，4,504,652,441 字节，官方 MD5 `2d86c2d8f47e12a8900d089645f7408c`。
- [62f3 官方发行说明](https://storage.googleapis.com/live-platform-resources-prd/templates/assets/2022_3_62f3_d577a0b614/2022_3_62f3_d577a0b614.md)。
- [Unity 2022.3 系统要求](https://docs.unity3d.com/2022.3/Documentation/Manual/system-requirements.html)：Apple M1 或更新、macOS Big Sur 11 起、Metal，并要求 Apple Silicon 上安装 Rosetta 2。文档未明确认证本机 macOS 27，因此兼容性须本机运行验证；不能从最低版本要求推断 macOS 27 已受官方测试。

Rosetta 2 初查未安装；已按负责人授权运行 Apple 官方 `softwareupdate --install-rosetta --agree-to-license`，结果成功，无管理员交互。安装 receipt 为 `com.apple.pkg.RosettaUpdateAuto`，版本 `1.0.0.0.1787810290`。

### Hub 实际安装与核验

Unity Hub **3.22.5 / arm64** 已安装到 `/Applications/Unity Hub.app`。官方 DMG 的 `hdiutil verify` 成功；完整应用 `codesign --verify --deep --strict` 成功，签名为 `Developer ID Application: Unity Technologies SF (9QW8UQUTAA)`，TeamIdentifier `9QW8UQUTAA`；`spctl --assess --type execute` 为 accepted / Notarized Developer ID。原生主可执行文件由 `file` 确认为 Mach-O arm64。

Hub headless `help` 可正常调用，`editors --installed --json` 初次输出 `[]`。账号和许可证 GUI 由主负责人协调，安装 agent 未登录账号、代填密码、声明 Personal 营收资格或请求购买。

## 实机联调

2026-11-06更新。报告位于 `verification/unity/unity6-validation/`；当前冻结产物为 `unity6-macos-build-04`。

| 层次 | 当前结果 |
|---|---|
| 纯C#核心 | source-regression-01：Core构建零警告/错误，49行为+42兼容+287生态+47玩法+49集成，共474项通过，30项输入hash当前一致。 |
| 当前Editor batch | batch-07：41/41、exit0、0错误/0警告；真实Unity6000.6.8f1/URP17.6.0、随包字体Resources加载、JsonUtility/完整路线/备份通过。8项记录源hash当前一致。 |
| 历史Editor Play | play-03：19/19、exit0、errors为空；实际Awake/初始Update、生产session、22实体/相机/collider与低帧推进通过；版本早于字体/UI修复。 |
| 当前macOS构建 | build-04：Succeeded、exit0、Mono2x/ARM64/None，0错误/0警告、93,129,825字节、3.375545秒；8项记录源hash当前一致。 |
| 当前封装 | 二进制hash与architecture.json一致，主程序/UnityPlayer直接lipo均arm64；普通可读OFL/NOTICE与官方来源/项目hash匹配，当前codesign deep/strict再验exit0；adhoc、无TeamIdentifier，非发布签名或公证。 |
| 实际app | manual-player-02已启动，键盘/存档及英文4K已有分项证据；英文1280窗口分项已实测；中文、鼠标路径与完整视觉性能待复验。 |
| Windows / Steam | 未安装Windows支持，无Windows构建/机器或Steam发布验收。 |

原失败历史保留：batch-01真实null inline底砂失败后用U04显式存在性/schema2修复；batch-02/03浮点JSON文本比较失败后使用已批准的类型化比较，连续double容差 `1e-12 * max(1, abs(a), abs(b))`，离散值/结构/ID/RNG/整数账本严格。Play-01/02各17/18失败于Search启动索引越界；正常启动/索引ready gate后Play-03通过，未删除索引或屏蔽错误。旧报告不认证当前字体/UI源码。

原unity保留2022产品基线，初始42项准备hash中仅Validation验收器后来改变。migration-source-manifest的not_run是历史准备状态。随包Noto字体来源、真实OFL许可、Include Font Data及共享资产清理已核对；详情见[Unity6迁移联调说明](Unity6迁移联调说明.md)。

`manual-player-02/analysis-01`对已记录快照作只读审计：五项状态检查通过。01/02/03暂停快照完整字节一致，时钟、钱包90、8 Ember+6虾、12份食物及23张receipt均不变；04只增加一次开灯receipt；05只增加一次12份投喂receipt、外部氮输入1.44 mgN。04→05期间累计13餐/摄入1.56 mgN包含原有食物，不能全部归因于新投喂；LastFoodId是感知标记，不是摄入归属账本。05→06恢复比较2258个严格值、1433个decimal值，无业务/结构/ID/RNG/整数差异，80处codec差异最大4e-15，均在批准容差内。7份快照与4张截图共11项hash匹配；当前分析脚本后来修改，报告保留的executed-analyzer.py hash与当时执行记录一致。

负责人记录的Space、F/L、F5/F9操作时间线与09–12截图支持暂停冻结、暂停拒绝F/L、恢复后开灯/投喂及读取恢复。09–12的英文标题、状态和长商品名已完整，英文4K标签裁边已修好；截图可见实际水箱与生物，不等于全游戏渲染/物理导航/性能通过。`07-zh-paused.png`实际仍为英文，不能作为中文或鼠标语言切换通过。`15-en-1280-loaded-paused.png`与1280日志另证明英文1280×800客户端的可见标题/状态和商品换行可读，F9已载入暂停、亮灯、14动物/90金币状态。中文切换、鼠标菜单、zoom、right-drag和glass仍未验收。

## 62f3 安装包与签名布局诊断

编辑器官方安装包已下载完整：4,504,652,441 字节，8 路官方 HTTP Range 下载共 178.6 秒（约 24 MiB/s）；合并后官方 MD5 `2d86c2d8f47e12a8900d089645f7408c` 一致，另算 SHA-256 `0a17d62dd24d120d562385901de393d5b88f3d849b75365b5cdb0ccffa78c01b`。

- 包路径：`/Users/zhouhelong/Library/Caches/CodexUnityInstall/2022.3.62f3/Unity-2022.3.62f3-arm64.pkg`。
- `pkgutil --check-signature`：可信 Developer ID Installer 签名链，`Unity Technologies SF (9QW8UQUTAA)`，可信签名时间 2025-10-24。
- `spctl --assess --type install` 对该 pkg 返回 **rejected / Unnotarized Developer ID**；`stapler validate` 表明包未附 stapled ticket。
- 只读理解官方 Distribution/PackageInfo：单一 Payload，50,204 文件，目标 `/Applications/Unity`，auth Root。官方前后安装脚本主要做旧安装移除、属主/权限、旧 WebKit 类 framework 链接兼容；未执行任何脚本，未删除旧安装，也未修改系统安全配置。

使用 Apple `pkgutil --expand-full` 在缓存目录完整展开，内层 `Unity.app` 原生 arm64，Info.plist 版本 2022.3.62f3，Developer ID Application 为同一 Unity 团队，时间戳 2025-10-24。逐一检查 CodeResources 的 **46,510 个 SHA-256 资源全部匹配**，无缺失；35 个嵌套签名代码均通过单独正常验证。

初次外层验证报 No such file or directory，定位到官方 `Contents/Documentation → ../../Documentation` 链接，默认独立 Editor 包未包含目标目录。仅在 bundle 外重建对应正常目录后，普通 `codesign --verify --deep` 通过；`--strict` 则明确返回 `invalid destination for symbolic link in bundle`，指向该官方外部文档链接。未修改、删除或重签任何 bundle 内签名内容。完整资源验证与严格策略结果分别记录，不能将两者混写为全面通过。

完成布局诊断后，`spctl --assess --type execute` 对内层完整 app 仍为 **rejected / Unnotarized Developer ID**，app 也未附 stapled ticket。因此目前 **62f3 尚未安装到 Applications、未注册 Hub、未运行 Editor 或项目**。没有清隔离属性、关闭 Gatekeeper、修改 spctl 策略、去掉官方链接或强行执行遭拒对象。

[Unity 官方命令行安装文档](https://docs.unity3d.com/2022.3/Documentation/Manual/InstallingUnity.html)给出的正常 macOS 安装方式是用户通过管理员身份执行 `sudo installer -package Unity.pkg -target /`，或使用 Hub 官方 Install Editor 流程。后续由主负责人协调用户完成必要的正常管理员/系统安全交互；安装 agent 不代填密码、不默默绕过系统评估。

主负责人报告 Hub 中另有 Unity 6000.6.8f1 Silicon 默认安装进行中；本 agent 未取消、改写或清理其下载/进程。只读备选查询发现公开 API 当前最新 Unity 6 LTS 为 **6000.3.28f1 / 8f082d2fe476**（2026-11-03，ARM64，label 为空）；6000.6.8f1 为 Supported 更新版，不能标成 LTS。[Unity 6.3 系统要求](https://docs.unity3d.com/6000.3/Documentation/Manual/system-requirements.html)列 Ventura 13+、M1+、Metal 和 Rosetta 2，也未明确认证 macOS 27。未为备选重复下载或升级工程。


## Unity 6.6 官方安装完成与交接

用户/Hub 默认流程先行启动了 6000.6.8f1 Silicon 安装。负责人决定保持 2022 基线，在独立 `unity6-validation` 副本适配当前受支持编辑器；未强行运行遭 Gatekeeper 拒绝的旧 f3。

事实来源为 Hub 安装日志与已安装应用本身：

- 版本 **6000.6.8f1**，revision **237b6593792b**，原生 **arm64**；官方 API stream 为 **SUPPORTED**，不是 LTS。
- 官方来源：[6000.6.8f1 ARM64 发行 API](https://services.api.unity.com/unity/editor/release/v1/releases?version=6000.6.8f1&architecture=ARM64&platform=MAC_OS)，[编辑器官方安装包](https://download.unity3d.com/download_unity/237b6593792b/MacEditorInstallerArm64/Unity-6000.6.8f1.pkg)。元数据为 4,999,319,010 字节。
- Hub 在 2026-11-05 19:25:35 完成下载；19:25:46 记录 Checksum check: Passed；19:26:07 记录 Editor is successfully installed / install_finished（以上为 Asia/Shanghai）。
- 应用：`/Applications/Unity/Hub/Editor/6000.6.8f1/Unity.app`。
- **可调用编辑器**：`/Applications/Unity/Hub/Editor/6000.6.8f1/Unity.app/Contents/MacOS/Unity`。
- `file` 确认 Mach-O 64-bit executable arm64；Info.plist 的 CFBundleVersion 为 6000.6.8f1。
- `codesign --verify --deep` 返回 **0**；`codesign --verify --deep --strict` 返回 **0**。
- 签名：`Developer ID Application: Unity Technologies SF (9QW8UQUTAA)`，可信时间戳 2026-11-02；`spctl --assess --type execute` 为 **accepted / Notarized Developer ID**。与旧 f3 的系统评估结果分别记录，没有清除隔离或关闭安全检查。
- 实际运行该可执行文件的 `-version -batchmode -nographics -quit` 返回 **0**，输出匹配 **6000.6.8f1**；未指定或打开任何项目。
- Hub 自带官方 Unity CLI 1.0.0-beta.13 的 `license status --json --non-interactive` 返回 **0**、`success=true`、`active=true`。仅提取状态，未输出、记录或展示许可证内容、账号凭据、许可 ID 或令牌；未替用户声明 Personal 资格。
- WebGL 模块于 19:26:24 记录成功安装；文档模块由 Hub 继续管理。安装目录和模块归官方 Hub 管理，本 agent 未取消、清理或篡改其安装文件。

[Unity 6.6 系统要求](https://docs.unity3d.com/6000.6/Documentation/Manual/system-requirements.html)列 macOS Ventura 13+、Apple M1+、Metal；Rosetta 2 用于 Progressive CPU Lightmapper / Realtime GI 等功能。文档没有明确列出 macOS 27 认证，故在安装交接时只能确认本机版本启动、签名与许可已实测；之后的项目导入、Play结果见上方实机联调，人工图形和构建仍各自验证。

安装 agent 的版本调用与许可核验均已完成并退出；交接时未见 Editor 进程，也从未打开项目。已将上述绝对路径和有效许可状态交给主负责人、玩法与集成 Sol；之后停止一切可能启动 Editor 的操作，玩法 Sol 按负责人安排独占 `unity6-validation` 的首次 batch 导入/编译。不要把“安装与版本调用通过”写成项目导入、Play 或构建已通过。

## 已安装构建支持（只读核对）

2026-11-05，玩法 Sol 独占 batch-04 期间，安装 agent 只读检查目录、Hub modules.json 和本机 Xcode；未启动 Editor、打开项目、安装模块或运行构建。

6000.6.8f1 的构建引擎目录位于 `/Applications/Unity/Hub/Editor/6000.6.8f1/PlaybackEngines/`，不在 `Unity.app/Contents/PlaybackEngines`。

| 目标/后端 | 实际状态 | 证据 |
|---|---|---|
| macOS Mono / ARM64 | 已存在 | MacStandaloneSupport/Variations 中 development、nondevelopment 两类 arm64 Mono player；file 确认为原生 Mach-O arm64 |
| macOS Mono / Intel x64 | 已存在 | 两类 x64 Mono player |
| macOS Mono / Universal | 已存在 | 两类 x64arm64 Mono player；file 确认为 x86_64+arm64 |
| macOS IL2CPP | 未安装 | modules.json 的 mac-il2cpp selected=false；无 IL2CPP player variation |
| Windows Mono | 未安装 | windows-mono selected=false；WindowsStandaloneSupport 目录不存在 |
| Windows Server | 未安装 | windows-server selected=false；同上 |
| WebGL | 已安装 | webgl selected=true，WebGLSupport 目录存在，Hub 安装成功日志已记录 |

本机已有 `/Applications/Xcode.app/Contents/Developer`，`xcrun --sdk macosx --show-sdk-version` 返回 27.2，clang 21.0.0 可调用。Mono 构建不需要另行补装 Mac IL2CPP，也不以 Xcode 是否存在代替缺失的 IL2CPP player 模块。

因此，本轮具备尝试产出本机 `.app` 的已安装模块条件。当前独立Unity6候选的 `BuildMacOS()` 明确选择 `BuildTarget.StandaloneOSX`、`ScriptingImplementation.Mono2x`、Apple Silicon/ARM64和 `BuildOptions.None`；直接输出 `.app`，不生成Xcode工程，并保存BuildReport摘要与构建消息。当前入口并非Development Build；首次macos-build-01已按这些设置成功构建，当前冻结产物为build-04。ARM64是本机最小目标；Universal虽有模板，不是本轮必要条件。

[Unity 6.6 macOS 构建设置](https://docs.unity3d.com/6000.6/Documentation/Manual/macosbuildsettings.html)与已安装手册列Apple Silicon / Intel / Universal；构建选项本身不等于商店发行就绪。当前build-04的BuildReport、架构和签名证据见上表。构建后将OFL及NOTICE复制到普通可读ThirdPartyNotices目录，再刷新外层adhoc seal并deep/strict核验；该本地签名不等于Developer ID发行签名或公证。

负责代码的Sol已在 `unity6-validation/Assets/_Game/Editor/AquariumProjectSetup.cs` 增加独立 `BuildMacOS()` 入口，并保存可复现补丁 `tools/unity6_patches/macos-arm64-mono-build.patch`；安装/审计agent只核对未修改产品代码。已有 `BuildWindows()` 不代表Windows支持已安装或构建通过。没有 Windows 模块、没有实际 Windows 构建或 Windows 机器验证，因此本轮不得标成 Windows 可运行或 Steam 已就绪。

## 本轮交付状态

本轮交付为安装与macOS候选的部分联调通过：474项纯C#核心、当前batch-07的41项Editor检查、当前build-04及arm64架构/本地adhoc签名完整性有通过证据；英文4K与1280窗口、部分键盘/存档流程已有分项实测。历史Play-03的19项smoke早于字体/UI修改，不作为最新UI回归；不写全面验收通过。

manual-player-01的锁屏及恢复属于历史。manual-player-02已实际启动build-04；CUA重置重连后坐标操作仍报noWindowsAvailable，Raise有SCStreamError -3812片段，AX click(0)返回成功，按键与截图可用；这不证明坐标点击送达。只读input-environment-diagnostic记录Player全窗口6、on-screen窗口0；CGSession字段和Guardian存在不足判定锁屏，也未证明坐标事件到达游戏。用户前台请求未获答复，本轮结束鼠标尝试；不能判定产品鼠标bug，也不能判其通过。中文切换、鼠标菜单、zoom、right-drag、glass待新的manual-player-03复验；截图07实际英文，不计中文通过。

manual-player-02/restore-verification.json与save-manifest确认恢复完成：primary、bak1..3、tmp五路径均回到测试前不存在，after保留primary及三份backup并逐一hash匹配，fixture未变；PID53279已退出且无Editor/Player。两份相关plist在清理前后整文件hash不变且无aquarium.language键，只能说明实际UI/日志为en，不能写成保存了显式en偏好或具有会话前prefs基线。最终player-1280-final.log及stdout已归档并核对hash；1280退出有native线程prematurely-finalized诊断，历史4K有IMK mach-port错误，均保留，不称Player全日志零错误。构建/Editor报告的0错误/0警告仅指各自报告范围。

Windows构建/机器、Steam发布、完整视觉性能及14项科学/发布pending未通过；仅en/zh-CN目录ready，9个planned与QA伪本地化不等于11语言翻译完成。后续按 `manual-player-02/next-manual03-retest.md` 使用新manual-player-03目录，重新stage已留存的05或06暂停档继续；最终输入结论见 `actual-input-outcome.md`。禁止复用已restored的manifest。
