# 文件层验收表（v0 复审 + v0.2 两草）

独立再解析 GLB，**没有**把首批 `self_check.json` 当证据照抄。
**Unity URP 视觉未做，本表不签字通过。** 预览为 CPU 光栅，非引擎；草摆为正弦采样，非水流物理。

| ID | 资产 / 映射 | 文件层 | 引擎 | 说明 |
|---|---|---|---|---|
| fish_proportion | fh_v0_tetra_proxy_L0 / ember_tetra (technical slot only) | 已过 | 待引擎验证 | 再导入体长 4.79 cm（含尾鳍；extras 曾写 3.8 cm 为生成目标，实测更长）。相对 60 cm 缸比例合理。不是 Ember Tetra 物种还原。 |
| fish_tail_wag | fh_v0_tetra_proxy_L0 | 已过 | 待引擎验证 | 8 骨链，仅 rotation，循环闭合，无根位移通道。t=0.5s 相对 t=0 最大顶点位移 4.53 mm。文件层证明摆尾存在；游戏观感待 URP。 |
| fish_turn_semantics | fh_v0_tetra_proxy_L0 / ember_tetra | 有条件通过（文件层有原地弯曲 clip）；若当作空间转弯则不合格 | 待引擎验证 | turn_left 只有骨骼旋转通道，无 translation；首尾关键帧回到绑定姿势（loop_keys_close=True）。中段最大顶点位移约 12.9 mm。这是「身体弯曲后回到原朝向」的原地姿态，不是世界空间航向改变，也不产生位移。玩法转向必须由逻辑旋转/移动根节点；不要把该 clip 当成完成了一次空间转弯。 |
| fish_species_mapping | fh_v0_tetra_proxy_L0 -> ember_tetra | 仅技术占位可接受，物种还原不合格（未声称） | 待引擎验证近景品种可读性 | 资产 extras 已标明 technical_proxy / not_real_species。可作为 ember_tetra 技术槽验证蒙皮与 clip 名称，不能当作灯鱼品种造型通过。 |
| mapping_carpet_as_anubias | pl_fg_v0_carpet_proxy -> plant_anubias_nana | 不合格 | 不要在引擎里按水榕验收此网格 | 该网格是前景地毯丛：约 13.3×5.2×13.9 cm，36 叶状细条、径向铺开，骨骼为 1 根 + 3 簇茎，无可辨识匍匐根茎、叶柄或厚实卵形叶。把地毯代理映射为 plant_anubias_nana 会误导形态。应停用该映射；改用 v0.2 pl_v0_2_anubias_proxy 做技术验证（仍非物种还原）。地毯网格可继续当「前景地毯占位」，不要叫水榕。 |
| v0_carpet_alphaclip_fields | pl_fg_v0_carpet_proxy | 已过（glTF 字段与 alpha 贴图） | 待引擎验证 URP Alpha Clipping / 双面 | alphaMode=MASK, doubleSided=True, tex={'name': 'leaf_fg_bc', 'is_png': True, 'size': [256, 256], 'alpha_min': 0, 'alpha_max': 255, 'frac_a0': 0.21844482421875, 'frac_a255': 0.102935791015625, 'frac_a_lt_102': 0.4373779296875, 'unique_alpha': 241}。文件层 MASK+doubleSided+有效透明通道成立；URP 转换未测。 |
| mapping_midleaf_as_java_fern | pl_md_v0_leaf_proxy -> plant_java_fern | 不合格（形态不足以代表铁皇冠） | 不要按铁皇冠近景验收此网格 | 尺寸约 15.2×7.7×8.0 cm，高仅约 7.7 cm；叶片为偏卵形/阔叶剖面（生成宽度约 2.2–3.0 cm、长 4.2–6.2 cm），不是长披针叶。虽有短管状根茎痕迹，但不足以读成附生铁皇冠。4 骨簇状 sway，不是沿叶长的柔摆。技术槽可保留默认关闭；形态验收请改用 v0.2 pl_v0_2_java_fern_proxy。 |
| v0_midleaf_alphaclip_fields | pl_md_v0_leaf_proxy | 已过（glTF 字段与 alpha 贴图） | 待引擎验证 URP Alpha Clipping / 双面 | alphaMode=MASK, doubleSided=True, tex={'name': 'leaf_fg_bc', 'is_png': True, 'size': [256, 256], 'alpha_min': 0, 'alpha_max': 255, 'frac_a0': 0.21844482421875, 'frac_a255': 0.102935791015625, 'frac_a_lt_102': 0.4373779296875, 'unique_alpha': 241}。 |
| v0_plant_sway_generic | both v0 plant proxies | 已过（有循环旋转 sway，无根位移通道） | 待引擎验证 | sway_loop 为程序正弦骨骼旋转，不是水流物理。v0 草是整簇 3 茎骨带动，容易显得偏刚体。v0.2 改为叶柄/叶片分级相位。 |
| closeup_readability_v0 | v0 mapped trio | 文件层仅确认有网格/UV/贴图，不签字 | 待引擎验证（近景阅读未做） | 无 Unity 画面。软件预览不能代替近景侧纹、叶脉、透明叠加验收。 |
| v02_anubias_morphology | pl_v0_2_anubias_proxy | 已过（技术代理形态） | 待引擎验证近景可读；不签物种还原 | 实测包围盒 8.4×9.3×6.0 cm（宽×高×深），高目标 6–10 cm，实测 9.3 cm。独立根茎网格 + 6 枚带叶柄的卵形叶，不是地毯。仍是程序化技术代理，不承诺水榕精模或物种还原。 |
| v02_anubias_sway | pl_v0_2_anubias_proxy | 已过（文件层分级柔摆） | 待引擎验证；非水流物理 | 根/低处平均位移 0.28 mm，叶尖平均 5.36 mm。叶柄/叶片分骨、相位错开，根骨不转。这是正弦采样对比，不是流体解算。 |
| v02_anubias_alphaclip | pl_v0_2_anubias_proxy | 已过（glTF MASK + doubleSided + 有效 alpha） | 待引擎验证 URP 双面 Alpha Clip | leaf mat={'name': 'mat_anubias_leaf', 'alphaMode': 'MASK', 'alphaCutoff': 0.4, 'doubleSided': True, 'has_baseColorTexture': True, 'has_normalTexture': True, 'has_mrTexture': True, 'baseColorFactor': [1.0, 1.0, 1.0, 1.0], 'baseColor_tex': {'name': 'anubias_leaf_bc', 'is_png': True, 'size': [512, 512], 'alpha_min': 0, 'alpha_max': 255, 'frac_a0': 0.09301376342773438, 'frac_a255': 0.8341941833496094, 'frac_a_lt_102': 0.10994338989257812, 'unique_alpha': 256}} |
| v02_java_fern_morphology | pl_v0_2_java_fern_proxy | 已过（技术代理形态） | 待引擎验证近景可读；不签物种还原 | 实测 8.2×14.3×7.2 cm，高目标 10–14 cm，实测 14.3 cm（略高于目标上限 14 cm）。附生向根茎 + 7 枚长披针叶（叶长目标 9.5–12.5 cm）。仍是技术代理，不是铁皇冠精模。 |
| v02_java_fern_sway | pl_v0_2_java_fern_proxy | 已过（文件层分级柔摆） | 待引擎验证；非水流物理 | 低处平均 0.23 mm，叶尖平均 7.12 mm。沿叶长分 a/b 骨，根茎不摆。 |
| v02_java_alphaclip | pl_v0_2_java_fern_proxy | 已过（glTF MASK + doubleSided + 有效 alpha） | 待引擎验证 URP 双面 Alpha Clip | leaf mat={'name': 'mat_java_fern_leaf', 'alphaMode': 'MASK', 'alphaCutoff': 0.4, 'doubleSided': True, 'has_baseColorTexture': True, 'has_normalTexture': True, 'has_mrTexture': True, 'baseColorFactor': [1.0, 1.0, 1.0, 1.0], 'baseColor_tex': {'name': 'java_fern_leaf_bc', 'is_png': True, 'size': [512, 512], 'alpha_min': 0, 'alpha_max': 255, 'frac_a0': 0.22156143188476562, 'frac_a255': 0.6785392761230469, 'frac_a_lt_102': 0.25753021240234375, 'unique_alpha': 256}} |
| unity_urp_visual | all | 未做，不签字 | 未做 | 本次无 Unity URP 实机画面。玻璃/水体/近景/植被透明叠加以引擎验收为准。软件光栅预览明确非引擎。 |
| license_source | v0.2 new plants + copied fish reference | 已过（来源声明准确，无排他权承诺） | N/A | 新网格与贴图为本次原创程序化。第三方运行库：numpy 2.5.4 (BSD)、Pillow 13.0.0 (HPND)、pygltflib 1.16.5 (MIT)。不声称独家版权或商业素材授权。复制的鱼 GLB 与 v0 哈希一致，未改。 |

## 明确未通过 / 未做

- Unity URP import and visuals were not run and are not signed off.
- Species reconstruction is not claimed and not passed.
- pl_fg_v0_carpet_proxy as plant_anubias_nana is unqualified.
- pl_md_v0_leaf_proxy as plant_java_fern silhouette is unqualified.
- turn_left is in-place bone bend returning to bind, not a spatial turn.
- Plant sway samples are not water-physics simulations.

## 首批包哈希

检查 70 个文件；mismatches=[]；unchanged=True。
基线见 `docs/v0_baseline_sha256.txt`。
