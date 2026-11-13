import type { MetadataRoute } from "next";
import { getPublicRelease } from "@/server/catalog";
import { isPublishedIn } from "@/components/site/format";
import type { Locale } from "@/domain/types";
export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const release = await getPublicRelease();
  const base = (process.env.PUBLIC_SITE_URL || "http://127.0.0.1:4174").replace(
    /\/$/,
    "",
  );
  const locales = (release.publishedLocales || ["zh-CN", "en"]) as Locale[];
  return locales.flatMap((locale) => [
    { url: `${base}/${locale}` },
    { url: `${base}/${locale}/aquatic` },
    { url: `${base}/${locale}/materials` },
    ...release.catalog
      .filter((e) => e.active && isPublishedIn(e, locale))
      .map((e) => ({ url: `${base}/${locale}/aquatic/${e.id}` })),
  ]);
}
