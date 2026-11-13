# Independent acceptance review — 2026-11-06

结论：本轮发现的导入守恒与缺译 CSV 缺陷已修复并经独立反例复验通过。可继续内部 local-preview；不能宣布正式发布。最终限定复审在 **production PID22656** 的完整独立测试 **7/7**、新增 runtime 测试 **1/1**、`tsc --noEmit` 通过；活动发布为 `r-1793947213469-5b5275e195`。未修改产品代码、现有 CMS 对象或活动发布；实测产生失败发布作业、未激活候选及一个明确 QA 标记且未绑定条目的 placeholder 媒体。

## Production runtime 复审补充（通过）

根在真实 production 后台发现候选构建 ENOENT：standalone 的 traced public 只有部分文件，且素材读写依赖 standalone cwd。此前 dev 的 7/7 没有覆盖该故障。后端修复后，本独立 QA 已重新对 **production4174 / PID22656** 跑完整 7 项（`production-suite.tap`），全部通过。真实新候选 `r-1793948428129-99069f0505` 构建 202 → preview 200、匿名仍 404，整个实测未激活，active 未变。

新增 `runtime.test.ts` 1/1（`production-runtime.tap`）同时验证：

- `getProjectRoot/publicPath/mediaPath` 使用绝对 `FISHATLAS_PROJECT_ROOT`；standalone 无配置时 fail-fast，拒绝相对配置与逃出目录路径。
- stage/freeze/integrity/Media.staticDir 共用持久路径；Media.staticDir 指向 platform/media，standalone public 与 media 实际 symlink 指向持久根。
- service.py 对既存 traced public/media 先备份再链接，不再被 exists=true 的局部目录阻断。
- 受控动态 `/media/releases/[filename]` route 验证 hash 文件名与实际内容 hash；用于 production 启动后新文件，避免 Next 启动时静态文件枚举造成 404。
- 经真实 production CMS 上传新图（13×17 PNG 测试色块，**placeholder，未绑定任何实体，不计为物种照片**）：media ID `1`，文件 `qa-runtime-probe-69435447-a9f8-4c36-893d-ae0847ab4b89.png`。源文件实在 `platform/media/`，由实际 stage/freeze 函数生成持久 `platform/public/media/releases/d6790e423b665410d0d834f2d9d74e502d4c4885f2da1b6f73f950b2c0025cd7.png`。
- **不重启服务**，新冻结文件 HEAD/GET 均 200，HTTP bytes SHA-256 与源文件和冻结文件均为 `d6790e423b665410d0d834f2d9d74e502d4c4885f2da1b6f73f950b2c0025cd7`；活动 deployment 前后完全一致。

测试媒体身份、实际源/输出绝对路径与 HTTP 结果在 `runtime-upload.json`、`runtime-results.json`；保留该明确标记的孤立测试素材用于追踪，未冒充正式照片，未修改活动内容。运行时目录缺陷经独立实测关闭；外部生产基础设施与持久卷部署仍未验收。

## 最终限定复审

- **单语 CSV：关闭，独立测试通过。** 统一 `entityName/entityUnit/localizedText/templateName` 仅读取请求语种且受 publishedLocales 约束。缺译明确提示并保留学名/稳定 ID、数量；独立测试还放入被排除的英文草稿 sentinel，确认 CSV 不泄漏。`Partial<Record<Locale,...>>` 与实际契约一致，独立测试已用存在性断言缩窄，类型检查通过。
- **设计器缺译：源码复审关闭。** 清单、选中项目、对象说明使用统一 entityName，新增目录/模板按 locale 发布状态过滤；保留历史方案身份与实例。未以真实浏览器注入单语发布验证，因此不将其标作独立浏览器通过。
- **场景素材 404：源码复审关闭。** `SceneImage` 为非空 URL 添加 onError，失败后生成相同 width/height 的矩形和名称；外层对象 ID、选择与逻辑数据保留。未独立做浏览器 404 注入。
- **CMS 表单：源码复审关闭。** Discovery 仍以结构化 JSON 存储，但实际 Field 已替换为选择框、复选框、分布/分类输入、双语说明及注意事项业务控件，修改时保留未编辑数据。实体列表有中文名称 Cell，listSearchableFields 包含中英名称、学名和 ID。不能因底层 JSON 存储误判仍是原始 JSON 编辑器。
- **CMS 候选预览：源码已确认。** ReleasePanel 加载受保护 preview API，显示本批目录变更、模板选择、只读双视图和实际 evaluatePlan 结果，尚未激活。已独立在线验证 preview API：匿名 401、认证 200、Cache-Control:no-store、候选 release ID 与 base ID 正确、changes 为数组（同内容目录 diff=0）；候选仍未公开。
- **浏览器边界。** 本独立会话尝试通过 CUA 打开 iab 与库存 browser ID 均返回 `Browser is not available`。因此上述 UI 修复仅计源码/类型验证，不冒充浏览器交互与视觉验收。

## 已发现并复验关闭

- **跨类型节点绕过数量守恒：已关闭。** 原实现允许 1 个底床项目额外挂普通 `objects`，也允许动物项目被 `substrateLayers` 引用，导入仍为 verified。两个独立反例在修复前失败，domain 修复后保持原断言重新执行通过。现在 `objects` 仅能引用非 layer，`substrateLayers` 仅能引用 layer。未改动独立断言来迁就实现。

## 独立证据

执行命令（在 `platform/`）：

```sh
env PATH=/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:$PATH ./node_modules/.bin/tsx --test qa-artifacts/independent/independent.test.ts
```

- `independent.test.ts` 是本审查新写的反例与真实 HTTP 测试，不依赖实现者的测试结论。dev 阶段结果在 `test-results.tap`，最终 production 阶段在 `production-suite.tap`；最新 production HTTP 汇总在 `live-results.json`，runtime 单列于上述文件。
- 未核实内容/规则/素材版本即便自行声称 verified，导入后仍 unverified，评价只返回 unknown；待确认温度与溶氧不会形成 pass。
- 同 release 的养护参数、规则条件、成体尺寸、重复 ID、数量和外部素材 URL 篡改均被拒绝，原方案不变。
- 12 鱼 + 6 虾 + 2 螺保持 20 个唯一实例；添加坡度底床后底部物体按所在深度定位，复制/删除/撤销与导入往返保持数量和身份。
- 匿名实体读取/创建、素材上传、模板/规则创建均 403；发布列表/创建/激活/回滚均 401。维护者登录使用私有凭据文件，未输出凭据或 token。
- 登录后的跨来源发布 POST 被 403 拒绝。
- 正式 R2 候选构建返回 422，最新活动发布实测照片阻塞为 `aquasoil`、`inert-substrate`、`java-moss`；没有伪造图片或绕开正式门槛。
- 真实 CMS 已有文档只读获取后，用真实编译函数独立验证：中文 approved / 英文 draft 时不泄漏英文 sentinel，沿用上次已发布英文；首次发布只含中文；共用核心改动而英文仍草稿时拒绝发布。
- 真实新建 local-preview 候选对匿名 public API 返回 404；错误 expected base 的 activate 和 rollback 均 409；失败激活后候选继续 404；整个实测前后 deployment 深度一致，活动发布未改变。

## 素材复核与验收边界

- `harlequin-rasbora.jpg` 已查看原尺寸图像，深体、尾柄黑色三角外形与 `Trigonostigma heteromorpha` 一致，元数据也对应；没有足够证据认定错图，不列缺陷。
- 已审阅 `media-provenance.json`。公共鸣谢页正在由前端补齐作者、原页和许可链接；这不等于强制 CMS 来源/许可录入流程。
- 根已确认原 Javamoos 来源学名为 Vesicularia dubyana，与本条目 Taxiphyllum barbieri 不符，domain 已撤 java-moss verified 绑定。最新正确内容集应为 23/26 张合格展示图，正式照片缺口为 aquasoil、inert-substrate、java-moss；旧活动发布曾为 24/26，不将错误绑定继续计作合格图片。受控同步后独立实测正式构建返回 422，严格匹配上述三项缺图；active manifest 仍为 local-preview。
- 未进行真机、5 位爱好者试用、外部部署或生产基础设施验收。没有把模拟浏览器或源码审查计作这些门槛通过。
- 本轮没有成功切换活动 release 的权限使用；成功发布与完整回滚的实现者集成报告可作为其他证据，本报告仅独立确认了匿名隔离、候选隔离、CAS 拒绝及失败不改变活动版本。
