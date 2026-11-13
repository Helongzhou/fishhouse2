# Visibility fix 01 progress

## Request and preservation boundary
User reports poor scene visibility. Root saved the active Player with F5 and observed the saved confirmation before this snapshot. Preserve actual latest user state; never overwrite it with manual-player-02 defaults.

## Confirmed user state
- Wallet 1300 coins; plant_anubias_nana purchased for 90 coins (1 unit), plant_java_fern for 110 coins (1 unit).
- No placements; no substrate; tank not filled; light off.
- Language zh-CN, captured separately from JSON save.

## Assignments
- Root: user coordination, Player lifecycle, UI verification and acceptance.
- Fix agent: source repair and exclusive Editor/build work after root exits Player.
- Evidence agent: read-only save checks/copies, hashes, screenshots and progress evidence; no product edits, Editor, UI or Player lifecycle operations.

## Evidence
user-before/manifest.json records all five save candidates (including absent files), hashes and copied language preferences. user-before/user-reported-visibility.png is an unmodified screenshot copy. Source save and preferences were not modified. Only language entries were extracted from prefs; unrelated prefs were not copied or printed.

## Pending
Source changes, before/after diffs, build paths/hashes and runtime outcomes await root/fix-agent results. Use file hashes and diffs without Git worktrees. No Editor or Player was started or stopped by this evidence agent.

## Root preflight and ruling
- Product task owns only candidate GameEntry and patch/prepare scripts. Audit shares read-only access to these files; no write conflict.
- Evidence task owns user backup, ledger and docs only. Root exclusively owns UI. Fix task may execute Unity only after root grants access following Player exit.
- Root reviewed docs/plans/2026-11-06-visibility-fix.md. This is a limited existing visibility bug under the user's continuing authorization for Sol repair; repeat approval is not required.
- Non-Git evidence uses before snapshots, diffs and hashes instead of commits/worktrees. Git-dependent skill scripts are not run.
- Shared implementation goal: display lighting does not alter ecology LightIsOn, economy or saves.
- Visual acceptance requires GPU screenshots; static color assertions alone do not establish a visual pass.
- Backup completed and root notified before Player shutdown. Latest primary save SHA256: 7de4837dca22088b213391acbbabf361e34c589d710a374546a869aad2302a4b.

## Build 05 runtime / stocked-light fixture
Root reports build05 success and actual Player empty-tank brightness improvement, with original user save loaded through F9 (1300 coins, two plants), Chinese language correct. Screenshot: 01-user-empty-fixed.png. Root did not press F5 and then quit Player. Evidence agent confirmed no Aquarium Player or Unity Editor process, and original primary hash still exactly 7de4837dca22088b213391acbbabf361e34c589d710a374546a869aad2302a4b.

At root's request, manual_player_save.py staged manual-player-02/snapshots/01-paused.json into the persistent save path only after fresh backup in stocked-light-check/before and a new save-manifest.json. Fixture is paused, light off, 14 agents and 90 coins. This is a temporary visual acceptance fixture, never user progress. Language preferences remain untouched (zh-CN); language-only backup and source hash are in stocked-light-check. Status is STAGED; restoration awaits root's explicit post-test Player-exit notification. Root will verify stocked scene under light off/on to check overexposure.

## Actual Player acceptance and restoration
Root completed real Player 1280-resolution Chinese (zh-CN) checks:
- 01-user-empty-fixed.png: original user empty/dry tank, 1300 coins and two unplaced plants; brightness visibly improved.
- 02-stocked-light-off.png: paused acceptance fixture with light off; fish and plants discernible.
- 03-stocked-light-on.png: retained failed attempt; despite its filename the light was still OFF. Rapid key input left pause blocking the L command. Do not use this image as on-state proof.
- 04-stocked-light-on-confirmed.png: actual light ON with fixture paused; clear illumination difference without washing the scene white, according to root's runtime inspection.
Root then F5-saved the ON fixture for after/ evidence and quit with super+q. Evidence agent confirmed no Player or Editor, archived current Player.log, and read-only checked the final fixture save: LightIsOn=true, Paused=true, 90 coins, 14 agents. Time advancement is not treated as a failure.

manual_player_save.py restore completed. Current user primary is byte-identical to the initial user save (SHA256 7de4837dca22088b213391acbbabf361e34c589d710a374546a869aad2302a4b), and bak1/bak2/bak3/tmp have their original absence states. Chinese preference remains zh-CN. No user file was cleared; temporary acceptance saves are retained under stocked-light-check/after. See restore-verification.json. Root was notified immediately that user Player can restart; evidence task will not exit the subsequently running Player.

Not tested in this lighting pass: right-drag side view, new lighting at 4K, or isolated placement into the dry tank. This evidence does not claim every visual or mouse interaction check passed.
