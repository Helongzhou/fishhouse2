# Unity source project

Unity **2022.3.62f3** (revision `96770f904ca7`, Apple Silicon), URP **14.0.11**. The current integration target supersedes the previous source-only f1 baseline following the official security patch review. This is a true 3D source project with geometric placeholders. The current editor installation is tracked separately; until real evidence is recorded, editor compilation, import, Play, JsonUtility file roundtrips and player builds have **not** been run.

## Open and prepare

1. From the repository root run `python3 tools/build_runtime_config.py` and `python3 tools/build_catalogs.py`.
2. Open this `unity` directory in the pinned Unity editor. Unity may resolve the pinned URP package; no Unity installation/package download was performed in this session.
3. Choose **Aquarium → Prepare Project**, then **Aquarium → Open Aquarium Scene**. Prepare generates the URP renderer/pipeline assets, assigns Graphics/Quality pipelines, registers the startup scene, and creates opaque/transparent Resources material assets to retain the used shader variants. These assets still need actual editor/player verification.
4. Enter Play in `Assets/_Game/Scenes/Aquarium.unity`.
5. **Aquarium → Build Windows x64** is an explicit helper that prepares the project then builds to `Builds/Windows/AquariumJournal.exe`. The Windows module and real build validation remain required.

Batch entry points are `Aquarium.UnityEditorTools.AquariumProjectSetup.Prepare` and `.BuildWindows`; use the installed editor's `-batchmode -projectPath <this-directory> -executeMethod <entry> -quit` only when the appropriate Unity installation/modules are available. No Steam account, SDK or publishing action is included.

## Aquarium scene

The new scene uses `AquariumGameEntry`. It begins with an empty tank and 1500 coins. Buy the base components, then select an owned component and click to place it; attached plants require an existing stone. Select and drag a placed object to move it, or press **R** to rotate 15 degrees. Four substrate sliders control left/right corners at the front/back. The template action arranges only already purchased components through the same commands. Undo/redo also works for wet-tank plant moves.

Confirm the deployed essentials, fill once, and start the nonzero fishless nitrogen challenge. Actual stable processing unlocks **8 Ember Tetra** and **6 Red Cherry Shrimp**. Actual stocked observation grants 450 coins once and unlocks **8 Neon Tetra**; the prescribed route ends at 280 coins. Feeding and 20% replacement-water exchanges are free, with ecological consequences. New hardscape/plants/equipment cannot be bought after confirmation in this batch because draining/rebuilding is not implemented. Optional driftwood is hidden in this teaching UI.

The one experimental teaching permission covers only this 8/6/8 population, allowing an entire sold pack to be repurchased. Research and full compatibility remain pending; no missing ecological data is marked verified. Release mode refuses these pending animal introductions. Planned, reference and test content cannot enter the default tank.

Right drag orbits; wheel approaches; **Space** pauses; **F** feeds; **L** switches light; **F5/F9** save/load. Pause rejects business commands while camera/settings remain available. Ecology runs at 1/2/4× separately from real-time behavior. Offline time is never advanced.

`aquarium-session-v1.json` lives under `Application.persistentDataPath`, with `.bak1`–`.bak3`. The full snapshot contains behavior schema2, gameplay/ecology, food ownership and per-animal ingested reserves, plus an explicitly flagged, structurally nonrecursive checkpoint. The host uses JsonUtility; its actual Unity roundtrip remains unverified. Loading validates every candidate before replacing the live game, then falls back through semantically valid backups. Locale preference remains separate under `aquarium.language` PlayerPrefs.

English is the source/default/final fallback. en/zh-CN each have 269 keys; qps-ploc is QA only. Nine other planned locales are not completed translations. Steam language retrieval is still explicitly null. OS fallback fonts are temporary; licensed fonts, layout, plural rules and real screen QA remain outstanding.

## Limits

Boxes/ellipsoids are 3D placeholder art. Layout collision/support validation does not implement fish/shrimp hardscape navigation. Shrimp and food markers are visually projected above the sloped substrate to remain visible; their core movement/food coordinates are unchanged. This is not stone/wood surface navigation. Full plant growth, real algae grazing, authored models/animation/audio, physical camera collisions, Windows offline/performance and Steam release remain unverified or unimplemented.

`AquariumInteractionDemo` is preserved as an isolated behavior pressure fixture with panda corydoras. It does not run in the default startup scene and is not the teaching stocking route. Its separate behavior schema2 file remains `aquarium-behaviour-demo-v2.json`.

## Verify source only

From the repository root:

```sh
python3 tools/verify_slice.py
```

This builds the production pure C# core as C#7.3/netstandard2.1 and runs behavior, compatibility, ecology, gameplay, integration and localization checks. It does not compile Unity adapters against substitute APIs or establish a playable build. Results and remaining reviews are recorded in `verification/latest.json` and the implementation board.
