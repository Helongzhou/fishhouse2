import type { CatalogEntity, CatalogRelease, Knowledge, Locale, Range } from "@/domain/types";
export const text = (locale: Locale, zh: string, en: string) =>
  locale === "zh-CN" ? zh : en;
export const kindName = (kind: string, locale: Locale) =>
  ({
    fish: ["鱼类", "Fish"],
    invertebrate: ["虾螺", "Shrimp & snails"],
    shrimp: ["虾类", "Shrimp"],
    snail: ["螺类", "Snails"],
    plant: ["水草", "Plants"],
    rock: ["石材", "Rocks"],
    sand: ["砂砾", "Sand & gravel"],
    wood: ["沉木", "Wood"],
    substrate: ["底床", "Substrate"],
  })[kind]?.[locale === "zh-CN" ? 0 : 1] || kind;
export function value<T>(
  field: Knowledge<T> | undefined,
  locale: Locale,
  format?: (value: T) => string,
): string {
  return field?.knowledgeState === "known"
    ? format
      ? format(field.value)
      : String(field.value)
    : field?.knowledgeState === "notApplicable"
      ? text(locale, "不适用", "Not applicable")
      : text(locale, "待补充", "Not known");
}
export const range = (r: Range) => `${r.min}–${r.max}`;
export function isPublishedIn(
  entity: { publishedLocales?: Locale[]; locales?: Partial<Record<Locale,unknown>> },
  locale: Locale,
) {
  return (!entity.publishedLocales || entity.publishedLocales.includes(locale))&&(!entity.locales||Boolean(entity.locales[locale]));
}

export { entityName, entityUnit, localizedText } from '@/domain/localization';

export function recommendedMixing(release: CatalogRelease, entityId: string, locale: Locale) {
  const seen = new Set<string>();
  return release.pairRules.flatMap((rule) => {
    if (!rule.enabled || rule.visibility !== "recommended" || (rule.a !== entityId && rule.b !== entityId)) return [];
    const partnerId = rule.a === entityId ? rule.b : rule.a;
    if (partnerId === entityId || seen.has(partnerId) || !isPublishedIn(rule, locale)) return [];
    const other = release.catalog.find((entity) => entity.id === partnerId && entity.active && isPublishedIn(entity, locale));
    if (!other) return [];
    seen.add(partnerId);
    return [{ rule, other }];
  }).slice(0, 3);
}
