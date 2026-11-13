# Tripo sample 01 preflight

2026-11-09: 11 real CLI offline dry-runs passed. All use explicit `P2-20260801`, one text-to-model step, one candidate, texture=true, texture_quality=standard, pbr=false, no uploads, no processing chain, native GLB. Every job estimates **110 credits**, total **1210 credits**, warnings/errors empty. Plans are `art/tripo-sample-01/plans/<asset_id>.json`; normalized API bodies live in `result.steps[].payload`. Exact argv, manifest hash and plan hashes are in `preflight-summary.json`.

P2 supports text generation; triangle face limits 48–50000, quad 48–25000. These plans use 4000–10000 triangles. CLI defaults PBR off and P2 even without a model option, but both are explicit here. No preset is used because game presets can append paid conversion. Generation has no documented arbitrary FBX format option; quad forces FBX but changes topology. Use GLB and an existing Unity glTF importer or local Blender conversion to FBX. No API conversion was added.

1210 is the bundled official list-price generation estimate, not a live account quotation. It excludes rigging, retargeted animations, paid conversion, rerolls, additional candidates, and all local production/import work. Offline validity does not guarantee server acceptance or model quality. No paid task was submitted by this preflight. Existing known balance is zero; no login, top-up or key access was performed here.

From repository root:

```sh
python3 tools/tripo_sample.py prepare
```

Only after the human explicitly confirms spending this plan and tops up, a future run can use:

```sh
python3 tools/tripo_sample.py execute --execute-paid --confirmed-credit-cap 1210
```

Do not run that command this round. `execute` checks balance before every asset; no automatic retry, redo, model substitution or batching. It records an uncertain-submission state before the CLI starts, then retains diagnostics, task ID, actual credits and SHA-256 hashes on success. Already-successful jobs are skipped; all other prior submissions stop the run for human diagnosis. The budget cap is a local estimate gate, not an API-enforced billing reservation.

On exit 4 / zero balance: stop and top up only with human direction. On timeout or network failure, do not resend `make`: a paid task may exist. Read `execution-state.json` and `<asset_id>-execution.json`. With a known task ID, use the CLI's blocking watcher or fetch artifacts, not a new generation:

```sh
TRIPO_CLIENT_APP=codex tripo task watch <task_id> --download --json
TRIPO_CLIENT_APP=codex tripo task get <task_id> --download --json
```

With no task ID, inspect CLI history/account task list and balance before any deliberate resubmission. The runner intentionally refuses to clear an uncertain state. A recovered task must be verified and reconciled into the state under human supervision; do not delete state to bypass it. An interrupted process may leave `execute.lock`: remove only after confirming no runner is active; retained per-job state still prevents duplicate submission.

Validation: `python3 -m py_compile tools/tripo_sample.py`; actual `prepare` (11 offline dry-runs); calling `execute` without both explicit controls rejected before any CLI command. Unity and paid execution were not run. Formal bundled CLI references used are saved alongside this runbook.

## Review hardening verification

The launcher now calls the login-shell `tripo` function with a constant `export TRIPO_CLIENT_APP=codex; tripo "$@"` program, preserving local proxy setup and positional-argv safety. A single real read-only balance check returned exit 0, balance 0, frozen 0; evidence: `proxy-balance-check.json`. No paid command was used.

Successful state is reusable only after the task ID, model path and every recorded file SHA-256 are verified. Missing or changed artifacts require recovery. Missing, negative, boolean, nonnumeric or nonfinite billed credits remain `needs_attention` with the task ID and CLI record saved; they never default to zero and never permit the next submission. The output model must belong to the current asset's actual output directory.

`test_runner.py` uses temporary directories and a stubbed CLI exclusively: five passing offline tests cover missing/invalid billing, credit-cap stopping, success reuse without another paid call, missing/changed hash/model evidence, failed submission stopping and re-entry refusal, and unconfirmed execution refusal. Output: `offline-tests.txt`. Manifest and reviewed plan files were not changed by these fixes.
