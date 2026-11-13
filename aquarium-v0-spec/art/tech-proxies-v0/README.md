# 观赏鱼布景养成游戏 · 首批 3D 技术代理样品（V0）

**状态：技术代理，不是终版美术，也不声称真实鱼种/草种还原。**  
生成与自检时间时区：Asia/Shanghai（UTC+8）。

本目录由本机程序化生成（Python / numpy / Pillow 写 glTF 2.0 GLB），用于 Unity URP 导入接线与近景/动画验收准备。预览静帧是 CPU 软件光栅，**不是** Unity URP 截图，也**不能**当作玻璃/水体透明或焦散通过。

## 先读结论

| 项 | 结论 |
|---|---|
| 现有可入引擎 3D 资产（核验前） | 在指定范围（`/workspace`、本 bot store/agent 数据）内 **未找到** `.blend/.fbx/.glb/.gltf/.obj/.usd`。这只说明这些路径里没有，不表示全球不存在。 |
| 现有图像 | 本 bot `assets/` 下 12 张 1280×720 JPEG，均为 **AI 参考静帧**，不是 3D 模型。指定的 `attachments/` 目录不存在。 |
| 本机 DCC | **未安装 Blender**；可用内存当时约 2.4 GiB，未强行安装完整 Blender。无 Autodesk FBX SDK。 |
| 本次实际交付格式 | **GLB（glTF 2.0）** + 静态 **OBJ（绑定姿势，无蒙皮动画）** + 程序化 PNG 贴图 + 源脚本。 |
| **FBX** | **未导出**（没有可用的真实 FBX 导出链路，不伪造）。 |
| 文件自检 | 10/10 GLB 通过独立再导入（自写 GLB 解析器 + pygltflib 交叉核对）：网格有限、尺寸为米、骨骼/权重点和、动画有可测顶点位移。 |
| Unity URP 引擎实测 | **未做**。玻璃/水体透明、折射、焦散、体积光、近景 LOD 切换均 **不能签字通过**。 |
| 专职建模 Bot | **不建议仅为分工名称新建**。名称不会增加 DCC 或导出能力。 |

## 页面（已用 ReadCanvas 核验）

- 视觉风格指南 v0.1（private）：https://cursor.com/canvas/store-918ecef8-5f4c-4c2a-a512-8b523bbc3280/fa6a8d63-15ca-4778-9e1b-f1deb9dfa565
- 新手三套参考板简报（private）：https://cursor.com/canvas/store-918ecef8-5f4c-4c2a-a512-8b523bbc3280/d32da0b8-6062-48d2-8226-3d38b741a9c6

`canvas` skill 文件 `/home/box/agent-data/managed-skills/skills/canvas/SKILL.md` 本机不存在；ReadCanvas / ListCanvases / EditCanvas 工具可用，页面已读到。

## 目录

```
aquarium-art-v0/
  README.md                  本说明
  ASSET_SPEC.md              首批资产可执行规格（建议，待拍板）
  assets_manifest.csv        资产清单
  source/                    生成与独立校验源码（可复现）
  export/glb/                可再导入的 GLB
  export/obj/                静态 OBJ（绑定姿势）
  export/textures/           程序化 PBR 贴图 PNG（亦已嵌入对应 GLB）
  preview/                   软件光栅静帧（非引擎）
  validation/self_check.json 独立再导入报告
  docs/ai_reference_stills.csv  AI 参考图路径（不是 3D 资产）
```

`.venv/` 仅用于本机生成，**不进入 zip**。

## 坐标与单位（建议，已按此做样品）

- 单位：**米**。60×30×36 cm 缸 = 宽 X 0.60、高 Y 0.36、深 Z 0.30。
- glTF / 样品：**右手系，+Y up，+Z forward**（glTF：资产正面朝 +Z）。
- 缸枢轴：内底面中心；前玻璃在 +Z（相机在更大的 +Z，看向 −Z）。
- Unity：导入器负责 RH→LH；建议用 glTFast 或 UnityGLTF，**Scale = 1**，不要再乘 0.01。
- 若将来从 Blender 导出：Blender 是 +Z up；需按导出插件做 Y-up 转换，并核对手性。**本次没有 Blender 导出转换可验证。**

## 首轮资产（一鱼两草 + 硬景，不扩鱼种）

| ID | 内容 | 枢轴 | 动画 |
|---|---|---|---|
| `tk_v0_tank60` | 60 缸玻璃薄壳 + 水体代理网格 | 内底中心 | 无 |
| `sb_v0_sand` | 底砂（含落到 y=0 的填充） | 内底中心 | 无 |
| `wd_v0_driftwood` | 沉木代理 | 底部中心（本地） | 无 |
| `rk_v0_stone_set` | 5 块景石一组 | 组底部中心 | 无 |
| `pl_fg_v0_carpet_proxy` | 前景地毯草代理 | 基部 | `sway_loop`（3s，循环，无根运动） |
| `pl_md_v0_leaf_proxy` | 中景叶片草代理 | 基部 | `sway_loop` |
| `fh_v0_tetra_proxy_L0/L1/L2` | 灯鱼外形技术代理（非物种还原） | 身体中心，朝向 +Z | `swim_tail_loop`（2s 循环）、`turn_left`（1.5s，循环回绑定，无根运动） |
| `scn_v0_tank60_triangle_proxy` | 上述拼好的三角构图示意场景 | 内底中心 | 上述 clip 同名共存，引擎侧应按对象播放 |

鱼体长再导入实测约 **4.79 cm**（含尾鳍）。L0 8 骨，L1/L2 4 骨。

## Unity URP 导入接线（待引擎验收）

1. 用 glTFast / UnityGLTF 导入 `export/glb/*.glb`。优先单个资产，场景文件可作摆放参考。
2. 检查 Transform：缸外尺寸应接近 0.60 × 0.36 × 0.30 m；鱼约 4.8 cm 长。
3. 材质：默认 PBR 金属-粗糙度。BaseColor = sRGB；Normal = 线性、OpenGL +Y（绿向上）；MetallicRoughness 贴图 G=粗糙、B=金属。
4. **叶子**：glTF `alphaMode=MASK` → URP 开 Alpha Clipping，双面。
5. **玻璃 / 水体**：**不要**指望 `KHR_materials_transmission` 在 URP Lit 上自动变成正确折射。需换成 URP 玻璃/水体方案（折射、吸收、焦散另做）。本次样品只提供带 alpha 的代理网格。
6. 动画：Animator 或 Playable；`swim_tail_loop` / `sway_loop` 勾选 Loop；**关闭 Apply Root Motion**。`turn_left` 为原地转向，位移由玩法驱动。
7. 蒙皮：Quality ≥ 4 bones。鱼 forward 为 +Z，场景里鱼已绕 Y 转 90° 以便沿 +X 游。
8. LOD：鱼已给 L0/L1/L2 三份文件，需在引擎里挂 LOD Group；本次 **未做** 距离切换实测。
9. 近景：滚轮/双击推相机 + LOD，不做放大镜 UI——需在引擎里验，软件预览不能代替。

## 许可与来源

- 本目录网格、贴图、动画、脚本均为本次原创程序化生成，供本项目使用。
- **未**使用扫描网格、商店模型和实拍贴图。
- `assets/` 里的 12 张图是既有 AI 概念静帧，仅作风貌参考，**不要当 3D 资产导入引擎**，也未打进本 zip 的 export 目录。

## 复现

```bash
python3 -m venv .venv
.venv/bin/pip install -r source/requirements.txt
.venv/bin/python source/generate_v0_assets.py
.venv/bin/python source/validate_v0_assets.py
```

## 明确未通过 / 未做

- Unity URP 导入、玻璃/水体透明与折射、焦散、体积光、近景 LOD。
- FBX 导出与 Blender 视口。
- 真实物种外形与终版材质。
- 官方 Khronos `gltf-validator` 二进制未装；自检为自写解析器 + pygltflib。
- 各独立 GLB 目前仍嵌入了一整套共用贴图（含未引用材质），文件体积偏大，但不影响网格/动画有效性。
