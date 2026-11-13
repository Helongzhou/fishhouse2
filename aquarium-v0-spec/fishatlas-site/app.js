const dataFiles = {
  species: "data/species.json",
  localizations: "data/species_localizations.json",
  care: "data/care_profiles.json",
  compatibility: "data/compatibility_rules.json",
  notes: "data/content_notes.json",
  media: "data/media_assets.json",
  guides: "data/care_guides.json",
};
const releaseVersion = new URL(import.meta.url).searchParams.get("v");

const strings = {
  en: {
    navDatabase: "Database", navCalculator: "Tank calculator", language: "Language", footerText: "A calm reference for freshwater keepers.", footerNote: "Personal notes, shared for fellow aquarists.",
    eyebrow: "Freshwater species reference", heroTitle: "Build a calmer tank.", heroBody: "Look up the fish you are considering, compare the conditions, and make a stocking plan that fits your aquarium.", searchPlaceholder: "Search common name, scientific name or alias", search: "Search", featured: "Start with a species", featuredBody: "A small, practical set of community fish and shrimp to explore.", browseAll: "Browse all", chooseBy: "Choose by situation", chooseByBody: "Use the details that matter when you are standing in front of a tank.", smallTank: "Small tanks", smallTankText: "Start with species whose minimum tank size stays within your plan.", schooling: "Schooling fish", schoolingText: "Check group size before you choose a single show fish.", layers: "Cover every layer", layersText: "Balance top, middle and bottom activity in one layout.", database: "Species database", results: "species", filters: "Filters", clear: "Clear", apply: "Apply", difficulty: "Difficulty", allDifficulties: "All difficulties", easy: "Easy", moderate: "Moderate", hard: "Hard", temperature: "Temperature °C", minTank: "Minimum tank L", waterLayer: "Water layer", allLayers: "All layers", top: "Top", mid: "Middle", bottom: "Bottom", noResults: "No species match these filters.", resetFilters: "Reset filters", overview: "Overview", parameters: "Parameters", care: "Care notes", compatibility: "Mixing notes", school: "Group", adultLength: "Adult length", water: "Water", pH: "pH", hardness: "GH", diet: "Diet", temperament: "Temperament", tank: "Minimum tank", equipmentBaseline: "Baseline setup", status: "Content status", updated: "Updated", peaceful: "Peaceful", omnivore: "Omnivore", grazer: "Grazer", ready: "Ready", partial: "Partial", needsReview: "Needs review", dataShort: "Data incomplete", formalImage: "Photo", placeholder: "Placeholder image", noPhoto: "No photo yet", backToDatabase: "Back to database", p1Eyebrow: "Planned feature", calculatorTitle: "Plan a tank around your fish", calculatorBody: "The calculator will let you enter your tank and add species by quantity. It will check space, group size, water ranges, layers and mixing risk.", calculatorPoint1: "Enter tank dimensions or usable litres", calculatorPoint2: "Add species and quantities", calculatorPoint3: "See each warning and the reason behind it", comingSoon: "Coming in P1", noteLabel: "Editor note", noNote: "No extra note yet.", compatible: "Compatible", caution: "Caution", avoid: "Avoid", unknown: "Unknown", reason: "Reason", conditions: "When it works", viewDetails: "View details", searchResults: "Search results", speciesCount: "species", contentStatus: "Content status", dataShortHint: "Some fields still need review.", unknownValue: "Not set", zone: "Layer"
  },
  "zh-CN": {
    navDatabase: "鱼种库", navCalculator: "鱼缸计算器", language: "语言", footerText: "给淡水缸玩家的实用整理。", footerNote: "个人整理，也分享给同好。",
    eyebrow: "淡水观赏鱼整理", heroTitle: "先把缸想清楚。", heroBody: "查品种、看参数、做混养判断，让每一次入缸都更有把握。", searchPlaceholder: "搜索中文名、学名或别名", search: "搜索", featured: "从一个品种开始", featuredBody: "先看看这组常见的社区鱼和虾。", browseAll: "查看全部", chooseBy: "按你的情况找", chooseByBody: "从真正影响养鱼体验的条件开始筛选。", smallTank: "小缸", smallTankText: "先看最小缸体不超过你计划的品种。", schooling: "群游鱼", schoolingText: "群养数量比“买一条试试”更重要。", layers: "照顾不同水层", layersText: "让上层、中层和底层都有合适的活动空间。", database: "鱼种库", results: "个品种", filters: "筛选", clear: "清空", apply: "应用", difficulty: "难度", allDifficulties: "全部难度", easy: "容易", moderate: "中等", hard: "较难", temperature: "水温 °C", minTank: "最小缸体 L", waterLayer: "水层", allLayers: "全部水层", top: "上层", mid: "中层", bottom: "底层", noResults: "没有符合这些条件的品种。", resetFilters: "重置筛选", overview: "概览", parameters: "参数", care: "饲养笔记", compatibility: "混养笔记", school: "群养", adultLength: "成体体长", water: "水温", pH: "pH", hardness: "GH", diet: "食性", temperament: "性情", tank: "最小缸体", equipmentBaseline: "基础布置", status: "内容状态", updated: "更新时间", peaceful: "温和", omnivore: "杂食", grazer: "刮食", ready: "已整理", partial: "部分整理", needsReview: "待确认", dataShort: "数据不完整", formalImage: "正式图片", placeholder: "占位图", noPhoto: "暂无照片", backToDatabase: "返回鱼种库", p1Eyebrow: "计划中的功能", calculatorTitle: "围绕你的鱼规划鱼缸", calculatorBody: "鱼缸计算器会让你输入缸体并添加鱼种和数量，检查空间、群养、水质范围、水层和混养风险。", calculatorPoint1: "输入缸体尺寸或可用水量", calculatorPoint2: "添加鱼种和数量", calculatorPoint3: "看到每条提醒及其原因", comingSoon: "P1 计划", noteLabel: "整理备注", noNote: "暂时没有额外备注。", compatible: "可以尝试", caution: "需要谨慎", avoid: "不建议", unknown: "数据不足", reason: "原因", conditions: "适用条件", viewDetails: "查看详情", searchResults: "搜索结果", speciesCount: "个品种", contentStatus: "内容状态", dataShortHint: "部分字段还需要确认。", unknownValue: "未填写", zone: "水层"
  }
};

const filterText = {
  en: {
    category: "Category", color: "Color", size: "Adult size", reproduction: "Reproduction", social: "Social pattern", body: "Body shape", temperament: "Temperament", more: "More filters", presets: "Quick picks", group: "Group", selected: "selected", active: "active", showFilters: "Filters", hideFilters: "Hide filters", tankVolume: "My tank volume L", rangeHint: "Matches when ranges overlap; check the full care notes.", sort: "Sort", sortDefault: "Default", sortLength: "Adult length: small first", sortTank: "Tank size: low first", rangeError: "Minimum cannot exceed maximum", numericError: "Use 0–40 °C, pH 0–14, and tank volume of at least 1 L.",
    smallTank: "Small tank", gentleSchool: "Gentle schooling", topFish: "Top layer", midSchool: "Mid-water school", bottomFish: "Bottom dwellers", livebearer: "Livebearers",
    categories: { tetra: "Tetra", rasbora: "Rasbora", barb: "Barb", danio: "Danio", corydoras: "Corydoras", gourami: "Gourami", cichlid: "Cichlid", livebearer: "Livebearer", pleco: "Pleco", catfish: "Catfish", shrimp: "Shrimp", snail: "Snail", other: "Other" },
    colors: { red: "Red", orange: "Orange", yellow: "Yellow", blue: "Blue", green: "Green", black: "Black", white: "White", silver: "Silver", transparent: "Transparent", brown: "Brown", mixed: "Mixed" },
    sizes: { small: "≤4 cm", medium: "4–8 cm", large: ">8 cm" },
    reproductions: { egg_laying: "Egg-laying", livebearing: "Livebearing", unknown: "Unknown" },
    socials: { schooling: "Schooling", pairing: "Pairing", solitary_tendency: "Solitary tendency", colony: "Colony", unknown: "Unknown" },
    bodies: { slender: "Slender", deep_bodied: "Deep-bodied", laterally_compressed: "Laterally compressed", bottom_dweller: "Bottom dweller", short_round: "Short and round", elongated: "Elongated", unknown: "Unknown" },
    temperaments: { peaceful: "Peaceful", community: "Community", territorial: "Territorial", aggressive: "Aggressive" }
  },
  "zh-CN": {
    category: "品种大类", color: "主色", size: "成体大小", reproduction: "繁殖方式", social: "社交方式", body: "体型", temperament: "性情", more: "更多筛选", presets: "快速选择", group: "群养", selected: "已选", active: "项生效", showFilters: "筛选条件", hideFilters: "收起筛选", tankVolume: "我的鱼缸水量 L", rangeHint: "区间有交集即显示；请查看完整饲养条件。", sort: "排序", sortDefault: "默认", sortLength: "成体从小到大", sortTank: "缸体需求从低到高", rangeError: "最小值不能大于最大值", numericError: "水温须为 0–40 °C、pH 为 0–14，鱼缸水量至少 1 L。",
    smallTank: "小缸", gentleSchool: "温和群游", topFish: "上层鱼", midSchool: "中层群游", bottomFish: "底栖伙伴", livebearer: "想养胎生鱼",
    categories: { tetra: "灯科鱼", rasbora: "波鱼类", barb: "鲃类", danio: "斑马鱼类", corydoras: "鼠鱼", gourami: "迷鳃类", cichlid: "慈鲷", livebearer: "胎生鳉", pleco: "异型", catfish: "鲶鱼类", shrimp: "虾", snail: "螺", other: "其他" },
    colors: { red: "红色", orange: "橙色", yellow: "黄色", blue: "蓝色", green: "绿色", black: "黑色", white: "白色", silver: "银色", transparent: "透明", brown: "棕色", mixed: "混色" },
    sizes: { small: "小型 ≤4 cm", medium: "中型 4–8 cm", large: "大型 >8 cm" },
    reproductions: { egg_laying: "卵生", livebearing: "胎生", unknown: "未知" },
    socials: { schooling: "群游", pairing: "成对", solitary_tendency: "单养倾向", colony: "群体", unknown: "未知" },
    bodies: { slender: "细长", deep_bodied: "深体", laterally_compressed: "侧扁", bottom_dweller: "底栖", short_round: "短圆", elongated: "延长", unknown: "未知" },
    temperaments: { peaceful: "温和", community: "社区缸", territorial: "有领地性", aggressive: "攻击性强" }
  }
};
Object.assign(strings.en, {
  navDatabase: "Aquatic life", database: "Aquatic life", backToDatabase: "Back to aquatic life",
  careConditions: "Basic conditions", environment: "Habitat & group", feedingSetup: "Feeding & setup", mixingRisks: "Mixing & cautions", commonGuides: "General care guides", oxygen: "Oxygen", flow: "Water flow", space: "Swimming space", group: "Group", feeding: "Feeding", setup: "Setup", minimumLength: "Minimum tank length", noEntries: "No entries in this group yet.", seeAll: "Show all", noConditions: "Details to be added", moreDetail: "Care details", important: "Important", caution: "Caution"
});
Object.assign(strings["zh-CN"], {
  navDatabase: "水族库", database: "水族库", backToDatabase: "返回水族库",
  careConditions: "基础条件", environment: "饲养环境与群体", feedingSetup: "喂食与布置", mixingRisks: "混养与提醒", commonGuides: "通用养护指南", oxygen: "溶氧", flow: "水流", space: "活动空间", group: "群体", feeding: "喂食", setup: "布置", minimumLength: "最短缸长", noEntries: "该类型暂未收录物种。", seeAll: "查看全部", noConditions: "资料待补充", moreDetail: "详细说明", important: "重要", caution: "注意"
});
Object.assign(filterText.en, { organismType: "Type", allTypes: "All", types: { fish: "Fish", shrimp: "Shrimp", snail: "Snail" }, oxygen: "Oxygen need", oxygenOptions: { standard: "Standard", high: "High" }, flow: "Water flow", flowOptions: { minimal: "Minimal", gentle: "Gentle", moderate: "Moderate", strong: "Strong" } });
Object.assign(filterText["zh-CN"], { organismType: "类型", allTypes: "全部", types: { fish: "鱼类", shrimp: "虾类", snail: "螺类" }, oxygen: "溶氧需求", oxygenOptions: { standard: "常规", high: "较高" }, flow: "水流", flowOptions: { minimal: "弱流", gentle: "缓流", moderate: "中等", strong: "较强" } });
filterText.en.sizes = { small: "≤4 cm", medium: "4–8 cm", large: ">8 cm" };
filterText["zh-CN"].sizes = { small: "≤4 cm", medium: "4–8 cm", large: ">8 cm" };
filterText.en.bodies = Object.fromEntries(Object.entries(filterText.en.bodies).filter(([key]) => key !== "bottom_dweller"));
filterText["zh-CN"].bodies = Object.fromEntries(Object.entries(filterText["zh-CN"].bodies).filter(([key]) => key !== "bottom_dweller"));
filterText.en.categories.betta = "Betta";
filterText["zh-CN"].categories.betta = "斗鱼";
const multiFilterFields = { category: "category_tags", color: "primary_colors", size: "size_class", reproduction: "reproduction_mode", social: "social_pattern", body: "body_shape_tags" };
const filterOptionKeys = { category: "categories", color: "colors", size: "sizes", reproduction: "reproductions", social: "socials", body: "bodies" };
const state = { locale: "en", view: "home", species: [], localizations: [], care: [], compatibility: [], notes: [], media: [], guides: [], filters: {}, draft: null };
const t = (key) => (strings[state.locale] || strings.en)[key] || strings.en[key] || key;
const ft = (key) => filterText[state.locale][key];
function optionLabel(field, value) {
  const key = multiFilterFields[field];
  const labelKey = { category: "categories", color: "colors", reproduction: "reproduction", social: "social", body: "body_shape" }[field];
  if (labelKey) for (const item of state.species) {
    const raw = Array.isArray(item[key]) ? item[key] : [item[key]];
    const index = raw.indexOf(value);
    if (index < 0) continue;
    const labels = localize(item.id).filter_labels?.[labelKey];
    const label = Array.isArray(labels) ? labels[index] : labels;
    if (label) return label;
  }
  return ft(filterOptionKeys[field])[value] || value;
}
function sizeShortLabel(value) { return { en: { small: "Small", medium: "Medium", large: "Large" }, "zh-CN": { small: "小型", medium: "中型", large: "大型" } }[state.locale][value] || t("unknownValue"); }
function availableOptions(field) {
  const key = multiFilterFields[field];
  const species = field === "category" && state.filters.type ? state.species.filter((item) => organismType(item) === state.filters.type) : state.species;
  const values = new Set(species.flatMap((item) => Array.isArray(item[key]) ? item[key] : [item[key]]).filter(Boolean));
  return Object.keys(ft(filterOptionKeys[field])).filter((value) => values.has(value));
}
function organismType(item) { return item.organism_type || (item.category_tags?.includes("shrimp") ? "shrimp" : item.category_tags?.includes("snail") ? "snail" : "fish"); }
function localized(value) { return typeof value === "object" && value !== null ? value[state.locale] || value.en || "" : value || ""; }

async function loadData() {
  const entries = await Promise.all(Object.entries(dataFiles).map(async ([key, path]) => {
    const url = new URL(path, import.meta.url);
    if (releaseVersion) url.searchParams.set("v", releaseVersion);
    const response = await fetch(url, { cache: "no-store" });
    if (key === "guides" && !response.ok) return [key, []];
    if (!response.ok) throw new Error(`${path}: ${response.status}`);
    return [key, await response.json()];
  }));
  entries.forEach(([key, value]) => { state[key] = value; });
}

function parseUrl() {
  const query = new URLSearchParams(location.search);
  const values = (key) => [...new Set(query.getAll(key).flatMap((part) => part.split(",")).map((value) => value.trim()).filter(Boolean))];
  const enumValue = (key, options) => values(key).find((value) => Object.hasOwn(options, value)) || "";
  const numericValue = (key) => values(key).join(",");
  state.locale = query.get("lang") === "zh-CN" ? "zh-CN" : "en";
  if (query.get("species")) state.view = "detail";
  else if (query.get("view") === "database") state.view = "database";
  else if (query.get("view") === "calculator") state.view = "calculator";
  else state.view = "home";
  state.filters = {
    q: query.get("q") || "",
    type: enumValue("type", { fish: true, shrimp: true, snail: true }),
    difficulty: enumValue("difficulty", { easy: true, medium: true, hard: true }),
    tempMin: numericValue("tempMin"),
    tempMax: numericValue("tempMax"),
    phMin: numericValue("phMin"),
    phMax: numericValue("phMax"),
    tank: numericValue("tank"),
    layer: enumValue("layer", { top: true, mid: true, bottom: true }),
    sort: enumValue("sort", { length: true, tank: true }),
    temperament: enumValue("temperament", ft("temperaments")),
    oxygen: enumValue("oxygen", { standard: true, high: true }),
    flow: values("flow").filter((value) => ["minimal", "gentle", "moderate", "strong"].includes(value)),
    ...Object.fromEntries(Object.keys(multiFilterFields).map((field) => [field, values(field).filter((value) => Object.hasOwn(ft(filterOptionKeys[field]), value))]))
  };
  if (state.species.length && state.filters.type) {
    const applicable = new Set(state.species.filter((item) => organismType(item) === state.filters.type).flatMap((item) => item.category_tags || []));
    const valid = state.filters.category.filter((value) => applicable.has(value));
    if (valid.length !== state.filters.category.length) {
      state.filters.category = valid;
      if (valid.length) query.set("category", valid.join(",")); else query.delete("category");
      history.replaceState(history.state, "", `?${query.toString()}`);
    }
  }
}

function localize(speciesId) { return state.localizations.find((item) => item.species_id === speciesId && item.locale === state.locale) || state.localizations.find((item) => item.species_id === speciesId && item.locale === "en") || {}; }
function care(speciesId) { return state.care.find((item) => item.species_id === speciesId) || {}; }
function media(speciesId) { return state.media.find((item) => item.species_id === speciesId) || { image_status: "none" }; }
function notes(speciesId) { return state.notes.filter((item) => item.entity_id === speciesId && item.visibility === "public"); }
function getSpecies(id) { return state.species.find((item) => item.id === id); }
function escapeHtml(value = "") { return String(value).replace(/[&<>'"]/g, (char) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;" }[char])); }
function formatRange(min, max, unit = "") { return `${min ?? "?"}–${max ?? "?"}${unit}`; }
function numberValue(value) { return value === "" || value == null ? null : Number(value); }
function invalidRange(filters) { return (filters.tempMin !== "" && filters.tempMax !== "" && Number(filters.tempMin) > Number(filters.tempMax)) || (filters.phMin !== "" && filters.phMax !== "" && Number(filters.phMin) > Number(filters.phMax)); }
function filterError(filters) {
  for (const [field, min, max] of [["tempMin", 0, 40], ["tempMax", 0, 40], ["phMin", 0, 14], ["phMax", 0, 14], ["tank", 1, Infinity]]) {
    const raw = filters[field];
    if (raw === "") continue;
    const value = Number(raw);
    if (!String(raw).trim() || !Number.isFinite(value) || value < min || value > max) return ft("numericError");
  }
  return invalidRange(filters) ? ft("rangeError") : "";
}
function layerLabel(layer) { return { top: t("top"), mid: t("mid"), bottom: t("bottom") }[layer] || t("unknownValue"); }
function difficultyLabel(value) { return { easy: t("easy"), medium: t("moderate"), hard: t("hard") }[value] || t("unknownValue"); }
function groupLabel(item, profile, detailed = false) {
  const mode = profile.husbandry?.group_mode || (item.social_pattern?.includes("solitary_tendency") ? "single" : item.social_pattern?.includes("pairing") ? "conditional_pair" : organismType(item) === "shrimp" ? "colony" : "shoal");
  const zh = state.locale === "zh-CN";
  const unit = organismType(item) === "fish" ? (zh ? "尾" : "fish") : (zh ? "只" : "individuals");
  if (mode === "single") return zh ? "建议单养" : "Keep singly";
  if (mode === "conditional_pair") return zh ? "单养或有条件成对" : "Single or conditional pair";
  if (mode === "unknown") return t("unknownValue");
  const recommended = profile.school_recommended;
  if (mode === "colony" || mode === "loose_group") return recommended ? (zh ? `建议 ${recommended} ${unit}起` : `Suggested ${recommended}+ ${unit}`) : t("unknownValue");
  if (!profile.school_min) return t("unknownValue");
  const minimum = zh ? `至少 ${profile.school_min} ${unit}` : `At least ${profile.school_min} ${unit}`;
  return detailed && recommended ? `${minimum}${zh ? `，建议 ${recommended} ${unit}` : `; ${recommended} recommended`}` : minimum;
}
function waterLayers(profile) { return profile.water_layers?.length ? profile.water_layers : profile.water_layer ? [profile.water_layer] : []; }
function layersLabel(profile) { return waterLayers(profile).map(layerLabel).join(" / ") || t("unknownValue"); }
function displayValue(value, suffix = "") { return value == null || value === "" ? t("unknownValue") : `${value}${suffix}`; }
function mediaBlock(asset, compact = false) {
  const status = asset.image_status || "none";
  const label = status === "placeholder" ? t("placeholder") : status === "formal" ? t("formalImage") : t("noPhoto");
  const image = status === "formal" && asset.file_url ? `<img src="${escapeHtml(asset.file_url)}" alt="${escapeHtml(asset.alt || "")}">` : "";
  return `<div class="card-media ${status}" aria-label="${escapeHtml(label)}">${image}<span class="media-label">${escapeHtml(label)}</span></div>`;
}
function updateNav() {
  document.querySelectorAll("[data-nav]").forEach((link) => link.classList.toggle("active", link.dataset.nav === state.view));
  document.querySelectorAll("[data-nav]").forEach((link) => { link.href = `?lang=${encodeURIComponent(state.locale)}&view=${encodeURIComponent(link.dataset.nav)}`; });
  const brand = document.querySelector(".brand");
  if (brand) brand.href = `?lang=${encodeURIComponent(state.locale)}&view=home`;
  document.querySelectorAll("[data-locale]").forEach((button) => button.classList.toggle("active", button.dataset.locale === state.locale));
  document.querySelectorAll("[data-i18n]").forEach((node) => { node.textContent = t(node.dataset.i18n); });
}
function queryFor(params) { return new URLSearchParams(Object.entries(params).filter(([, value]) => value !== "" && value != null && (!Array.isArray(value) || value.length)).map(([key, value]) => [key, Array.isArray(value) ? value.join(",") : value])).toString(); }
function listContext() {
  const query = new URLSearchParams(location.search);
  if (query.get("view") === "database") return `?${queryFor({ lang: state.locale, view: "database", ...state.filters })}`;
  return `?${queryFor({ lang: state.locale, view: "database" })}`;
}
function safeReturn() {
  const raw = new URLSearchParams(location.search).get("return");
  if (!raw || !raw.startsWith("?") || raw.startsWith("//")) return `?${queryFor({ lang: state.locale, view: "database" })}`;
  const query = new URLSearchParams(raw.slice(1));
  if (query.get("view") !== "database" || query.has("return") || query.has("species")) return `?${queryFor({ lang: state.locale, view: "database" })}`;
  query.set("lang", state.locale);
  return `?${query.toString()}`;
}
function captureDraft() {
  const form = document.getElementById("filter-form");
  if (!form) return null;
  return { values: Object.fromEntries([...new FormData(form).entries()].filter(([key]) => !Object.keys(multiFilterFields).includes(key) && key !== "flow")), multi: Object.fromEntries([...Object.keys(multiFilterFields), "flow"].map((key) => [key, new FormData(form).getAll(key)])), shellOpen: form.closest("details")?.open, moreOpen: form.querySelector(".advanced-filters")?.open };
}
function restoreDraft(draft, reset = []) {
  const form = document.getElementById("filter-form");
  if (!form || !draft) return;
  for (const [key, value] of Object.entries(draft.values)) if (!reset.includes(key)) { const control = form.elements.namedItem(key); if (control && "value" in control) control.value = value; }
  for (const [key, values] of Object.entries(draft.multi)) if (!reset.includes(key)) form.querySelectorAll(`input[name="${key}"]`).forEach((control) => { control.checked = values.includes(control.value); });
  form.closest("details").open = draft.shellOpen;
  form.querySelector(".advanced-filters").open = draft.moreOpen;
}
function navigate(params, options = {}) {
  const draft = options.preserveDraft ? captureDraft() : null;
  history.replaceState({ ...history.state, scrollY: window.scrollY }, "");
  history.pushState({}, "", `?${queryFor(params)}`);
  parseUrl(); render(); restoreDraft(draft, options.resetDraft || []);
  if (!options.keepScroll) window.scrollTo(0, 0);
}
function openSpecies(event) {
  const link = event.target.closest(".species-card");
  if (!link || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
  event.preventDefault();
  history.replaceState({ ...history.state, scrollY: window.scrollY }, "");
  history.pushState({ returnScrollY: window.scrollY }, "", link.href);
  parseUrl(); render(); window.scrollTo(0, 0);
}
function returnToList(event) {
  event.preventDefault();
  const scrollY = history.state?.returnScrollY || 0;
  history.pushState({ scrollY }, "", safeReturn());
  parseUrl(); render(); requestAnimationFrame(() => window.scrollTo(0, scrollY));
}
function speciesCard(item) {
  const info = localize(item.id); const profile = care(item.id); const asset = media(item.id);
  const labels = info.filter_labels || {};
  const category = (labels.categories || [])[0] || optionLabel("category", item.category_tags?.[0]);
  const extra = [state.filters.color?.length ? (labels.colors || []).join(" / ") : "", state.filters.reproduction?.length ? labels.reproduction || optionLabel("reproduction", item.reproduction_mode) : ""].filter(Boolean);
  const facts = [[t("adultLength"), displayValue(profile.adult_length_cm, " cm")], [t("tank"), displayValue(profile.minimum_tank_l, " L")], [t("water"), formatRange(profile.temperature_min_c, profile.temperature_max_c, "°C")], [t("group"), groupLabel(item, profile)]];
  const href = `?${queryFor({ lang: state.locale, species: item.id, return: listContext() })}`;
  return `<a class="species-card" href="${escapeHtml(href)}">${mediaBlock(asset, true)}<div class="card-body"><div class="card-heading"><h3>${escapeHtml(info.common_name || item.scientific_name)}</h3><span class="tag gold">${escapeHtml(category)}</span></div><span class="latin">${escapeHtml(item.scientific_name)}</span><dl class="card-fact-grid">${facts.map(([label, value]) => `<div><dt>${escapeHtml(label)}</dt><dd>${escapeHtml(value)}</dd></div>`).join("")}</dl>${extra.length ? `<div class="card-extra">${extra.map((value) => `<span>${escapeHtml(value)}</span>`).join("")}</div>` : ""}</div></a>`;
}
function renderHome() {
  const featured = state.species.slice(0, 3);
  const quick = [["smallTank", "?view=database&tank=40"], ["schooling", "?view=database&social=schooling"], ["layers", "?view=database&layer=bottom"]];
  return `<div class="page"><section class="hero"><h1>${t("heroTitle")}</h1><p>${t("heroBody")}</p><form class="search-box" id="hero-search"><input name="q" value="${escapeHtml(state.filters.q)}" placeholder="${escapeHtml(t("searchPlaceholder"))}" aria-label="${escapeHtml(t("searchPlaceholder"))}" /><button>${t("search")}</button></form></section><div class="section-heading"><h2>${t("featured")}</h2><a class="text-link" href="?lang=${encodeURIComponent(state.locale)}&view=database">${t("browseAll")} →</a></div><div class="species-grid">${featured.map(speciesCard).join("")}</div><div class="section-heading"><h2>${t("chooseBy")}</h2></div><div class="decision-strip">${quick.map(([key, href]) => `<a class="decision-card" href="${href}&lang=${encodeURIComponent(state.locale)}"><strong>${t(key)}</strong><span aria-hidden="true">→</span></a>`).join("")}</div></div>`;
}
function filterSpecies() {
  const f = state.filters; const query = f.q.trim().toLowerCase(); const tempMin = numberValue(f.tempMin); const tempMax = numberValue(f.tempMax); const phMin = numberValue(f.phMin); const phMax = numberValue(f.phMax); const tank = numberValue(f.tank);
  if (filterError(f)) return [];
  const results = state.species.filter((item) => {
    const info = localize(item.id); const profile = care(item.id);
    const haystack = [info.common_name, item.scientific_name, ...(item.aliases || []), info.summary].join(" ").toLowerCase();
    if (query && !haystack.includes(query)) return false;
    if (f.type && organismType(item) !== f.type) return false;
    if (f.difficulty && profile.difficulty !== f.difficulty) return false;
    if (f.layer && !waterLayers(profile).includes(f.layer)) return false;
    if (f.temperament && profile.temperament !== f.temperament) return false;
    if (f.oxygen && profile.husbandry?.oxygen_need !== f.oxygen) return false;
    if (f.flow.length && !f.flow.some((value) => profile.husbandry?.flow_preference?.includes(value))) return false;
    if (tempMin != null && (profile.temperature_max_c == null || profile.temperature_max_c < tempMin)) return false;
    if (tempMax != null && (profile.temperature_min_c == null || profile.temperature_min_c > tempMax)) return false;
    if (phMin != null && (profile.ph_max == null || profile.ph_max < phMin)) return false;
    if (phMax != null && (profile.ph_min == null || profile.ph_min > phMax)) return false;
    if (tank != null && (profile.minimum_tank_l == null || profile.minimum_tank_l > tank)) return false;
    return Object.entries(multiFilterFields).every(([field, key]) => {
      const selected = f[field] || [];
      if (!selected.length) return true;
      const values = Array.isArray(item[key]) ? item[key] : [item[key]];
      return selected.some((value) => values.includes(value));
    });
  });
  if (f.sort) {
    const key = f.sort === "length" ? "adult_length_cm" : "minimum_tank_l";
    results.sort((a, b) => {
      const av = numberValue(care(a.id)[key]); const bv = numberValue(care(b.id)[key]);
      return (av ?? Infinity) - (bv ?? Infinity) || a.scientific_name.localeCompare(b.scientific_name);
    });
  }
  return results;
}
function filtersFromForm(form) {
  const data = new FormData(form);
  const values = Object.fromEntries(["q", "difficulty", "tempMin", "tempMax", "phMin", "phMax", "tank", "layer", "temperament", "oxygen"].map((key) => [key, data.get(key) || ""]));
  for (const field of Object.keys(multiFilterFields)) values[field] = data.getAll(field).join(",");
  values.flow = data.getAll("flow").join(",");
  return values;
}
function renderCalculator() { return `<div class="page"><div class="calculator-placeholder"><div class="placeholder-icon" aria-hidden="true">＋</div><span class="eyebrow">${t("p1Eyebrow")}</span><h1>${t("calculatorTitle")}</h1><p>${t("calculatorBody")}</p><ul><li>${t("calculatorPoint1")}</li><li>${t("calculatorPoint2")}</li><li>${t("calculatorPoint3")}</li></ul><div class="card-meta" style="margin-top:25px"><span class="tag gold">${t("comingSoon")}</span></div></div></div>`; }
function optionChips(field) {
  const selected = state.filters[field] || [];
  return `<fieldset class="chip-field"><legend>${escapeHtml(ft(field))}</legend><div class="chip-options">${availableOptions(field).map((value) => `<label class="filter-chip ${selected.includes(value) ? "is-selected" : ""}"><input type="checkbox" name="${field}" value="${value}" ${selected.includes(value) ? "checked" : ""} /><span>${escapeHtml(optionLabel(field, value))}</span></label>`).join("")}</div></fieldset>`;
}
function selectedFilters() {
  const f = state.filters;
  const selected = Object.keys(multiFilterFields).flatMap((field) => (f[field] || []).map((value) => ({ field, value, label: optionLabel(field, value) })));
  const simple = { difficulty: { easy: t("easy"), medium: t("moderate"), hard: t("hard") }, layer: { top: t("top"), mid: t("mid"), bottom: t("bottom") }, temperament: ft("temperaments"), oxygen: ft("oxygenOptions") };
  for (const field of Object.keys(simple)) if (f[field]) selected.push({ field, value: f[field], label: simple[field][f[field]] || f[field] });
  for (const value of f.flow) selected.push({ field: "flow", value, label: ft("flowOptions")[value] });
  for (const field of ["q", "tank", "tempMin", "tempMax", "phMin", "phMax"]) if (f[field]) selected.push({ field, value: f[field], label: `${field === "q" ? t("search") : field === "tank" ? ft("tankVolume") : field.startsWith("temp") ? t("temperature") : t("pH")} ${f[field]}` });
  return selected;
}
function renderActiveFilters() {
  const selected = selectedFilters();
  return selected.length ? `<div class="active-filters" aria-label="${escapeHtml(t("filters"))}">${selected.map(({ field, value, label }) => `<button type="button" class="active-chip" data-remove-field="${field}" data-remove-value="${escapeHtml(value)}" aria-label="${escapeHtml(t("clear"))}: ${escapeHtml(label)}">${escapeHtml(label)} <span aria-hidden="true">×</span></button>`).join("")}</div>` : "";
}
function renderEnhancedFilter() {
  const f = state.filters; const active = selectedFilters().length;
  const option = (value, label, selected) => `<option value="${value}" ${selected ? "selected" : ""}>${escapeHtml(label)}</option>`;
  const numeric = (name, min, max, step, placeholder) => `<input name="${name}" type="number" min="${min}" max="${max}" step="${step}" value="${escapeHtml(f[name])}" placeholder="${state.locale === "zh-CN" ? name.endsWith("Min") ? "最低" : "最高" : placeholder}" aria-label="${escapeHtml(`${name.startsWith("temp") ? t("temperature") : t("pH")} ${name.endsWith("Min") ? state.locale === "zh-CN" ? "最低" : "minimum" : state.locale === "zh-CN" ? "最高" : "maximum"}`)}" />`;
  const select = (name, label, options) => `<div class="field"><label for="filter-${name}">${escapeHtml(label)}</label><select id="filter-${name}" name="${name}">${option("", state.locale === "zh-CN" ? "全部" : "All", !f[name])}${options.map(([value, text]) => option(value, text, f[name] === value)).join("")}</select></div>`;
  const flowChips = `<fieldset class="chip-field"><legend>${escapeHtml(ft("flow"))}</legend><div class="chip-options">${Object.entries(ft("flowOptions")).map(([value, label]) => `<label class="filter-chip"><input type="checkbox" name="flow" value="${value}" ${f.flow.includes(value) ? "checked" : ""}><span>${escapeHtml(label)}</span></label>`).join("")}</div></fieldset>`;
  return `<details class="filter-shell" ${window.innerWidth > 820 ? "open" : ""}><summary>${escapeHtml(ft("showFilters"))}${active ? ` <span class="filter-count">${active}</span>` : ""}</summary><form class="filter-panel" id="filter-form"><h2>${t("filters")}</h2><div class="field"><label for="filter-q">${t("search")}</label><input id="filter-q" name="q" value="${escapeHtml(f.q)}" placeholder="${escapeHtml(t("searchPlaceholder"))}" /></div>${optionChips("category")}${select("difficulty", t("difficulty"), [["easy", t("easy")], ["medium", t("moderate")], ["hard", t("hard")]])}<div class="field"><label for="filter-tank">${escapeHtml(ft("tankVolume"))}</label><input id="filter-tank" name="tank" type="number" min="1" value="${escapeHtml(f.tank)}" placeholder="60" /></div><details class="advanced-filters" ${(f.color.length || f.size.length || f.reproduction.length || f.social.length || f.body.length || f.layer || f.temperament || f.oxygen || f.flow.length || f.tempMin || f.tempMax || f.phMin || f.phMax) ? "open" : ""}><summary>${escapeHtml(ft("more"))}</summary><div class="advanced-content">${optionChips("color")}${optionChips("size")}${optionChips("reproduction")}${optionChips("social")}${optionChips("body")}${select("layer", t("waterLayer"), [["top", t("top")], ["mid", t("mid")], ["bottom", t("bottom")]])}${select("temperament", ft("temperament"), Object.entries(ft("temperaments")))}${select("oxygen", ft("oxygen"), Object.entries(ft("oxygenOptions")))}${flowChips}<div class="field"><label>${t("temperature")}</label><div class="range-pair">${numeric("tempMin", 0, 40, 1, "Min")}${numeric("tempMax", 0, 40, 1, "Max")}</div></div><div class="field"><label>${t("pH")}</label><div class="range-pair">${numeric("phMin", 0, 14, .1, "Min")}${numeric("phMax", 0, 14, .1, "Max")}</div></div><p class="range-hint">${escapeHtml(ft("rangeHint"))}</p></div></details><p id="filter-error" class="filter-error" role="alert" hidden></p><div class="filter-actions"><button class="clear" type="button" id="clear-filters">${t("clear")}</button><button class="apply" type="submit">${t("apply")}</button></div></form></details>`;
}
const presetFilters = {
  smallTank: { tank: "40" }, gentleSchool: { social: "schooling", temperament: "peaceful" }, topFish: { layer: "top" }, midSchool: { layer: "mid", social: "schooling" }, bottomFish: { layer: "bottom" }, livebearer: { reproduction: "livebearing" }
};
function renderEnhancedDatabase() {
  const error = filterError(state.filters);
  const results = filterSpecies();
  const types = ["", ...["fish", "shrimp", "snail"].filter((type) => state.species.some((item) => organismType(item) === type) || state.filters.type === type)];
  const typeBar = `<div class="type-bar" role="group" aria-label="${escapeHtml(ft("organismType"))}">${types.map((type) => `<button type="button" data-type="${type}" class="type-option ${state.filters.type === type ? "active" : ""}" aria-pressed="${state.filters.type === type}">${escapeHtml(type ? ft("types")[type] : ft("allTypes"))}</button>`).join("")}</div>`;
  const empty = state.filters.type && !state.species.some((item) => organismType(item) === state.filters.type) ? t("noEntries") : t("noResults");
  return `<div class="page database-page"><div class="results-head"><div><h1>${t("database")}</h1>${error ? "" : `<span>${results.length} ${t("speciesCount")}</span>`}</div><div class="result-tools"><label for="sort-results">${escapeHtml(ft("sort"))}</label><select id="sort-results" name="sort" aria-label="${escapeHtml(ft("sort"))}"><option value="" ${!state.filters.sort ? "selected" : ""}>${escapeHtml(ft("sortDefault"))}</option><option value="length" ${state.filters.sort === "length" ? "selected" : ""}>${escapeHtml(ft("sortLength"))}</option><option value="tank" ${state.filters.sort === "tank" ? "selected" : ""}>${escapeHtml(ft("sortTank"))}</option></select></div></div>${typeBar}<div class="preset-bar" aria-label="${escapeHtml(ft("presets"))}">${Object.keys(presetFilters).map((key) => `<button type="button" class="preset-button" data-preset="${key}">${escapeHtml(ft(key))}</button>`).join("")}</div>${renderActiveFilters()}<div class="database-layout">${renderEnhancedFilter()}<section><div class="results-grid">${error ? `<div class="empty-state" role="alert">${escapeHtml(error)}</div>` : results.length ? results.map(speciesCard).join("") : `<div class="empty-state"><p>${escapeHtml(empty)}</p><a class="text-link" href="?lang=${encodeURIComponent(state.locale)}&view=database">${t("resetFilters")}</a></div>`}</div></section></div></div>`;
}
const detailLabels = {
  en: {
    light: { subdued: "Subdued light", moderate: "Moderate light", adaptable: "Adaptable light" },
    substrate: { smooth_sand: "Smooth sand", smooth_gravel: "Smooth gravel", optional: "Substrate optional" },
    habitat: { plants: "Plants", surface_cover: "Surface cover", hiding_places: "Hiding places", wood: "Wood", open_swimming: "Open swimming", mature_biofilm: "Mature biofilm" },
    feedingZone: { surface: "Surface", midwater: "Midwater", bottom: "Bottom" },
    food: { small_particles: "Small particles", floating: "Floating food", sinking: "Sinking food", vegetable: "Vegetable food", biofilm: "Biofilm" }
  },
  "zh-CN": {
    light: { subdued: "柔和光照", moderate: "适中光照", adaptable: "光照适应性较广" },
    substrate: { smooth_sand: "细软沙", smooth_gravel: "圆滑砾石", optional: "底材非必需" },
    habitat: { plants: "水草", surface_cover: "水面遮蔽", hiding_places: "躲避处", wood: "沉木", open_swimming: "开阔游泳区", mature_biofilm: "成熟生物膜" },
    feedingZone: { surface: "水面", midwater: "中层", bottom: "底层" },
    food: { small_particles: "小颗粒", floating: "浮性饲料", sinking: "沉性饲料", vegetable: "植物性饲料", biofilm: "生物膜" }
  }
};
function tagLabels(kind, values = []) { return values.map((value) => detailLabels[state.locale][kind][value]).filter(Boolean).join(" · "); }
function careRow(title, value, note = "") {
  return `<div class="care-row"><span>${escapeHtml(title)}</span><div><strong>${escapeHtml(value || t("unknownValue"))}</strong>${note ? `<p>${escapeHtml(note)}</p>` : ""}</div></div>`;
}
function renderAquaticDetail() {
  const query = new URLSearchParams(location.search);
  const item = getSpecies(query.get("species"));
  if (!item) return `<div class="page"><div class="empty-state">${escapeHtml(t("noEntries"))}</div><a class="back-link" href="?lang=${encodeURIComponent(state.locale)}&view=database">← ${t("backToDatabase")}</a></div>`;
  const info = localize(item.id), profile = care(item.id), husbandry = profile.husbandry || {}, asset = media(item.id);
  const notesFor = husbandry.notes?.[state.locale] || husbandry.notes?.en || {};
  const range = formatRange(profile.temperature_min_c, profile.temperature_max_c, "°C");
  const oxygen = ft("oxygenOptions")[husbandry.oxygen_need] || t("unknownValue");
  const flow = (husbandry.flow_preference || []).map((value) => ft("flowOptions")[value]).filter(Boolean).join(" / ") || t("unknownValue");
  const length = husbandry.minimum_tank_length_cm ? `${husbandry.minimum_tank_length_cm} cm` : "";
  const space = length ? `${t("minimumLength")} ${length}` : notesFor.space || t("unknownValue");
  const food = tagLabels("food", husbandry.food_form_tags);
  const zone = tagLabels("feedingZone", husbandry.feeding_zone);
  const setup = [tagLabels("light", [husbandry.light_preference]), tagLabels("substrate", husbandry.substrate_tags), tagLabels("habitat", husbandry.habitat_tags)].filter(Boolean).join(" · ");
  const alerts = (husbandry.alerts || []).slice(0, 3);
  const labels = info.filter_labels || {};
  const identityTags = [(labels.categories || [])[0] || optionLabel("category", item.category_tags?.[0]), labels.reproduction || optionLabel("reproduction", item.reproduction_mode)].filter(Boolean);
  const recognition = [[ft("color"), (labels.colors || []).join(" / ")], [ft("body"), (labels.body_shape || []).join(" / ")]].filter(([, value]) => value);
  const itemNotes = notes(item.id).map((note) => localized(note.text_localized) || note.text);
  const rules = state.compatibility.filter((rule) => rule.subject_species_id === item.id || rule.object_species_id === item.id);
  const otherId = (rule) => rule.subject_species_id === item.id ? rule.object_species_id : rule.subject_species_id;
  const facts = [
    [t("adultLength"), displayValue(profile.adult_length_cm, " cm")], [t("water"), range], [t("pH"), formatRange(profile.ph_min, profile.ph_max)], [t("hardness"), formatRange(profile.gh_min, profile.gh_max)],
    [t("tank"), displayValue(profile.minimum_tank_l, " L")], [t("waterLayer"), layersLabel(profile)], [t("difficulty"), difficultyLabel(profile.difficulty)], [t("temperament"), ft("temperaments")[profile.temperament] || t("unknownValue")]
  ];
  const detailMedia = asset.image_status === "formal" && asset.file_url ? `<img src="${escapeHtml(asset.file_url)}" alt="${escapeHtml(asset.alt || info.common_name)}">` : `<span>${escapeHtml(asset.image_status === "placeholder" ? t("placeholder") : t("noPhoto"))}</span>`;
  const diet = { omnivore: t("omnivore"), carnivore: state.locale === "zh-CN" ? "肉食" : "Carnivore", grazer: t("grazer") }[profile.diet] || t("unknownValue");
  return `<div class="page detail-page"><div class="detail-hero"><section class="detail-copy"><div class="breadcrumb"><a class="return-list" href="${escapeHtml(safeReturn())}">${t("database")}</a><span>/</span><span>${escapeHtml(info.common_name)}</span></div><h1>${escapeHtml(info.common_name || item.scientific_name)}</h1><span class="latin">${escapeHtml(item.scientific_name)}</span><p class="summary">${escapeHtml(info.summary)}</p><div class="identity-tags">${identityTags.map((value) => `<span class="tag">${escapeHtml(value)}</span>`).join("")}</div></section><div class="detail-media ${asset.image_status === "formal" ? "has-image" : "none"}">${detailMedia}</div></div>
    ${recognition.length ? `<details class="recognition"><summary>${state.locale === "zh-CN" ? "识别特征" : "Identification"}</summary><dl>${recognition.map(([label, value]) => `<div><dt>${escapeHtml(label)}</dt><dd>${escapeHtml(value)}</dd></div>`).join("")}</dl></details>` : ""}
    <section class="detail-section"><h2>${t("careConditions")}</h2><dl class="stat-grid">${facts.map(([label, value]) => `<div class="stat"><dt>${escapeHtml(label)}</dt><dd>${escapeHtml(value)}</dd></div>`).join("")}</dl>${itemNotes.length ? `<div class="field-notes">${itemNotes.map((text) => `<p>${escapeHtml(text)}</p>`).join("")}</div>` : ""}</section>
    <section class="detail-section"><h2>${t("environment")}</h2><div class="care-rows">${careRow(t("oxygen"), oxygen, notesFor.oxygen)}${careRow(t("flow"), flow, notesFor.flow)}${careRow(t("space"), space, length ? notesFor.space : "")}${careRow(t("group"), groupLabel(item, profile, true), notesFor.group)}</div></section>
    <section class="detail-section"><h2>${t("feedingSetup")}</h2><div class="care-rows">${careRow(t("feeding"), [diet, food, zone].filter(Boolean).join(" · "), notesFor.feeding)}${careRow(t("setup"), setup || t("unknownValue"), notesFor.setup)}</div>${info.body ? `<details class="more-care"><summary>${t("moreDetail")}</summary><p>${escapeHtml(info.body)}</p></details>` : ""}</section>
    ${alerts.length || rules.length ? `<section class="detail-section"><h2>${t("mixingRisks")}</h2>${alerts.length ? `<div class="alert-list">${alerts.map((alert) => `<p class="care-alert ${alert.severity}"><strong>${escapeHtml(t(alert.severity === "important" ? "important" : "caution"))}</strong>${escapeHtml(localized(alert.text))}</p>`).join("")}</div>` : ""}<div class="compat-grid">${rules.map((rule) => { const other = getSpecies(otherId(rule)); const friend = other ? localize(other.id).common_name : otherId(rule); const reason = localized(rule.reason_localized) || rule.reason; const conditions = localized(rule.conditions_localized) || rule.conditions; return `<article class="compat-card"><header><strong>${escapeHtml(friend)}</strong><span class="risk ${escapeHtml(rule.risk_level)}">${escapeHtml(t(rule.decision || "unknown"))}</span></header><p>${escapeHtml(reason)}</p>${conditions ? `<details><summary>${t("conditions")}</summary><p>${escapeHtml(conditions)}</p></details>` : ""}</article>`; }).join("")}</div></section>` : ""}
    ${state.guides.length ? `<section class="detail-section"><h2>${t("commonGuides")}</h2><div class="guide-list">${state.guides.map((guide) => `<details><summary>${escapeHtml(localized(guide.title))}</summary><p>${escapeHtml(localized(guide.body))}</p></details>`).join("")}</div></section>` : ""}
    <a class="back-link return-list" href="${escapeHtml(safeReturn())}">← ${t("backToDatabase")}</a></div>`;
}
function bindEvents() {
  document.querySelectorAll("[data-locale]").forEach((button) => { button.onclick = () => {
    const draft = captureDraft(), query = new URLSearchParams(location.search);
    query.set("lang", button.dataset.locale);
    history.replaceState({ ...history.state, scrollY: window.scrollY }, "");
    history.pushState({ ...history.state }, "", `?${query.toString()}`);
    parseUrl(); render(); restoreDraft(draft); window.scrollTo(0, history.state.scrollY || 0);
  }; });
  document.querySelectorAll(".species-card").forEach((link) => link.addEventListener("click", openSpecies));
  document.querySelectorAll(".return-list").forEach((link) => link.addEventListener("click", returnToList));
  const heroForm = document.getElementById("hero-search");
  if (heroForm) heroForm.addEventListener("submit", (event) => { event.preventDefault(); navigate({ lang: state.locale, view: "database", q: new FormData(heroForm).get("q") || "", sort: state.filters.sort }); });
  document.getElementById("sort-results")?.addEventListener("change", (event) => navigate({ lang: state.locale, view: "database", ...state.filters, sort: event.target.value }, { preserveDraft: true, keepScroll: true }));
  document.querySelectorAll("[data-type]").forEach((button) => button.addEventListener("click", () => {
    const type = button.dataset.type;
    const applicable = new Set(state.species.filter((item) => !type || organismType(item) === type).flatMap((item) => item.category_tags || []));
    const category = type ? state.filters.category.filter((value) => applicable.has(value)) : state.filters.category;
    navigate({ lang: state.locale, view: "database", ...state.filters, type, category }, { preserveDraft: true, resetDraft: ["category"], keepScroll: true });
  }));
  const filterForm = document.getElementById("filter-form");
  if (filterForm) filterForm.addEventListener("submit", (event) => {
    event.preventDefault();
    const values = filtersFromForm(filterForm);
    const error = document.getElementById("filter-error");
    if ((values.tempMin && values.tempMax && Number(values.tempMin) > Number(values.tempMax)) || (values.phMin && values.phMax && Number(values.phMin) > Number(values.phMax))) {
      error.textContent = state.locale === "zh-CN" ? "最小值不能大于最大值" : "Minimum cannot exceed maximum";
      error.hidden = false;
      filterForm.querySelector(".advanced-filters").open = true;
      return;
    }
    const validation = filterError({ ...state.filters, ...values });
    if (validation) { error.textContent = validation; error.hidden = false; filterForm.querySelector(".advanced-filters").open = true; return; }
    navigate({ lang: state.locale, view: "database", type: state.filters.type, ...values, sort: state.filters.sort });
  });
  document.getElementById("clear-filters")?.addEventListener("click", () => navigate({ lang: state.locale, view: "database" }));
  document.querySelectorAll("[data-preset]").forEach((button) => button.addEventListener("click", () => navigate({ lang: state.locale, view: "database", sort: state.filters.sort, ...presetFilters[button.dataset.preset] })));
  document.querySelectorAll("[data-remove-field]").forEach((button) => button.addEventListener("click", () => {
    const field = button.dataset.removeField;
    const value = button.dataset.removeValue;
    const filters = { ...state.filters, [field]: Array.isArray(state.filters[field]) ? state.filters[field].filter((item) => item !== value) : "" };
    navigate({ lang: state.locale, view: "database", ...filters }, { preserveDraft: true, resetDraft: [field], keepScroll: true });
  }));
}
function render() { const main = document.getElementById("main-content"); if (state.view === "detail") main.innerHTML = renderAquaticDetail(); else if (state.view === "database") main.innerHTML = renderEnhancedDatabase(); else if (state.view === "calculator") main.innerHTML = renderCalculator(); else main.innerHTML = renderHome(); document.documentElement.lang = state.locale; updateNav(); bindEvents(); }

history.scrollRestoration = "manual";
window.addEventListener("popstate", (event) => { parseUrl(); render(); requestAnimationFrame(() => window.scrollTo(0, event.state?.scrollY || 0)); });
window.matchMedia("(min-width: 821px)").addEventListener("change", (event) => {
  if (event.matches) {
    const filters = document.querySelector(".filter-shell");
    if (filters) filters.open = true;
  }
});
parseUrl();
loadData().then(() => { parseUrl(); render(); }).catch((error) => { document.getElementById("main-content").innerHTML = `<div class="page"><div class="empty-state">Failed to load site data: ${escapeHtml(error.message)}</div></div>`; });
