# 核验范围说明

指定上传目录（先列）：
`/cursor/stores/store-fff5df73-2e50-4001-986d-a20c3c287d50/grok-bot/agents/e6c065bb-54cf-4ce5-b8d3-60ce790921e1/attachments/`
**不存在**（ls 报 No such file or directory）。

同级实际存在的是 `assets/`（12 张 JPEG）。

3D 搜索（rg --files）：`/workspace`、`/home/box/agent-data`、本 bot store。扩展名含 blend/fbx/glb/gltf/obj/usd/usda/usdc/abc/dae。在本次写入 `/workspace/aquarium-art-v0` 之前，上述范围内 **0 个命中**。

未扫描其他用户计算机、未读取凭据、未把「范围内未找到」说成全球不存在。
