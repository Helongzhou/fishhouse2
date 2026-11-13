# Aquarium V0 snapshot import

This directory records the sanitized import. The four `SNAPSHOT-*` files are the original snapshot reports, copied unchanged. Nothing under `aquarium-v0-spec/` was edited.

## Source

- Mac project root: `/Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec`
- The Mac tree had no `.git`. This import does not rewrite that machine and does not replace history on `main`.
- Repository path: `aquarium-v0-spec/`
- Snapshot time (Asia/Shanghai): 2026-11-13T11:00:17.384097+08:00

## Unity entries

Confirmed from the imported project files. This note does not claim an editor import, compile, or visual check.

| Tree | Editor | URP | Entry scene | Entry script |
| --- | --- | --- | --- | --- |
| `aquarium-v0-spec/unity/` | Unity 2022.3.62f3 | 14.0.11 | `Assets/_Game/Scenes/Aquarium.unity` | `Assets/_Game/UnityAdapter/AquariumGameEntry.cs` |
| `aquarium-v0-spec/unity6-validation/` | Unity 6000.6.8f1 | 17.6.0 | `Assets/_Game/Scenes/Aquarium.unity` | `Assets/_Game/UnityAdapter/AquariumGameEntry.cs` |

## What was checked before commit

- Four ZIP archives matched the supplied SHA256 values.
- Archive paths had no absolute paths, `..` components, or symlinks.
- `SNAPSHOT-MANIFEST-SHA256.txt` lists 861 files and 73,727,625 bytes. All 861 extracted files matched that SHA256 and size.
- Joined archive entry count is 865: 861 project files plus these four snapshot reports.
- A fresh filename and content scan found no excluded cache or secret file. The only env file is `aquarium-v0-spec/fishatlas-site/platform/.env.example`, whose single setting is a local `127.0.0.1` URL with no userinfo.
- Programmatic art samples are not in this snapshot and were not added.
- Nested ignore files were left unchanged. `git add -f` was used so verified files they would skip are still in this commit, including `fishatlas-site/platform/` paths under `media/` and `payload-types.ts`, plus Unity `.csproj` / `.sln` files ignored inside `unity6-validation/`.

## Exclusion scope

See `SNAPSHOT-EXCLUSION-REPORT.txt` for the 95 excluded entries. In short, the snapshot left out `.env` and credentials, `.runtime`, Unity `Library` / `Temp` / `Obj` / `Logs` / `UserSettings` / `Builds` / `.tools`, `node_modules`, `.next`, `bin` / `obj`, logs, `.DS_Store`, and symlinks. No project script was executed for this import, and no package install or build was run.
