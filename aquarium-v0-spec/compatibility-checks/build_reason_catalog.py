"""Build the compatibility-owned localization proposal; never modifies shared catalogs."""
import json
from pathlib import Path
entries = {
'ADULT_MALE_BETTA_CONSPECIFIC_BLOCK': (
'Multiple adult male {source} cannot share this tank.',
'House adult males in separate suitable systems. Do not rely on a larger group or individual temperament.',
'多只同物种成年雄性{source}不能共缸。', '为成年雄鱼安排独立且合适的系统，不以增加数量或赌个性代替隔离。'),
'ADULT_SPACE_INSUFFICIENT': (
'The adult space requirement for {source} is not met at {target}.',
'Use a tank that meets adult length, height and usable swimming-space requirements, or choose a suitable species.',
'{target}未满足{source}的成体空间要求。', '换用满足成体长度、高度和可游空间的缸，或选择适合现有缸的物种。'),
'ADULT_SPACE_UNKNOWN': (
'The adult space requirement or available space for {source} at {target} is unverified.',
'Confirm adult requirements and usable space after substrate and hardscape displacement. Juveniles still require an adult plan.',
'{source}在{target}的成体要求或可用空间未核实。', '核验成体要求及扣除底床、硬景后的可游空间；幼鱼也需成体规划。'),
'BETTA_CONSPECIFIC_CONDITIONS_REVIEW': (
'No multiple-adult-male rule was triggered for {source}, but this same-species group still has unmet review conditions.',
'Review individuals, sex, space, cover and management. Prepare immediate separation; V0 defaults to one individual.',
'{source}未触发多成年雄鱼规则，但同类组合仍有待审核条件。', '审核个体、性别、空间、遮挡与管理条件并准备立即隔离；V0默认采用单只方案。'),
'BETTA_SEX_OR_STAGE_UNKNOWN': (
'Sex or life stage is unknown for {source}; adult male conflict cannot be ruled out.',
'Confirm the sex and life stage of every individual before approving a same-species combination.',
'{source}的性别或生命阶段未知，不能排除同类成年雄鱼冲突。', '核实每个个体的性别和生命阶段后，再评估同类组合。'),
'BOTTOM_SIFTER_FOOD_UNKNOWN': (
'Appropriate sinking food for {source} has not been confirmed.',
'Provide species-appropriate sinking food. Feces and leftovers are not an adequate feeding plan.',
'{source}的适口沉底饲料供应未核实。', '确认针对该物种的适口沉底饲料，不能依赖粪便或其他鱼的剩食。'),
'BOTTOM_SIFTER_NEEDS_SINKING_FOOD': (
'No appropriate sinking-food plan is present for {source}.',
'Provide suitable sinking food and observe feeding. Bottom-sifting fish are not feces cleaners.',
'{source}没有适口沉底饲料计划。', '提供适合该物种的沉底饲料并观察摄食；底栖翻砂鱼不是排泄物清洁工。'),
'BOTTOM_SIFTER_NEEDS_SOFT_SAND': (
'The modeled soft-sand foraging requirement of {source} is unmet.',
'Provide a safe, accessible soft-sand foraging area and reassess. Sifting does not remove the need for waste management.',
'{source}的软砂觅食需求未满足。', '提供安全可达的软砂觅食区后重评；翻砂不能替代废物处理。'),
'BOTTOM_SIFTER_SUBSTRATE_UNKNOWN': (
'The foraging substrate for {source} is unverified.',
'Confirm a safe soft-sand substrate for this bottom-sifting profile.',
'{source}的觅食底材未核实。', '确认该翻砂物种有可供安全觅食的软砂底材。'),
'COMMUNITY_RANGE_EMPTY': (
'The whole-community water-parameter intersection is empty; {source} and {target} define conflicting bounds.',
'Separate incompatible species or remove a conflicting member, then recompute the entire combination. A freshwater label is insufficient.',
'全组合的水参数交集为空；{source}与{target}形成冲突边界。', '分缸或移除冲突物种，再对完整组合求交；同属淡水不能代替参数兼容。'),
'COMMUNITY_WATER_TYPE_MISMATCH': (
'{source} and {target} require different water systems.',
'Separate freshwater, brackish and saltwater requirements into suitable systems; do not invent a compromise water type.',
'{source}和{target}需要不同水型系统。', '按淡水、汽水、海水要求分开建立系统，不用折中水型解决。'),
'CONTENT_POLICY_UNKNOWN': (
'The V0 purchasing policy for {source} is unknown.',
'Have the content owner confirm purchasable_v0, planned or reference_only.',
'{source}的V0购买政策未核实。', '由内容负责人确认purchasable_v0、planned或reference_only。'),
'DIRECTIONAL_FIN_NIPPING_RISK': (
'{source} may nip the fins of {target}; source group size is {source_count}. This is a risk, not a guaranteed attack.',
'Prefer separating the fin-nipping source and long-finned target. Review group size, space and stress; adequate numbers do not guarantee safety.',
'{source}可能咬伤{target}的长鳍；来源同种数量为{source_count}。这是风险，不等于必然攻击。', '优先分开咬鳍来源与长鳍对象；核验群体、空间和压力，数量达标也不保证安全。'),
'GAPE_PREDATION_RISK': (
'The gape of {source} is not smaller than the body cross-section of {target}; size-related predation is possible.',
'Separate potential prey or use a dedicated nursery. Cover does not guarantee survival; reassess as animals grow.',
'{source}的口裂不小于{target}的身体横截面，存在尺寸相关捕食风险。', '隔离潜在猎物或使用独立育幼系统；遮挡不保证存活，并需随生长重评。'),
'INCONSISTENT_SPECIES_PROFILE': (
'The same species ID {source} uses inconsistent profile instances.',
'Reference one reviewed profile version for all cohorts of the same species.',
'同一物种ID {source}使用了不一致的配置实例。', '同一物种的所有群组引用同一份已核验配置版本。'),
'INVALID_STOCK_GROUP': (
'{target} has a missing species/group ID, duplicate group ID, or a non-positive count.',
'Provide a reviewed species, unique group ID and positive integer count, then reassess.',
'{target}存在物种/群组ID缺失、群组ID重复或非正数量。', '补齐已核验物种、唯一群组ID及正整数数量后重新评估。'),
'NO_VALID_STOCK': (
'No valid stock group is available for assessment.',
'Add at least one reviewed species cohort; an empty or invalid composition is not an approval.',
'没有可用于评估的有效生物群组。', '添加至少一个已核验生物群组；空组合或无效组合不表示通过。'),
'PREDATION_PROFILE_UNKNOWN': (
'The ability of {source} to prey on smaller animals is unverified.',
'Confirm diet and size-related predation capability. Do not infer safety or total loss from fish/shrimp categories.',
'{source}捕食较小动物的能力未核实。', '核验食性及尺寸相关捕食能力；不从鱼虾分类推断安全或全灭。'),
'PREDATION_SIZE_UNKNOWN': (
'Gape or current body cross-section data for {source} and {target} is missing; swallowing risk is unresolved.',
'Confirm current-stage gape and body cross-section, and separate potential prey until verified.',
'{source}与{target}的口裂或当前身体横截面数据缺失，吞食风险待核实。', '核验当前生命阶段的口裂和身体横截面；未知前将潜在猎物分开。'),
'SAME_SPECIES_GROUP_TOO_SMALL': (
'{source} has {source_count} individuals, below its recommended same-species minimum of {required_count}. Other fish or shrimp do not count.',
'After reassessing space, filtration and conflicts, add the same species or choose a species with different social requirements.',
'{source}同种数量为{source_count}，低于建议的{required_count}；其他鱼虾不能代替。', '空间、过滤及冲突重新评估通过后补足同种，或选择群居要求不同的物种。'),
'SPECIES_GROUP_MIN_UNKNOWN': (
'The same-species group requirement for {source} is unknown.',
'Verify social and conspecific-isolation requirements instead of substituting other species.',
'{source}的同种群体数量要求未核实。', '核验群居、独居及同类隔离要求，不能用其他物种替代。'),
'SPECIES_RANGE_UNKNOWN': (
'The reviewed {parameter} range for {source} is missing or invalid.',
'Confirm the complete species range and reassess. Do not treat unknown data as a compatible freshwater default.',
'{source}已核验的{parameter}区间缺失或无效。', '核验完整区间后重评；不能把未知值当作兼容的淡水默认值。'),
'SPECIES_WATER_TYPE_UNKNOWN': (
'The water system required by {source} is unverified.',
'Confirm the species water system; unknown must not default to freshwater.',
'{source}所需水型未核实。', '确认该物种水型，不能将未知默认成淡水。'),
'TANK_PARAMETER_OUTSIDE_SPECIES_RANGE': (
'The current tank {parameter} is outside the reviewed range for {source}.',
'If a whole-community range exists, adjust gradually into its interior. Otherwise separate species; avoid sudden parameter changes.',
'当前缸的{parameter}不在{source}的已核验区间内。', '若全组合有共同安全区间，渐进调整至其内部；否则分缸，避免参数突变。'),
'TANK_PARAMETER_UNKNOWN': (
'The current tank measurement for {parameter} is missing or invalid.',
'Measure and record the current value; compatible species ranges do not prove current water conditions are suitable.',
'当前缸的{parameter}测量值缺失或无效。', '测量并录入当前值；物种区间兼容不代表当前水质合格。'),
'TANK_WATER_TYPE_MISMATCH': (
'{source} requires a different water system from the current tank.',
'Move the species into a suitable independent water system.',
'{source}所需水型与当前缸不符。', '将该物种移入匹配水型的独立系统。'),
'TANK_WATER_TYPE_UNKNOWN': (
'The current tank water system is unknown.',
'Confirm whether this is a freshwater, brackish or saltwater system.',
'当前缸水型未知。', '确认当前为淡水、汽水或海水系统。'),
'V0_CONTENT_NOT_PURCHASABLE': (
'{source} is planned or reference-only and cannot be purchased in V0. This is content policy, not an ecological incompatibility claim.',
'Remove it from the V0 purchase list or wait for an approved content version.',
'{source}为planned或reference_only，V0不开放购买；这是内容政策，不是生态不兼容断言。', '从V0购物清单移除，或等待审核后的内容版本开放。'),
}
result={'en':{},'zh-CN':{}}
for code,(em,er,zm,zr) in entries.items():
    for lang,msg,repair in [('en',em,er),('zh-CN',zm,zr)]:
        result[lang]['compatibility.reason.'+code+'.message']=msg
        result[lang]['compatibility.reason.'+code+'.repair']=repair
result['en']['compatibility.summary.monitor']='No modeled conflict found. Quarantine, staged introduction, monitoring and an isolation plan are still required.'
result['en']['compatibility.summary.action_required']='Review every blocking, risk, unmet-condition and unverified-data reason. Resolve and reassess before introduction.'
result['zh-CN']['compatibility.summary.monitor']='未发现已建模的冲突；仍须检疫、分批入缸、持续观察并准备隔离。'
result['zh-CN']['compatibility.summary.action_required']='请查看所有阻止、风险、未满足条件及待核实原因，修复后重新评估。'
path=Path(__file__).with_name('reason-catalog.json')
path.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
print(str(path), len(entries),'codes;',len(result['en']),'keys per language')
