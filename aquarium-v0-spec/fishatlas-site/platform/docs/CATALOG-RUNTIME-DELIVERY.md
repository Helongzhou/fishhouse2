# Catalog runtime delivery

Status: delivered locally on 2026-11-06. Production source frozen; database-free standalone service is running at http://127.0.0.1:4174/zh-CN.

## Runtime

- `src/data/catalog.json` is the sole catalog source, imported by `getPublicRelease`. No database or CMS fallback remains.
- Removed Payload admin/API routes, release mutation routes, CMS collection/components, build/activate/rollback modules, Payload config/generated types and retired integration/domain tests. `/admin` and its descendants now return a direct 410 response, avoiding a streamed locale not-found page with status 200.
- Removed Payload, PostgreSQL, GraphQL and their support dependencies; only Next, React, React DOM and Sharp remain in runtime dependencies. Frozen-lockfile install passes.
- Removed designer engine/storage/import/export modules and obsolete seed/calculator inputs. Browser IndexedDB/localStorage were not read, reset or deleted.
- Kept current-catalog read-only compatibility APIs. Requested IDs must match the file's current ID; unknown/historical IDs return 404. Template API is removed. API responses use `no-store`.
- Kept durable hashed media route and project-root path guard. All 75 baseline media references exist and match their filename hashes.
- Service manager retains detached standalone 4174 and 4173 redirect with PID identity checks. It no longer reads `.env` or starts a database.

## Recovery

Created before removal:

- `.runtime/retired-catalog-refocus/platform-before-refocus.tgz` (complete source/scripts/tests/package/config snapshot; mode 600).
- `.runtime/retired-catalog-refocus/qa-before-refocus.tgz` (retired QA source and evidence before removing three TypeScript tests that referenced deleted modules; screenshots and other evidence remain in place).
- `.runtime/backups/fishatlas-20261106-153624.dump` (custom-format PostgreSQL dump; `pg_restore -l` succeeds).
- `.runtime/backups/media-20261106-153624.tgz` (original and published media; mode 600).

Dedicated PostgreSQL cluster is `.runtime/postgres`, previously listening on localhost:55432. It was stopped with its own `pg_ctl -D .runtime/postgres stop -m fast`; `pg_ctl status` confirms no server running and the port is closed. Its data files, original `.env` and `.runtime/admin-credentials.txt` remain intact. No unrelated service, database or prototype was changed.

To inspect retired source, extract the tarball into a separate directory outside the active TypeScript project. Recover the database into a separate database using the matching PostgreSQL version and `pg_restore --no-owner`; do not overwrite the retained cluster.

## Final verification

- `pnpm install --frozen-lockfile`: passed; no Payload/PostgreSQL/GraphQL dependency remains in the lockfile or standalone runtime.
- `pnpm test`: 2/2 catalog content tests passed.
- `pnpm build`: passed after retiring three stale QA TypeScript tests; final route table contains only catalog/site, read-only compatibility APIs, media route and explicit admin retirement response.
- `pnpm typecheck`: passed.
- `python3 scripts/probe-catalog.py`: 58/58 checks passed against the final production service with PostgreSQL stopped. Evidence: `.runtime/evidence/catalog-runtime-http.json`.
- Checked eight zh-CN/en home/library/material/detail URLs, exact HTTP catalog equality with the JSON source, and the local-catalog deployment manifest.
- `/admin` and `/admin/login`: GET/POST/PATCH/DELETE return 410. CMS entity/user/GraphQL and old release mutation URLs return 404 for these methods. Old release IDs, unknown IDs and template resources return 404.
- All 75 baseline/current media references return 200 and match their content hashes. A new hash-named file created after process startup returned 200 with immutable cache headers; the temporary probe file was removed. Missing hash path returns 404.
- Port 4173 returns 308 to 4174 with the complete English library path/query preserved.
- Detached production PIDs at final handoff: web 54700; redirect 54701. Both registered processes remain running. The project PostgreSQL port 55432 is closed.

Browser/product acceptance is coordinated separately by the root reviewer; this report records runtime and HTTP evidence.


## Final UI follow-up cutover

After the UI owner froze quick-filter contrast/pressed-state and detail-summary/diet-label fixes, stopped both registered services before rebuilding `.next`, then ran `pnpm build`, `pnpm typecheck` and `service:start`; all passed. README build examples now use the same stop → build → start order.

Ten focused production HTTP checks passed for zh-CN/en home, fish library and neon-tetra detail, deployment ID, admin 410, retired release API 404 and 4173 redirect. PostgreSQL port 55432 remains closed. Both detached processes remain running at the PIDs above. Evidence: `.runtime/evidence/catalog-runtime-final-smoke.json`. Root and the independent reviewer were notified that the final UI is ready for focused browser acceptance.
