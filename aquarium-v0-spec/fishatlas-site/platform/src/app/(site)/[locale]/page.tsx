import Link from "next/link";
import { getPublicRelease } from "@/server/catalog";
import { EntityCard } from "@/components/site/shared";
import { isPublishedIn, kindName, text } from "@/components/site/format";
import type { Locale } from "@/domain/types";
import { animalKinds, availableWaterTypes, optionLabel } from "@/components/site/library-filters";
import { HomeHero } from "@/components/site/home";
export default async function HomePage({
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
  const waterChoices = availableWaterTypes(release.catalog.filter((e) => animalKinds.includes(e.kind) || e.kind === "plant"));
  return (
    <div className="page home-page">
      <section className="home-hero">
        <div className="hero-copy">
          <h1>
            {text(
              locale,
              "认识水下的生命，\n照顾好每一天。",
              "Know their world.\nCare for yours.",
            )}
          </h1>
          <p>
            {text(
              locale,
              "查阅鱼虾螺与水草的习性、环境和饲养资料。",
              "Explore freshwater life, its habitats and everyday care.",
            )}
          </p>
          <form action={`/${locale}/aquatic`} className="search-form">
            <input
              type="search"
              name="q"
              autoComplete="off"
              aria-label={text(locale, "搜索水族库", "Search aquatic library")}
              placeholder={text(
                locale,
                "搜索名称、学名或别名…",
                "Search names or scientific names…",
              )}
            />
            <button type="submit">{text(locale, "搜索", "Search")}</button>
          </form>
          <div className="hero-actions">
            <Link className="text-link" href={`/${locale}/aquatic`}>
              {text(locale, "浏览全部资料", "Explore the library")}
            </Link>
          </div>
        </div>
        <HomeHero locale={locale} />
      </section>
      <section className="home-section category-section" aria-labelledby="category-heading">
        <div className="section-heading">
          <h2 id="category-heading">{text(locale, "按种类浏览", "Browse by kind")}</h2>
        </div>
        {waterChoices.length > 0 && (
          <nav className="water-tabs home-water-tabs" aria-label={text(locale, "水体", "Water type")}>
            <span>{text(locale, "水体", "Water type")}</span>
            {waterChoices.map((water) => (
              <Link key={water} href={`/${locale}/aquatic?water=${water}`}>
                {optionLabel("water", water, locale, release)}
              </Link>
            ))}
          </nav>
        )}
        <div className="category-links">
          {["", "fish", "plant", "invertebrate"].map((kind) => (
            <Link key={kind} href={`/${locale}/aquatic${kind ? `?type=${kind}` : ""}`}>
              {kind ? kindName(kind, locale) : text(locale, "全部", "All")}
            </Link>
          ))}
        </div>
      </section>
      <section className="home-section">
        <div className="section-heading">
          <h2>{text(locale, "精选图鉴", "Featured in the library")}</h2>
          <Link href={`/${locale}/aquatic`}>
            {text(locale, "查看全部", "View all")}
          </Link>
        </div>
        <div className="entity-grid featured-grid">
          {[
            "neon-tetra",
            "ember-tetra",
            "honey-gourami",
            "red-cherry-shrimp",
            "anubias-nana",
            "java-fern",
          ]
            .flatMap((id) =>
              release.catalog.filter((e) => e.id === id && e.active),
            )
            .map((e) => (
              <EntityCard key={e.id} entity={e} locale={locale} />
            ))}
        </div>
      </section>
    </div>
  );
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ locale: Locale }>;
}) {
  const { locale } = await params;
  return {
    title: text(
      locale,
      "FishAtlas · 水族图鉴与饲养资料",
      "FishAtlas · Aquatic library & care",
    ),
    description: text(
      locale,
      "认识淡水鱼虾螺、水草与造景材料，查阅环境、习性和饲养资料。",
      "Explore freshwater fish, shrimp, snails, aquatic plants and materials, with practical care information.",
    ),
  };
}
