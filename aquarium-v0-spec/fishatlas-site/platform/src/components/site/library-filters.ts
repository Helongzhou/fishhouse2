import type {
  CatalogEntity,
  CatalogCategory,
  CatalogRelease,
  Knowledge,
  Locale,
  Range,
} from "@/domain/types";
import { text } from "./format";
export const animalKinds = ["fish", "shrimp", "snail"];
export const publicType = (entity: { kind: string }) =>
  entity.kind === "shrimp" || entity.kind === "snail" ? "invertebrate" : entity.kind;
export const matchesType = (entity: Pick<CatalogEntity, "kind">, type: string | null) =>
  !type || entity.kind === type || publicType(entity) === type;
const waterTypes = ["freshwater", "marine", "brackish"] as const;
export function availableWaterTypes(source: CatalogEntity[]) {
  const present = new Set(source.filter((e) => e.active).flatMap((e) => traitValues(e, "water")));
  return waterTypes.filter((water) => present.has(water));
}
export function presetFilters(query: URLSearchParams, preset: Record<string, string | undefined>) {
  const next = new URLSearchParams();
  for (const key of ["q", "type", "water", "sort"])
    if (query.has(key)) next.set(key, query.get(key)!);
  for (const [key, value] of Object.entries(preset))
    if (value) next.set(key, value);
  return next;
}
export function removeActiveFilter(query: URLSearchParams, key: string, value: string) {
  const next = new URLSearchParams(query);
  if (key === "type" && (value === "shrimp" || value === "snail")) {
    next.set("type", "invertebrate");
  } else {
    const remaining = values(next, key).filter((item) => item !== value);
    next.delete(key);
    if (remaining.length) next.set(key, remaining.join(","));
  }
  return next;
}
export const multiFields = [
  "water",
  "category",
  "color",
  "size",
  "reproduction",
  "social",
  "body",
  "flow",
] as const;
export type MultiField = (typeof multiFields)[number];
const labels: Record<string, [string, string]> = {
 community:['适合社区缸','Community'],catfish:['鲶鱼','Catfish'],deep_bodied:['高身','Deep bodied'],short_round:['短圆','Short & round'],foreground:['前景','Foreground'],midground:['中景','Midground'],background:['后景','Background'],epiphyte:['附生','Epiphyte'],rooted:['底栽','Rooted'],layer:['铺设层','Layer'],piece:['单件','Piece'],rock:['石材','Rock'],sand:['砂砾','Sand'],wood:['沉木','Wood'],substrate:['底床','Substrate'],gray:['灰','Gray'],mixed:['混色','Mixed'],
  freshwater: ["淡水", "Freshwater"],
  marine: ["海水", "Marine"],
  brackish: ["汽水", "Brackish"],
  tetra: ["灯鱼", "Tetras"],
  rasbora: ["波鱼", "Rasboras"],
  gourami: ["丝足鲈", "Gouramis"],
  betta: ["斗鱼", "Bettas"],
  livebearer: ["卵胎生鱼", "Livebearers"],
  danio: ["鲃鱼", "Danios"],
  corydoras: ["鼠鱼", "Corydoras"],
  pleco: ["异型鱼", "Plecos"],
  shrimp: ["虾", "Shrimp"],
  snail: ["螺", "Snails"],
  red: ["红", "Red"],
  blue: ["蓝", "Blue"],
  green: ["绿", "Green"],
  orange: ["橙", "Orange"],
  yellow: ["黄", "Yellow"],
  silver: ["银", "Silver"],
  black: ["黑", "Black"],
  white: ["白", "White"],
  brown: ["棕", "Brown"],
  transparent: ["透明", "Transparent"],
  small: ["小型", "Small"],
  medium: ["中等", "Medium"],
  large: ["大型", "Large"],
  egg_laying: ["卵生", "Egg laying"],
  livebearing: ["卵胎生", "Livebearing"],
  unknown: ["未知", "Unknown"],
  schooling: ["群游", "Schooling"],
  shoaling: ["松散群游", "Shoaling"],
  colony: ["群居", "Colony"],
  solitary_tendency: ["偏独居", "Solitary tendency"],
  pairing: ["配对", "Pairing"],
  loose_group: ["松散群体", "Loose group"],
  slender: ["纤细", "Slender"],
  laterally_compressed: ["侧扁", "Laterally compressed"],
  bottom_dwelling: ["底栖", "Bottom dwelling"],
  armored: ["甲鲶体形", "Armored"],
  flattened: ["扁平", "Flattened"],
  elongated: ["修长", "Elongated"],
  shrimp_like: ["虾形", "Shrimp-like"],
  easy: ["容易", "Easy"],
  moderate: ["中等", "Moderate"],
  hard: ["较难", "Hard"],
  top: ["上层", "Top"],
  mid: ["中层", "Middle"],
  bottom: ["底层", "Bottom"],
  gentle: ["轻柔", "Gentle"],
  minimal: ["极弱", "Minimal"],
  strong: ["强", "Strong"],
  standard: ["普通", "Standard"],
  high: ["高", "High"],
  peaceful: ["温和", "Peaceful"],
  semi_aggressive: ["有领地性", "Semi-aggressive"],
  territorial: ["领地性", "Territorial"],
  aggressive: ["攻击性", "Aggressive"],
  omnivore: ["杂食", "Omnivore"],
  grazer: ["刮食", "Grazer"],
  carnivore: ["肉食", "Carnivore"],
  herbivore: ["草食", "Herbivore"],
};
export const knownValue = <T>(f: Knowledge<T> | undefined): T | null =>
  f?.knowledgeState === "known" ? f.value : null;
// Browse categories are a separate facet from social patterns and body shapes.
// The published dictionary may add categories; the explicit legacy IDs retain old URLs.
const browseCategoryIds: Record<CatalogEntity["kind"], readonly string[]> = {
  fish: ["tetra", "rasbora", "gourami", "betta", "livebearer", "danio", "corydoras", "catfish", "pleco"],
  shrimp: ["shrimp"],
  snail: ["snail"],
  plant: ["foreground", "midground", "background", "epiphyte", "rooted", "floating"],
  rock: ["rock"], sand: ["sand"], wood: ["wood"], substrate: ["substrate"],
};
export function browseCategories(e: CatalogEntity, categories: CatalogCategory[] = []): string[] {
  const allowed = new Set([
    ...browseCategoryIds[e.kind],
    ...categories.filter(c => c.active && c.kinds.includes(e.kind)).map(c => c.id),
  ]);
  return e.tags.filter(id => allowed.has(id));
}
export function traitValues(e: CatalogEntity, key: string, categories: CatalogCategory[] = []): string[] {
  switch (key) {
    case "water":
      return animalKinds.includes(e.kind) || e.kind === "plant" ? e.discovery?.waterTypes || [] : [];
    case "category":
      return browseCategories(e, categories);
    case "color":
      return e.colors;
    case "size":
      return e.discovery?.sizeClass ? [e.discovery.sizeClass] : [];
    case "reproduction":
      return e.discovery?.reproductionMode
        ? [e.discovery.reproductionMode]
        : [];
    case "social":
      return e.discovery?.socialPatterns || [];
    case "body":
      return e.discovery?.bodyShapes || [];
    case "flow":
      return knownValue(e.care?.flow) || [];
    case "layer":
      return knownValue(e.care?.waterLayers) || [];
    case "difficulty":
      return [knownValue(e.care?.difficulty) || "unknown"];
    case "oxygen":
      return [knownValue(e.care?.oxygen) || "unknown"];
    case "temperament":
      return [knownValue(e.care?.temperament) || "unknown"];
    case "diet":
      return [knownValue(e.care?.diet) || "unknown"];
    default:
      return [];
  }
}
export function optionLabel(
  key: string,
  v: string,
  locale: Locale,
  release: CatalogRelease,
): string {
  if (key === "category") {
    const cat = release.categories.find((c) => c.id === v);
    if (cat) return cat.names[locale];
  }
  const localizationKey: Record<string, string> = {
    category: "categories",
    color: "colors",
    reproduction: "reproduction",
    social: "social",
    body: "body_shape",
  };
  for (const e of release.catalog) {
    const values = traitValues(e, key, release.categories);
    const index = values.indexOf(v);
    if (index < 0) continue;
    const discovery = e.discovery as typeof e.discovery & {
      filterLabels?: Record<Locale, Record<string, string | string[]>>;
    };
    const translated =
      discovery?.filterLabels?.[locale]?.[localizationKey[key]];
    if (translated) {
      const label = Array.isArray(translated) ? translated[index] : translated;
      if (label) return label;
    }
  }
  return labels[v]?.[locale === "zh-CN" ? 0 : 1] || v.replaceAll("_", " ");
}
export function values(p: URLSearchParams, key: string) {
  return [
    ...new Set(
      p
        .getAll(key)
        .flatMap((s) => s.split(","))
        .map((s) => s.trim())
        .filter(Boolean),
    ),
  ];
}
export function normalizeFilters(raw: string, source: CatalogEntity[], categories: CatalogCategory[] = []) {
  const p = new URLSearchParams(raw);
  for (const key of [
    ...multiFields,
    "layer",
    "difficulty",
    "oxygen",
    "temperament",
    "diet",
  ]) {
    const aquatic = source.some((e) => animalKinds.includes(e.kind) || e.kind === "plant");
    const options = new Set<string>(key === "water"
      ? aquatic ? waterTypes : []
      : source.flatMap((e) => traitValues(e, key, categories)));
    if (key === "flow")
      for (const x of ["minimal", "gentle", "moderate", "strong"])
        options.add(x);
    const valid = values(p, key).filter((v) => options.has(v));
    p.delete(key);
    if (valid.length) p.set(key, valid.join(","));
  }
  if (p.get("type")) {
    const applicable = new Set(
      source.filter((e) => matchesType(e, p.get("type"))).flatMap((e) => browseCategories(e, categories)),
    );
    const valid = values(p, "category").filter((v) => applicable.has(v));
    p.delete("category");
    if (valid.length) p.set("category", valid.join(","));
  }
  return p;
}
export function numericError(p: URLSearchParams, locale: Locale) {
  for (const [key, min, max] of [
    ["tempMin", 0, 40],
    ["tempMax", 0, 40],
    ["phMin", 0, 14],
    ["phMax", 0, 14],
    ["tank", 1, 100000],
  ] as const) {
    const v = p.get(key);
    if (
      v !== null &&
      v !== "" &&
      (!v.trim() ||
        !Number.isFinite(Number(v)) ||
        Number(v) < min ||
        Number(v) > max)
    )
      return text(
        locale,
        "请填写有效数值：水温 0–40 °C、pH 0–14、缸体至少 1 L。",
        "Enter valid numbers: 0–40 °C, pH 0–14, and tank volume at least 1 L.",
      );
  }
  for (const [a, b] of [
    ["tempMin", "tempMax"],
    ["phMin", "phMax"],
  ])
    if (p.get(a) && p.get(b) && Number(p.get(a)) > Number(p.get(b)))
      return text(
        locale,
        "最低值不能大于最高值。",
        "Minimum cannot exceed maximum.",
      );
  return "";
}
export function filterEntities(
  source: CatalogEntity[],
  p: URLSearchParams,
  locale: Locale,
  categories: CatalogCategory[] = [],
): CatalogEntity[] {
  if (numericError(p, locale)) return [];
  const overlaps = (
    f: Knowledge<Range> | undefined,
    a: string,
    b: string,
    reviewed = true,
  ) => {
    if (!p.get(a) && !p.get(b)) return true;
    return (
      reviewed &&
      f?.knowledgeState === "known" &&
      f.value.max >= Number(p.get(a) || -Infinity) &&
      f.value.min <= Number(p.get(b) || Infinity)
    );
  };
  const out = source.filter((e) => {
    const q = (p.get("q") || "").trim().toLocaleLowerCase();
    if (
      q &&
      ![
        e.id,
        e.scientificName,
        ...e.aliases,
        ...Object.values(e.locales).map((l) => l.name),
      ]
        .join(" ")
        .toLocaleLowerCase()
        .includes(q)
    )
      return false;
    if (!matchesType(e, p.get("type"))) return false;
    for (const key of [
      ...multiFields,
      "layer",
      "difficulty",
      "oxygen",
      "temperament",
      "diet",
    ]) {
      const selected = values(p, key);
      const careKey = (
        {
          flow: "flow",
          layer: "waterLayers",
          difficulty: "difficulty",
          oxygen: "oxygen",
          temperament: "temperament",
          diet: "diet",
        } as Record<string, string>
      )[key];
      if (
        selected.length &&
        careKey &&
        e.care?.editorialState?.[
          careKey as keyof typeof e.care.editorialState
        ] === "needsReview"
      )
        return false;
      const field=(e.care as unknown as Record<string,Knowledge<unknown>> | undefined)?.[careKey];
      if(selected.length && field?.editorialState==='needsReview')return false;
      if (
        selected.length &&
        !selected.some((v) => traitValues(e, key, categories).includes(v))
      )
        return false;
    }
    if (
      p.get("tank") &&
      (e.care?.minimumTankL.knowledgeState !== "known" ||
        (e.care?.editorialState?.minimumTankL === "needsReview" || e.care?.minimumTankL.editorialState === "needsReview") ||
        e.care.minimumTankL.value > Number(p.get("tank")))
    )
      return false;
    return (
      overlaps(
        e.care?.temperatureC,
        "tempMin",
        "tempMax",
        e.care?.editorialState?.temperatureC !== "needsReview" && e.care?.temperatureC.editorialState !== "needsReview",
      ) &&
      overlaps(
        e.care?.ph,
        "phMin",
        "phMax",
        e.care?.editorialState?.ph !== "needsReview" && e.care?.ph.editorialState !== "needsReview",
      )
    );
  });
  const number = (e: CatalogEntity, key: "adultLengthMm" | "minimumTankL") =>
    knownValue(e.care?.[key]) ?? Infinity;
  if (p.get("sort") === "length")
    out.sort((a, b) => number(a, "adultLengthMm") - number(b, "adultLengthMm"));
  if (p.get("sort") === "tank")
    out.sort((a, b) => number(a, "minimumTankL") - number(b, "minimumTankL"));
  if (p.get("sort") === "name")
    out.sort((a, b) =>
      (a.locales[locale]?.name||a.id).localeCompare((b.locales[locale]?.name||b.id), locale),
    );
  if (p.get("sort") === "difficulty") {
    const order = ["easy", "medium", "moderate", "hard"];
    out.sort((a, b) => {
      const av = order.indexOf(knownValue(a.care?.difficulty) || "");
      const bv = order.indexOf(knownValue(b.care?.difficulty) || "");
      return (av < 0 ? 99 : av) - (bv < 0 ? 99 : bv);
    });
  }
  if (!p.get('sort')) { const order=['fish','shrimp','snail','plant','rock','sand','wood','substrate']; out.sort((a,b)=>order.indexOf(a.kind)-order.indexOf(b.kind)||(a.locales[locale]?.name||a.id).localeCompare((b.locales[locale]?.name||b.id),locale)); }
  return out;
}
