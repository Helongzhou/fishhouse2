# 来源与第三方许可清单

## 本目录新资产
- `pl_v0_2_anubias_proxy`、`pl_v0_2_java_fern_proxy`、对比场景、新建程序化贴图与生成脚本：本次原创程序化生成，供本项目作为技术代理使用。
- **不声称**：独家版权、对真实物种的权利、对第三方扫描/商店模型的授权、或「已取得水榕/铁皇冠商业外观授权」。

## 从首批复制、声明未改
- `export/glb/ref_unchanged_from_v0/fh_v0_tetra_proxy_L0.glb`：逐字节复制自 `/workspace/aquarium-art-v0/export/glb/fh_v0_tetra_proxy_L0.glb`，SHA-256 一致，仅作对照，未改网格或动画。

## 生成时使用的库（venv 运行，不打进美术包授权转移）
| 库 | 本机版本 | 许可（常见声明，以上游为准） |
|---|---|---|
| Python | 3.13.5 | PSF |
| numpy | 2.5.4 | BSD |
| Pillow | 13.0.0 | HPND / MIT-CMU |
| pygltflib | 1.16.5 | MIT |

`source/glb_writer.py` 与 `source/mesh_lib.py` 复制自 v0 源码（哈希与首批相同），仅存在于本独立目录。
