# Tripo 11资产样板准备与接入计划

目标：先留下可复验的11项制作清单、费用预检与Unity缺资产拒绝报告；真实资产到位后，再完成独立样板，不用灰盒冒称完成。

## 当前事实与方案

- 目标为 `unity6-validation`，Unity 6000.6.8f1 / URP 17.6.0；`unity` 基线不改。
- 美术依据：`docs/物种研究与美术参考规范.md`；manifest为 `art/tripo-sample-01/manifest.json`，schema_version=1，11项：Ember、樱花虾、螺研究、5草、2石、1木。
- 当前余额0，无API生成产物、模型或样板场景。提示词、设计尺寸、预算和摆放为制作建议，不是已完成资产或核实的成体范围。
- 采用独立 `Assets/_TripoSample/Editor` 验收器，不引用 Aquarium.Core/UnityAdapter，不调用 `AquariumProjectSetup.Prepare`，不运行 GameEntry。后者会创建会话/几何，Prepare会改全局项目设置，均不适合样板隔离。
- FBX/OBJ可使用Unity内置导入；Tripo原生GLB若需使用，先核实官方glTFast具体版本对Unity6000.6/URP17.6兼容及许可证并锁包，或离线转FBX。当前不安装包、不自写GLB解析器。
- 获root授权只实施Editor就绪验收器并独占一次短batch；不实现无模型的场景、LOD或动画生成器。

## 已准备的复验入口

- [x] `python3 tools/tripo_sample.py prepare`：真实11项dry-run已通过，总费用1210 credits，无警告；离线费用/payload预检输出 `art/tripo-sample-01/plans/` 和 `verification/tripo-sample-01/preflight-summary.json`；不生成、不充值、不修改CLI全局配置。以该报告实际费用为准，费用不含远端自动rig、重拓扑/转换。
- [x] Unity短batch执行 `Aquarium.TripoSample.TripoSampleReadiness.RunBatch`，报告 `verification/tripo-sample-01/unity-readiness/report.json`。schema1/11项/唯一ID/local_files结构通过，但11个model为空时必须failed、missing_models=11、退出码1；LOD/rig/visual为not_run。这是验收器拒绝缺资产的验证，不是样板验收通过。
- [x] 对比 `source-before.json` 与 `source-after.json`，确认只有新增_TripoSample代码和Unity生成meta；正式场景、BuildSettings、GameEntry、资源配置和原unity基线不变。不要访问或修改用户1300金币/两草存档及zh-CN偏好。

## 真实模型到位后（依赖：API额度与已确认的生成预算）

本地导入、材质、动画、LOD和灯光验收均在现有用户授权范围内，无需额外执行授权。

1. 逐件生成/下载，保存task ID、实际费用、授权证据和文件SHA256；更新manifest local_files，不凭planned推断成功。
2. 整理本地模型/贴图与来源文件，确认米制、+Y向上、+Z朝向、pivot和缩放；导入Unity后记录真实网格、材质、贴图尺寸和缺失引用。
3. 检查形态并必要重拓扑：鱼主要鳍/脂鳍/叉尾，虾体节与附肢，螺壳软体关系，水草成熟水下叶与根茎。人工QA不能被自动网格计数代替；螺仍是研究样板，不更改V0商品。
4. 作者制作真正不同的LOD0/1/2网格，记录三角数递减/网格身份/轮廓；禁止空物体或同网格重复伪造LOD。manifest预算仍需目标硬件实测后冻结。
5. 按实际拓扑决定骨骼/蒙皮/程序动画；不依赖远端自动rig，不预先保证生成网格可动画。鱼游动/转向/觅食、虾爬行/尾弹、螺伸缩/爬行、水草柔度分别验收；静态石木不要求骨骼。
6. 创建独立样板scene/prefab与URP材质，避开正式BuildSettings和GameSession/存档；使用独立镜头近景/全景/开关灯验收，记录透明鳍、双面叶、半透明虾及螺壳粗糙度。不得替换正式缸内资产后冒称原玩法已接入。
7. 在目标硬件录制相同镜头、重复LOD切换与动画测试，保存截图/视频/性能及逐项人工验收报告；再决定是否进入正式生态/开缸/存档接入的独立任务。

当前生成阻塞不会被就绪脚本消除。所有模型仍缺失时，交付状态只能是“准备完成、生成/样板待办”。
