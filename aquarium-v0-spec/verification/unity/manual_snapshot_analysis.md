# Recorded manual-player snapshots

`analyze_manual_snapshots.py` reads explicitly named artifacts under `verification/`. It never discovers or reads the active player save directory, writes a save, launches Unity, or supplies input. The plan, snapshots, catalog/enum sources and screenshots receive SHA256 records; the executed analysis script is retained with the output. Output must be a new directory under `verification/`; existing evidence is never overwritten.

After the coordinator records real F5 snapshots, copy `manual-snapshot-plan.example.json` into that manual evidence directory and replace every placeholder with an actual artifact. Snapshot and screenshot paths resolve relative to the plan. For example:

```sh
python3 verification/unity/analyze_manual_snapshots.py \
  --plan verification/unity/unity6-validation/manual-player-02/analysis-plan.json \
  --output verification/unity/unity6-validation/manual-player-02/analysis-01
```

The example is not evidence and must not be run as a substitute for recording the operations. F9 restores the primary save currently on disk; its target must be established by the coordinator's actual procedure. Never assume F9 restores an earlier copied snapshot after later F5 saves have replaced the primary.

`report.json` and `analysis.md` contain clocks, pause/light state, coins, stage, active food ownership/portions, nitrogen, receipt counts/operations, species counts, fish/shrimp action distributions and aggregate behavior counters. Species kinds come from the selected candidate's actual runtime catalog; action/stage numbers come from its source enums. This Python inspection does not execute Unity's semantic Restore validator.

| Check kind | Required state evidence |
|---|---|
| `paused_exact` | Both paused; identical complete JSON structure, values and saved bytes. No tolerance. |
| `paused_commands` | Same exact checks after observed F/L attempts; no additional accepted receipt. |
| `resume_light` | Running clock advances; saved light toggles; exactly one new successful light receipt agrees with the state. |
| `resume_feed` | Running clock advances; exactly one new successful feed receipt; a new source event; external food nitrogen matches the receipt's portions. |
| `restore_paused` | Both paused; identical complete snapshot structure and strict integer/identity state; only serialized decimal values may use the previously approved `1e-12 * max(1, abs(a), abs(b))` codec bound. Every nonzero decimal difference is recorded. |

The running checks default to unchanged wallet and fish/shrimp identities. If a legitimate route reward occurred between selected snapshots, record that separately and set `"same_wallet": false` on that specific check; the report still shows the actual before/after coins. Food can be consumed or expire during a running interval, so the feed check uses persistent accepted receipts and cumulative external food input rather than requiring an uneaten particle to remain.

If the coordinator resumes, performs L/F, then deliberately pauses before capturing F5, declare `"after_paused": true` on that light/feed check. Successful command receipts and an advanced clock still establish the state changes; the pause state at capture must match the declared timeline. The default expects an unpaused capture.

JSON parsing preserves arbitrary-sized integers and exact decimal tokens, rejects duplicate fields and non-finite numbers, and keeps booleans distinct from integers. Snapshot comparison preserves field sets and array order. Exact pause comparisons never use the codec bound. The tool detects recorded snapshot changes during analysis and refuses paths outside `verification/`, including symlinks resolving to live saves.

Optional `screenshots` entries have `label`, `path` and an optional coordinator-written `observation`. They are linked and hashed, not interpreted or approved by the script. `state_evidence_passed` means only that the declared snapshot comparisons passed. A snapshot cannot prove whether a key or a mouse button caused a state change, nor prove that an attempted paused command occurred. Combine these findings with the actual input timeline and visual review before claiming physical routing acceptance.
