# Visibility fix source implementation

Candidate BuildTank now explicitly sets Flat ambient, its linear-color spherical-harmonic ambient probe, and sun to the single aquarium directional light. Off/on key intensities remain .55/1.15. Base (.38,.43,.44), frame (.44,.53,.55), back (.16,.25,.28) and background (.065,.10,.115) improve surface separation. No second light, unlit material, ecology/transaction/save/font/UI changes.

Before copies are in source-before/, unified changes in review.diff, SHA-256 in source-sha256.json. tools/prepare_unity6_validation.py applies visibility-lighting.patch after build04 patches and records its digest/intention in generated migration manifests.

Validation completed: preparation to /tmp/aquarium-visibility-repro-01 succeeded with baseline_unchanged=true and unity_launched=false; resulting GameEntry is byte-identical to candidate; Python AST parses.

Build05 completed with real Unity C# compile and macOS ARM64 Mono2x build: Succeeded, 0 errors/0 warnings. Invocation, source hashes, editor log and build report are under ../unity6-macos-build-05/. Deep strict codesign verification passed; font license notices present. The build owner exited Editor without launching Player; root subsequently performed the actual Player checks below. No new batch acceptance or Play smoke suite was run for build05.

Root actual GPU/Player verification at 1280 with zh-CN:
- 01-user-empty-fixed.png: user's 1300-coin/two-plant empty tank clearly separates base, back and frame.
- 02-stocked-light-off.png: independent filled/stocked fixture, plants and fish remain identifiable with light off.
- 04-stocked-light-on-confirmed.png: confirmed on state is visibly brighter without washing out surfaces.
- 03-stocked-light-on.png has a misleading filename: L was rejected while paused; it is not evidence of the on state.

Scope: these are real Player images, not injected Editor render fixtures. Right-drag/side angle and new-lighting 4K remain unverified. Build04 UI code is unchanged; current 1280 Chinese screen is readable. Historical batch-07 41 checks and Play-03 19 smoke checks retain their original versions and do not imply reruns for build05. Final state: root completed restoration (stocked-light-check/restore-verification.json confirms original primary hash identical and zh unchanged), restarted build05 and used F9; 05-user-restored-final.png shows 1300 coins, two plants, Chinese UI and the visible empty tank. Player remains open in the restored user session.
