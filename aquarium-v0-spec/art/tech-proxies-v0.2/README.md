# aquarium-art-v0.2 · 两草最小改进（技术代理）

**本目录是独立版本，不覆盖** `/workspace/aquarium-art-v0/` **首批任何文件。**  
时区：Asia/Shanghai（UTC+8）。样品是原创程序化**技术代理**，不是终版美术，**不声称真实物种还原**。  
预览是 CPU 软件光栅（可采样贴图），**不是 Unity URP 截图**，也**不是水流物理模拟**。Unity 视觉**未做、不签字**。

## 映射修正（请程序按此改槽位，默认关闭、原占位可保留）

| 引擎槽（仅技术验证） | 不要用（形态不合格） | 改用 |
|---|---|---|
| `plant_anubias_nana` | `pl_fg_v0_carpet_proxy`（地毯丛，无匍匐根茎/叶柄/卵形叶） | `export/glb/pl_v0_2_anubias_proxy.glb` |
| `plant_java_fern` | `pl_md_v0_leaf_proxy`（偏卵形短叶，不像长披针铁皇冠） | `export/glb/pl_v0_2_java_fern_proxy.glb` |
| `ember_tetra` | —（网格可继续用作技术槽） | 仍用首批 `fh_v0_tetra_proxy_L0`（本目录仅有哈希一致的只读副本，**未改网格**） |

地毯代理仍可当「前景地毯占位」，只是**不能叫水榕**。

## 接入：单位、缩放、材质、动画

- 单位：**米**。适合默认 60×30×36 cm 缸。导入 Scale = **1**，不要当厘米再乘 0.01。
- 轴向：glTF 右手系，+Y up，+Z forward。交给 glTFast / UnityGLTF 转 Unity 左手系。
- 枢轴：根茎基部原点（`rhizome_base_origin`），Y=0 贴基质。
- 水榕代理实测（再导入 AABB）：约 **8.4 × 9.3 × 6.0 cm**（宽×高×深）。高目标 6–10 cm，实测 9.3 cm。
- 铁皇冠代理实测：约 **8.2 × 14.3 × 7.2 cm**。高目标 10–14 cm，实测 14.3 cm（**略高于上限 0.4 cm**，仍属 36 cm 缸中景）。
- 叶片材质：glTF `alphaMode=MASK`，`doubleSided=true`，BaseColor 贴图含有效 alpha（有 0 与 255）。URP 请开 Alpha Clipping、双面。根茎为不透明 PBR。
- 动画：仅 `sway_loop`（水榕 3.0 s，铁皇冠 3.5 s），**循环、无根运动、只有旋转**。根茎/根骨不摆；叶柄小摆、叶片更大、相位错开。请关闭 Apply Root Motion。
- 组合场景 `scn_v0_2_two_plants_proxy.glb` 仅作对比（水榕 x=-0.09，铁皇冠 x=+0.09）；clip 名带植株前缀，避免重名。

## 鱼转向请勿误用

`turn_left`（首批 L0，本目录副本未改）只有骨骼旋转，首尾回到绑定朝向。这是**原地弯曲后回正**，**不是**世界空间转向，也没有位移。玩法转弯要由逻辑旋转/移动根节点。

## 许可

新网格与贴图：本次原创程序化，供本项目技术验证使用。**不承诺排他权或第三方商业素材授权。**  
运行库：numpy 2.5.4（BSD）、Pillow 13.0.0（HPND）、pygltflib 1.16.5（MIT）。详见 `docs/LICENSE_AND_THIRD_PARTY.md`。

## 主要文件

- 逐项验收：`docs/ACCEPTANCE.md`、`validation/self_check.json`
- 清单：`assets_manifest.csv`
- 预览：`preview/`（均标明非引擎）
- 首批冻结哈希：`docs/v0_baseline_sha256.txt`（结束时复核未变）
