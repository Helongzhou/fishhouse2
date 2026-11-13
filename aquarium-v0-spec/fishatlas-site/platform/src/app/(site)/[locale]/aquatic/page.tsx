import { getPublicRelease } from "@/server/catalog";
import { Library } from "@/components/site/library";
import { isPublishedIn, text } from "@/components/site/format";
import type { Locale } from "@/domain/types";
export default async function AquaticPage({
  params,
}: {
  params: Promise<{ locale: Locale }>;
}) {
  const { locale } = await params;
  const source = await getPublicRelease();
  const release = {
    ...source,
    catalog: source.catalog.filter((e) => isPublishedIn(e, locale)),
  };
  return <Library release={release} locale={locale} />;
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ locale: Locale }>;
}) {
  const { locale } = await params;
  return {
    title: text(locale, "水族库 · FishAtlas", "Aquatic library · FishAtlas"),
  };
}
