"use client";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import type { CatalogEntity, CatalogRelease, Locale } from "@/domain/types";
import { EntityFacts, Photo, text, kindName, value } from "./shared";
import { entityName, range, recommendedMixing } from "./format";
import { optionLabel } from "./library-filters";
export function EntityDetail({
  entity: e,
  release,
  locale,
}: {
  entity: CatalogEntity;
  release: CatalogRelease;
  locale: Locale;
}) {
  const search = useSearchParams();
  const from = search.get("from");
  const back = from?.startsWith(`/${locale}/`)
    ? from
    : `/${locale}/${e.material ? "materials" : "aquatic"}`;
  const husbandry = e.discovery?.husbandry as
    | {
        notes?: Record<Locale, Record<string, string>>;
        alerts?: { text: Record<Locale, string> }[];
      }
    | undefined;
  const notes = husbandry?.notes?.[locale];
  const normalizeNote = (note: string) => note.replace(/\s+/g, " ").trim();
  const extraCareNote = Object.values(notes || {}).reduce(
    (remaining, note) => remaining.replaceAll(normalizeNote(note), "").trim(),
    normalizeNote(e.care?.notes[locale] || ""),
  );
  const feeding = e.kind === "fish" ? e.care?.feeding : undefined;
  const careNotes = { ...notes };
  if (feeding && !careNotes.feeding) careNotes.feeding = "";
  const recommendations = recommendedMixing(release, e.id, locale);
  return (
    <article className="page detail-page">
      <Link className="back-link" href={back}>
        {text(locale, "返回列表", "Back to results")}
      </Link>
      <section
        className={`detail-hero ${!e.media.photo ? "detail-no-photo" : ""}`}
      >
        <div className="detail-media">
          <Photo entity={e} locale={locale} priority />{e.media.photo&&<Link className="detail-photo-credit" href={`/${locale}/credits#${e.id}`}>{e.media.attribution||text(locale,"图片鸣谢","Image credits")}</Link>}
        </div>
        <div className="detail-identity">
          <span className="kind-name">{kindName(e.kind, locale)}</span>
          <h1>{entityName(e,locale)}</h1>
          <i className="scientific-name">{e.scientificName}</i>
          <p>{e.locales[locale]?.summary}</p>
        </div>
      </section>
      <section className="detail-section">
        <h2>{text(locale, "基础条件", "At a glance")}</h2>
        <EntityFacts entity={e} locale={locale} />
      </section>
      <div className={`detail-columns${recommendations.length ? "" : " detail-columns-single"}`}>
        <section className="detail-section">
          <h2>
            {text(
              locale,
              e.material ? "使用与准备" : "环境与照料",
              e.material ? "Preparation & use" : "Habitat & care",
            )}
          </h2>
          <p className="prose">{e.locales[locale]?.description}</p>
          {husbandry?.alerts?.map((alert, i) => (
            <p className="care-note" key={i}>
              {alert.text[locale]}
            </p>
          ))}
          {Object.keys(careNotes).length > 0 && (
            <dl className="care-rows">
              {Object.entries(careNotes).map(([key, note]) => (
                <div key={key}>
                  <dt>
                    {text(
                      locale,
                      (
                        {
                          oxygen: "溶氧",
                          flow: "水流",
                          space: "活动空间",
                          group: "群体",
                          feeding: "喂食",
                          setup: "布置",
                        } as Record<string, string>
                      )[key] || key,
                      (
                        {
                          oxygen: "Oxygen",
                          flow: "Flow",
                          space: "Space",
                          group: "Group",
                          feeding: "Feeding",
                          setup: "Setup",
                        } as Record<string, string>
                      )[key] || key,
                    )}
                  </dt>
                  <dd>
                    {note && <p>{note}</p>}
                    {key === "feeding" && feeding && (
                      <div className="feeding-guidance">
                        <p className="feeding-size">
                          {feeding.pelletDiameterMm
                            ? text(
                                locale,
                                `颗粒直径 ${range(feeding.pelletDiameterMm)} mm`,
                                `Pellet diameter ${range(feeding.pelletDiameterMm)} mm`,
                              )
                            : text(locale, "啃食沉底片", "Grazing on sinking wafers")}
                          <span>{text(locale, "成鱼参考", "Adult reference")}</span>
                        </p>
                        <p>{feeding.advice[locale]}</p>
                      </div>
                    )}
                  </dd>
                </div>
              ))}
            </dl>
          )}
          {extraCareNote && <p className="care-note">{extraCareNote}</p>}
          {e.material && <p>{e.material.preparation[locale]}</p>}
          {e.care && (
            <dl className="facts">
              <div>
                <dt>{text(locale, "溶氧需求", "Oxygen demand")}</dt>
                <dd>
                  {value(e.care.oxygen, locale, (v) =>
                    v === "high"
                      ? text(
                          locale,
                          "高；需另行核对增氧条件",
                          "High; check oxygen provision",
                        )
                      : text(
                          locale,
                          "常规需求，仍需核对实际条件",
                          "Standard; verify actual conditions",
                        ),
                  )}
                </dd>
              </div>
              <div>
                <dt>{text(locale, "食性", "Diet")}</dt>
                <dd>{value(e.care.diet, locale, (diet) => optionLabel("diet", diet, locale, release))}</dd>
              </div>
            </dl>
          )}
        </section>
        {recommendations.length > 0 && (
          <section className="detail-section mixing-section">
            <h2>{text(locale, "适合混养", "Suggested tankmates")}</h2>
            {recommendations.map(({ rule, other }) => (
              <div className="mixing-note" key={other.id}>
                <h3>
                  <Link href={`/${locale}/aquatic/${other.id}?from=${encodeURIComponent(back)}`}>
                    {entityName(other, locale)}
                  </Link>
                </h3>
                {rule.reason[locale] && <p>{rule.reason[locale]}</p>}
                {rule.condition[locale] && <p>{rule.condition[locale]}</p>}
              </div>
            ))}
          </section>
        )}
      </div>
      <div className="detail-bottom">
        <Link href={back}>{text(locale, "返回列表", "Back to results")}</Link>
      </div>
    </article>
  );
}
