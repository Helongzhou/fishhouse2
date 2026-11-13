# Unity 6 build05 visibility fix Implementation Plan

**Goal:** Empty and unlit tanks remain clearly usable for layout, with a visibly brighter lit state.

**Constraints:** Unity 6000.6.8f1 / URP 17.6.0; candidate only; preserve build04 UI, ecology and user progress. Root ruling: this repairs an existing usability bug without adding gameplay. Non-git review uses before copies, unified diff and SHA-256; preserve unity/ baseline. Native implementation by assigned owner, no subagents.

**Checklist:**
- [x] Inspect lighting/pipeline/material/camera sources and record diagnosis.
- [x] Root reviews minimal design and releases source implementation.
- [x] Save source-before copies and implement candidate presentation lighting.
- [x] Add reproducible migration patch and verify exact output/hash.
- [x] Review diff and report; wait for root exclusive Unity slot.
- [x] Real GPU at 1280 zh-CN: user empty dry tank and independent stocked fixture light off/confirmed on; current Chinese UI readable.
- [ ] Right-drag/side angle and 4K with new lighting.
- [x] Build05 compile/build/signature/license evidence.
- [x] Root progress restoration/final preservation acceptance: original primary hash unchanged, zh unchanged; build05 F9 restores 1300 coins/two plants.

## Diagnosis (read-only inspection)
- Active scene has only AquariumGameEntry; it serializes no RenderSettings, skybox, baked lighting or default light. Runtime BuildTank creates the only directional light.
- AquariumGameEntry.cs:91 assigns ambientLight but never ambientMode. Unity's default skybox ambient mode does not use that flat ambient color; this scene provides no authored skybox/probes. The intended dependable fill is therefore not established. Confirm actual mode/probe in isolated validation, since source inspection cannot establish runtime engine defaults.
- GraphicsSettings selects AquariumURP (guid dd53fc9bb7d72432a86e426bbdbb918f); quality top level selects same, other quality levels inherit default. URP main-light rendering is enabled. AquariumOpaque references URP Lit, is opaque, nonmetallic, has no emission. No evidence of a missing/pink shader or a disabled main-light renderer.
- Runtime lamp is .55 off / 1.15 on, but tank base/edges use (.15,.19,.20), back uses (.035,.10,.13), and background (.025,.045,.05). Lit dark surfaces without a reliable ambient floor collapse into near-black. Camera starts 17 degrees above front; base is visible geometrically. This is a presentation lighting/material contrast defect, independent of the equipment requirement for ecological light.

## Minimal change
- In candidate AquariumGameEntry only, explicitly establish flat ambient lighting and a stable ambient probe at runtime; retain a directional key whose off/on intensity keeps an obvious difference. Prefer a single lamp plus ambient floor over adding multiple lights (URP main-light selection remains unambiguous).
- Give base and glass edges a separate readable neutral material, brighten back/background modestly if the real off-state image requires it. Keep plants/stone Lit and preserve normal/shape shading; do not use unlit/emission to conceal the defect.
- Keep World.LightIsOn, ecology, transactions, catalog, save schema/path and user files untouched. Do not change UI/font build04 work or unity/ baseline.
- Reproduce via tools/unity6_patches/visibility-lighting.patch applied by tools/prepare_unity6_validation.py, included in preparation manifest hashes/intended changes.

## Verification and boundaries
1. Root preserves current user session and closes Player before any execution. No user save access by fix owner.
2. Isolated fresh 60cm empty tank, no equipment: off state clearly separates back, base, edges and background; two placed plants can be located and selected.
3. Same camera/layout, equipped light toggled off/on: visible brightness difference and shape shading, off remains usable. Check rotated camera and substrate/water case.
4. Use separate test persistent storage; confirm transaction/light ecology checks and build04 Chinese/layout remain passing; no economy/progress migration.
5. Root grants one exclusive Unity build slot. Produce build05 + report/screenshots, then root restores/checks user session without resets.

## Files
- unity6-validation/Assets/_Game/UnityAdapter/AquariumGameEntry.cs
- tools/unity6_patches/visibility-lighting.patch
- tools/prepare_unity6_validation.py
- docs/plans/2026-11-06-visibility-fix.md

Status: source/build05 complete; root real Player empty/off/on screenshots pass the stated 1280 zh-CN scope. No user persistent files touched by fix owner. Root final progress restoration completed; Player remains open in the restored user session. Right-drag/side angle/4K remain outside completed checks.
