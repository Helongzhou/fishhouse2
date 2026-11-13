import { isPublishedIn } from "@/components/site/format";
import { notFound } from "next/navigation";
import { getPublicRelease } from "@/server/catalog";
import { EntityDetail } from "@/components/site/detail";
import type { Locale } from "@/domain/types";
export default async function DetailPage({
  params,
}: {
  params: Promise<{ locale: Locale; slug: string }>;
}) {
  const { locale, slug } = await params;
  const release = await getPublicRelease();
  const entity = release.catalog.find(
    (e) => e.id === slug && e.active && isPublishedIn(e, locale),
  );
  if (!entity) notFound();
  return <EntityDetail entity={entity} release={release} locale={locale} />;
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ locale: Locale; slug: string }>;
}) {
  const { locale, slug } = await params;
  const release = await getPublicRelease();
  const e = release.catalog.find(
    (e) => e.id === slug && e.active && isPublishedIn(e, locale),
  );
  if (!e) return {};
  const langs = Object.fromEntries(
    (e.publishedLocales || ["zh-CN", "en"]).map((l) => [
      l,
      `/${l}/aquatic/${e.id}`,
    ]),
  );
  return {
    title: `${e.locales[locale]?.name} · FishAtlas`,
    description: e.locales[locale]?.summary,
    alternates: { canonical: `/${locale}/aquatic/${e.id}`, languages: langs },
  };
}
