# 水族库与饲养决策信息升级

日期：2026-11-06。用户已批准上一轮产品审查建议，直接实施。本文件是实施与验收基线。

## 范围与分工

- 保持无构建依赖的静态网站、10 个示例物种、英语与简体中文。计算器继续 P1 占位，不新增算法。
- 数据 Sol：只改 data/、schemas/、scripts/；前台 Sol：只改 app.js、styles.css、index.html；QA Sol：tests/、验收报告、README。负责人管理集成、范围及人工验收。
- 不新增来源/许可登记，不做疾病诊断、统一检疫天数、统一换水比例或加热功率公式。繁殖细分、寿命、成熟月龄继续后续阶段。

## 分类与列表

- 中文导航/页面/返回统一“水族库”，英文为“Aquatic life”。仍只收录淡水生物。
- 顶部类型：全部、鱼类、虾类、螺类。只显示已有内容的具体类型；直达没有内容的合法类型显示真实空态。“全部”只清 type，“清空”清全部过滤。类型切换时清理不适用的 category，保留其余可适用条件；URL、条件标签、计数同步。
- type 使用单值 organism_type；category 是独立的爱好者分类多选。热带/冷水不增加互斥导航，具体温区继续筛选。body_shape 不再包含 bottom_dweller，底栖只用于水层。
- 桌面宽视口（建议 >=1280）结果三列，普通桌面两列，小屏一列。内容区上限约1320，保持卡片足够宽，中英文长名字均无裁切/溢出。筛选侧栏在小屏可折叠。
- 卡片保留名称/学名/主要分类，优先显示精确成体体长、最小缸体、水温、群体要求。颜色/繁殖等次要信息按生效条件展示，避免堆标签。成体筛选使用厘米区间，去掉 >8cm 等于“大型”的全局标签。
- 暂无照片/占位状态诚实展示并压缩空白，不生成物种图片。

## 稳定数据契约（前后端共同遵循）

保留既有字段，增加以下结构。所有值未知时用 null/unknown/空数组，不能为了展示编造数值。

species.organism_type: fish | shrimp | snail。

care_profiles.water_layers: 非空数组，top/mid/bottom；保留 water_layer 为主水层且必须在数组内。筛选匹配数组，显示可组合为中上层/中下层。

care_profiles.husbandry:

```text
oxygen_need: standard | high | unknown
flow_preference: [minimal | gentle | moderate | strong]（可多选，未知空数组）
minimum_tank_length_cm: 正数 | null
group_mode: shoal | single | conditional_pair | loose_group | colony | unknown
light_preference: subdued | moderate | adaptable | unknown
substrate_tags: [smooth_sand | smooth_gravel | optional]（未知空数组）
habitat_tags: [plants | surface_cover | hiding_places | wood | open_swimming | mature_biofilm]
feeding_zone: [surface | midwater | bottom]
food_form_tags: [small_particles | floating | sinking | vegetable | biofilm]
notes: { en: {oxygen, flow, space, group, feeding, setup}, zh-CN: 同结构 }
alerts: [{ code, severity: caution|important, text: {en, zh-CN} }]
```

- oxygen_need 的 standard 意为保持充足溶氧，不表示低氧安全或不用增氧。水流与溶氧独立。flow 的 minimal 意为弱流，不鼓励停水/缺氧。
- group_mode 决定如何解释已有 school_min 与 school_recommended；shoal 显示至少与建议，colony/loose_group 显示建议起养，single 显示单养，conditional_pair 显示可单养/有条件成对并附物种说明。不把所有物种统一成6尾。
- notes 六个键为简短实操说明，可空字符串；有特殊判断时必须有本地化说明。alerts 只针对适用物种，最多3个显著提醒，避免无差别警告。code 为稳定短键，不包含未经核实的诊断。
- minimum_tank_length_cm 只填有把握的值，斑马鱼已有约60cm说明可结构化；其余不确定留空，不从升数倒推。空间正文可说明底面积和游泳空间。
- 新增字段不生成/改变 calculator 数值。原 calculator_enabled=false 的条目继续禁用，未知数值 null。
- 新增简短 data/care_guides.json，结构为 [{id, title:{en,zh-CN}, body:{en,zh-CN}}]。只含新鱼观察与隔离、水质与换水两个共用指南；不承诺短期观察即检疫完成，不写疾病诊断和药量，不把静置当作除氯胺办法。
- 新增字段的中英文标签由前台字典呈现；既有 filter_labels 保持对应。可为斗鱼增加 category=betta 受控值与标签。

## 详情与交互

- 顺序：身份与图片 → 基础条件（体长只出现一次） → 饲养环境（水流/溶氧/空间/群体） → 喂食与布置 → 混养与适用提醒 → 可折叠的通用健康/维护指南。
- 风险短句默认可见，其余说明用可点击/键盘展开的 details，不只依赖 hover。
- 推荐群养数读取现有数据。敏感内容仅作条件说明，不用单一“温和”代替幼虾捕食、咬鳍、同种领地等风险。
- 前台新增水流与溶氧筛选，放更多筛选中；具体物种资料不足则排除明确条件，并在详情显示待补充。延续同维OR、跨维AND规则。
- 不再把 calculator_data_status 渲染成全条目的内容状态。field_status 的待确认值如有展示必须指向具体参数；没有信息不添加“已核实”保证。
- 分类/筛选/排序/语言继续 URL 持久化；进入详情携带安全的本站列表上下文，页面内返回与浏览器后退恢复条件及列表滚动位置。拒绝外部 return URL。
- 首页快捷入口接到有效筛选，文案与水族库统一。旧URL继续可用。

## 验收

1. 中英类型分组、相关category、全部与清空区别、真实空态、组合/未知值、URL及排序通过。
2. 1440/1280三列、1024两列、390中英单列及长名字无溢出；不通过缩小正文把卡片塞进去。
3. 基础卡片有体长/水温/缸体/群体，颜色/繁殖命中条件可见，非当前条件不堆满卡片。
4. 每种类型的群体文案正确：霓虹灯至少/建议，蜜丽丽条件成对，斗鱼单养，虾建议群体。氧气和水流可独立筛选，不互相推断。
5. 详情无重复体长或calculator状态误标；喂食/环境/关键提醒可读且无统一疾病阈值、检疫7天或固定W/L建议。
6. 列表→详情→页面内返回恢复筛选/排序/滚动；切语/刷新/前进后退稳定。重要提示键盘/触屏可见。
7. Node/Python数据校验、JS语法、已有筛选回归、独立浏览器验收通过；负责人检查截图和实际路径。
