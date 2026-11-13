import type { CatalogEntity, Locale } from './types';

export function localeIsPublished(publishedLocales: Locale[] | undefined, locale: Locale): boolean {
  return publishedLocales === undefined || publishedLocales.includes(locale);
}

export function missingTranslation(locale: Locale, identity: string, field: 'name' | 'text' | 'unit' = 'text'): string {
  const labels = { name: ['此语言名称未提供', 'Name unavailable in this language'], text: ['此语言文案未提供', 'Text unavailable in this language'], unit: ['此语言单位未提供', 'Unit unavailable in this language'] };
  return `${labels[field][locale === 'zh-CN' ? 0 : 1]} (${identity})`;
}

/** Reads only the requested published locale, including when draft text remains in a snapshot. */
export function localizedText(text: Partial<Record<Locale, string>> | undefined, locale: Locale, identity = '', publishedLocales?: Locale[]): string {
  const value = localeIsPublished(publishedLocales, locale) ? text?.[locale] : undefined;
  return typeof value === 'string' && value.trim() ? value : missingTranslation(locale, identity);
}

export function entityName(entity: CatalogEntity, locale: Locale): string {
  const value = localeIsPublished(entity.publishedLocales, locale) ? entity.locales?.[locale]?.name : undefined;
  return typeof value === 'string' && value.trim() ? value : missingTranslation(locale, entity.scientificName || entity.id, 'name');
}

export function entityUnit(entity: CatalogEntity, locale: Locale): string {
  const units = entity.plant?.unit ?? entity.material?.unit;
  if (entity.plant || entity.material) {
    const value = localeIsPublished(entity.publishedLocales, locale) ? units?.[locale] : undefined;
    return typeof value === 'string' && value.trim() ? value : missingTranslation(locale, entity.id, 'unit');
  }
  return locale === 'zh-CN' ? entity.kind === 'fish' ? '尾' : '只' : 'individual';
}
