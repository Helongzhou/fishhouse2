# Unity readiness — 2026-11-09

实际Unity 6000.6.8f1 batch执行完成，进程退出码1（预期拒绝）。manifest schema1 / 11 unique IDs合法；11模型全部缺失。import、LOD、rig/animation、visual QA均not_run，scene未创建。此结果只证明准备验收器正确拒绝空资产，不表示Tripo样板完成。

执行前pgrep未发现Unity或AquariumJournal。命令（需独占Editor后运行）：

```sh
/Applications/Unity/Hub/Editor/6000.6.8f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath '/Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/unity6-validation' -executeMethod Aquarium.TripoSample.TripoSampleReadiness.RunBatch -logFile '/Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/verification/tripo-sample-01/unity-readiness/Editor.log'
```

- `report.json`：11项实际就绪拒绝报告；`Editor.log`：原始Unity日志。
- `source-before.json` / `source-after.json` / `source-diff.json`：原unity与候选项目Assets、Packages、ProjectSettings的SHA256比对。modified/deleted均空；仅新增_TripoSample代码/asmdef及其meta。
- 验收器不引用Core/Adapter，不创建GameSession，不调用正式Prepare，不读写PlayerPrefs或用户存档；用户数据保护依据代码隔离与源差异，不声称此次读取验证过用户存档内容。

当前无API生成产物。后续真实模型的导入、形态、尺度、URP材质、LOD、骨骼动画和近景/全景/开关灯验收，见 `docs/plans/2026-11-09-tripo-sample.md`。GLB导入依赖仍pending，未安装包。
