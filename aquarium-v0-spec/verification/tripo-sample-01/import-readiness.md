# Studio样板本地导入路线

2026-11-09，只读准备；尚未获得实际模型格式，未启动Unity、未安装工具或包。

## 已核实

- `unity6-validation/ProjectSettings/ProjectVersion.txt`：6000.6.8f1；Packages manifest仅URP 17.6.0与imgui/jsonserialize/physics模块。没有glTF导入包。
- `command -v blender`无结果；`/Applications`无Blender。当前不能假定本机可离线GLB转FBX。
- Unity官方registry稳定版本`com.unity.cloud.gltfast` **6.20.0**声明最低Unity **6000.0**，Apache-2.0。registry的latest标签指向**7.0.0-exp.1**，因此不能无版本安装。
- 6.20.0官方包文档明确支持URP shader graphs、Editor `.glb`/`.gltf` ScriptedImporter并生成原生Unity资源。文档引用6000.6；这给出版本适用依据，但尚不是6000.6.8f1/URP17.6.0实导入通过证据。
- 依赖：Burst1.8.30、Collections2.6.8、Mathematics1.3.3、jsonserialize1.0.0、unitywebrequest1.0.0。PNG/JPEG需要imageconversion模块；动画播放需要animation模块，均须按实际文件再决定。压缩扩展需要另行匹配Draco/KTX/meshopt插件，不能预先假定不存在。

官方registry与包内原始证据保存在`import-package-evidence/`；仅下载到内存检查tarball并保存上述小文本，没有安装。

## 最小路线

1. 若Studio直接提供带贴图FBX：保留原下载包和SHA256，以Unity内置FBX导入作为最低依赖路线；外置贴图保持相对关系，随后只在独立样板目录制作URP材质。OBJ也可内置导入，但不能承诺骨骼/动画。
2. 若实际只有GLB/glTF：建议root裁定锁定官方**6.20.0**，不安装实验7.x、不装大型Blender来绕行。先在项目外读取GLB JSON头，确认extensionsRequired、贴图编码、网格/材质/动画与外部引用，再增补确实需要的模块。获得Unity独占槽后修改validation包manifest并让UPM生成锁文件；首次真实模型导入通过后才批量导入。
3. 原始文件留在`art/tripo-sample-01/studio-generated/<id>/`。导入副本仅进入`unity6-validation/Assets/_TripoSample/Models/<id>/`；glTF外置bin/贴图不能改名或破坏相对路径。逐件记录源hash、网格/材质/纹理统计、缺失引用、bounds与导入日志，再制作独立scene/prefab。
4. 当前本轮9项，排除stone_secondary与buce_bukit_kelam；原11项manifest不改成假通过。Studio赠送额度产物仅用于非商业内部测试，不执行旧API/P2预算。

此阶段不做无模型LOD、骨骼或空scene。不运行AquariumProjectSetup.Prepare；不触碰原unity、正式GameEntry、BuildSettings、玩法配置、用户存档或PlayerPrefs。等待实际格式与root裁定后继续。
