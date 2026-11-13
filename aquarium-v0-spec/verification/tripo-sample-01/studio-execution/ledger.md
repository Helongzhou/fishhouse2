# Studio execution ledger

## 2026-11-09 preflight

- Native Chrome UI opened `https://studio.tripo3d.ai/zh` and showed signed-in greeting `zhouhlwork`; balance **200**.
- Assets page showed four `Tripo Demo Model` entries dated `11-09 10:29`. No aquarium assets observed.
- Existing demo task IDs: `1b674e7b-65fa-4d4a-9c38-a7a668e8b3a0`, `3ae3b5a0-e9c4-4cb8-9b40-9125e9b769f4`, `5342c4fd-9759-4690-b95b-075a76c3a442`, `fbe6ea7e-3c82-457c-9622-e1bdb038df1d`.
- Workbench URL: `https://studio.tripo3d.ai/zh/workspace/generate`.
- Default UI parameters: high precision model, H3.1 highest quality, public, part generation off, 8K texture off. Generate button displayed **30**.
- H2.5 pricing and export entitlement remain unverified. No generation was submitted; no credits spent; no files downloaded.
- Blocker: AX clicks navigate ordinary links, but model selector clicks only focus the element. Space/Return/Down did not open it. Two native screenshot-coordinate clicks returned `Computer Use server error -10005: noWindowsAvailable`. Rebinding Chrome and invoking exposed window `Raise` did not resolve this. Runtime did not expose optional `cua.computer.launch_app`.
- Root notified to independently check native input. Desktop released after CUA completion summary.
