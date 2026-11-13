# FishAtlas 水族库

Next.js 16.4.5 / React 19.3.1 的双语水族资料站。目录的唯一运行时来源是 `src/data/catalog.json`；无需数据库、后台账户或 `.env`。前台包含水族生物、养护资料、材料信息和照片署名。

## 本地运行

需要 Node 24.22.1+（24.x）与 pnpm 11.25.0。当前机器：

```sh
export PATH="/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:$PATH"
PNPM=/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/bin/fallback/pnpm
"$PNPM" install --frozen-lockfile
"$PNPM" dev
```

[中文](http://127.0.0.1:4174/zh-CN) · [English](http://127.0.0.1:4174/en)。开发服务与生产服务共用 4174，启动前应先停止另一种服务。

## 目录更新

编辑 `src/data/catalog.json`，同时为新内容设置新的目录 `id`，运行验证并重新构建。保留稳定的条目 `id`、已有照片路径和双语字段。鱼类颗粒饲料直径是成年鱼的实用参考，底栖刮食鱼的薄片／饼粮单独说明。照片缺失继续明确显示缺失状态。

```sh
pnpm service:stop
pnpm typecheck
pnpm test
pnpm build
pnpm service:start
```

`/api/public/deployment`、`/api/public/releases/{当前id}/catalog`、`manifest` 和 `entities/{entityId}` 仅用于读取当前本地目录；未知目录 ID 返回 404。响应使用 `no-store`。旧模板 API、管理后台、CMS CRUD 和发布修改接口均已移除。

## 稳定生产预览

```sh
pnpm service:stop
pnpm build
pnpm service:start
pnpm service:status
# 生产服务运行、专用数据库停止时检查页面、退役接口与持久图片：
python3 scripts/probe-catalog.py
```

生产服务运行时先执行 `pnpm service:stop`，再构建并启动，避免构建替换正在使用的文件。需要停止预览时执行 `pnpm service:stop`。

服务脱离工具会话运行，仅绑定 localhost，PID 记录在 `.runtime/services.json`。日志为 `.runtime/web-production.log` 和 `.runtime/redirect-production.log`。4173 返回 308 跳转到 4174，保留路径与查询参数。停止命令只处理登记且身份匹配的本项目进程。机器重启后需重新启动。

`service:start` 自动设置绝对路径 `FISHATLAS_PROJECT_ROOT` 并连接持久 `public`、`media` 和静态构建文件；它不读取旧数据库凭据。`/media/releases/{sha256.ext}` 继续从持久 `public/media/releases` 读取、验证内容 hash，支持服务启动后新增的文件。已有图片 URL 保持可用。

## 退役内容与恢复

旧后台、发布系统、开缸设计领域实现、测试、脚本和配置已先压缩归档至 `.runtime/retired-catalog-refocus/platform-before-refocus.tgz`。归档内的 TypeScript 不参与当前编译。原数据库备份为 `.runtime/backups/fishatlas-20261106-153624.dump`，媒体备份为 `.runtime/backups/media-20261106-153624.tgz`；二者已验证可读取。

专用 PostgreSQL 数据目录 `.runtime/postgres`、`.env` 和 `.runtime/admin-credentials.txt` 保留在原处，数据库服务已停止。浏览器中曾保存的方案未删除。若需研究或恢复旧系统，请先将源码归档解压到单独目录，并在另一个数据库中使用同版 PostgreSQL 的 `pg_restore --no-owner`；不要覆盖当前目录或现有数据库。旧凭据、原始照片与数据库备份应按私密文件保管。

本次运行验证见 `docs/CATALOG-RUNTIME-DELIVERY.md`。
