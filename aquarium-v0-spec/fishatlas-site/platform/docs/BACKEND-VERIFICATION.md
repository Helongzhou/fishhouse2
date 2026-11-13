# 本地后端验证记录

2026-11-06，Node 24.22.1 / pnpm 11.25.0 / PostgreSQL 16.15。已实际查询 npm metadata 并安装锁定 Payload / @payloadcms/next / db-postgres / storage-s3 3.93.0、Next 16.4.5、React/ReactDOM 19.3.1。Payload peer 的 Next >=16.3.3 <17 与运行时要求满足。依赖锁位于 pnpm-lock.yaml。

当前运行地址 http://127.0.0.1:4174，CMS http://127.0.0.1:4174/admin。维护者凭证仅 `.runtime/admin-credentials.txt`，权限600；未记入报告或工具输出。

当前 active 为 `r-1793947213469-5b5275e195`，catalog SHA-256 `ced0cfc6fb29c4432d9a6f3d7b820e88ca0dd054a56d7a81394bbb94db018938`，`engine-1`，designer=true，quality=local-preview。旧失败实现的快照保留历史；新建方案按引擎与模板身份门槛拒绝不兼容版本。

## 已执行

- `pnpm typecheck`：通过。
- `pnpm test`：29领域/存储测试通过。
- `pnpm test:backend`：10真实Postgres/Payload测试通过，含JSONB hash往返、不可变触发器、类型值0/false、分类环、语言草稿隔离/首发单locale、公开候选拒绝、幂等、CAS冲突、完整回退、素材故障、最终模板identity与engine实际运行、核心类型门、候选差异及 standalone 持久目录回归。
- `pnpm probe:cms`：真实HTTP匿名GET entities/users/media均403，GET releases401；匿名写entities403/releases401；维护者login200、鉴权26实体、跨Origin写403。固定release catalog返回一年immutable缓存。脚本不输出密码和会话token。
- 实际备份恢复：pg_dump与媒体归档，隔离fishatlas_restore_probe库pg_restore成功；验证26实体、原active、16历史快照后删除临时库，主库保持。证据 `.runtime/evidence/restore-probe.json`；该数量是当次备份时的历史数量。

CMS文档的草稿与approved语言状态是两个独立门槛；发布器仅读取已发布文档，未公开语言不会漏出主译文或养护说明中的语言草稿。单SSR请求通过React cache固定一次release，noStore保证新请求读取active。

R1候选不校验尚未启用的designer模板/引擎；R2在最终编译快照上逐模板createPlan、核对templateId并运行evaluatePlan。模板/规则ID来自CMS稳定entityId，执行引擎版本来自代码常量。照片、双视图复制到hash文件名并在激活前重新校验；快照在Postgres禁止update/delete，唯一active条件更新与审计事件同事务提交。失败候选不会切换active。

## 尚未宣称通过

23张真实对象照片已绑定，惰性底床、水草泥与爪哇莫丝三张真实照片仍缺，严格照片门保留阻塞。本地预览不能作为正式发布。真实手机、性能、5人试用和人工素材核验由整体验收另行记录。

当前是localhost单进程、持久本地文件夹；配置了可选S3媒体adapter，但未开通或测试真实云对象存储。发布素材编译路径当前使用本地原件/公开目录；生产S3原件读取与发布对象上传需要部署环境适配。小规模构建任务状态持久化，但没有独立跨进程worker队列。没有公开部署或生产基础设施配置。

## 最终生产复验

生产构建成功；独立 QA 的最后 live7/7 通过。独立后台进程运行 standalone，旧4173入口的308跳转实际保留完整路径与query。生产 HTTP 探针再次确认匿名拒绝、维护者登录、中文列表搜索以及受保护候选预览。证据为 `.runtime/evidence/cms-http-probe.json` 与 `cms-preview-probe.json`。

CMS 原始检索资料已改表单控件，本轮无需数据库 schema 变更。候选面板提供真实变更表、只读 Scene 双视图和 domain.createPlan/evaluatePlan 结果，无本地方案存储依赖。

## 生产发布路径回归

人工在真实 production CMS 构建候选时发现 cwd 变成 `.next/standalone`，原始场景读取失败。现由 `FISHATLAS_PROJECT_ROOT` 显式指定持久根目录，发布读源/写目标、素材 hash 校验与新上传原件统一使用 `runtime-paths.ts`；standalone 目录无显式根配置时立即报错。受控动态 hash 素材路由支持启动后新增素材，避免 Next 启动时枚举 public 文件导致新文件404。

重新构建并启动后，真实 production `POST /api/admin/releases` 返回202，候选 `r-1793948385916-99069f0505` 的受保护 preview 返回200、26条目/2模板，匿名公开请求404，active前后保持 `r-1793947213469-5b5275e195`。证据 `.runtime/evidence/production-candidate-post.json`。运行服务 PID22656，旧入口跳转 PID22657。

根验收在真实 production CMS 中构建候选 `r-1793948495882-99069f0505`，实际确认变更摘要、两模板切换、正俯双视图与规则列表；未激活。截图 `qa-artifacts/root/cms-production-preview.jpg`。

独立 QA 最终 production 原有7 suites及新增 runtime suite全部通过。真实上传一张未绑定条目的测试占位图，stage/freeze 后生成全新 hash，无重启即可 HEAD/GET 200且内容 SHA-256一致；active保持不变。绝对根配置、standalone缺配置立即失败、Media.staticDir与持久目录符号链接均验证通过。证据 `qa-artifacts/independent/production-suite.tap`、`production-runtime.tap`、`runtime-results.json` 与 `runtime-upload.json`。测试占位图未作为条目照片使用，不计入23张真实照片。
