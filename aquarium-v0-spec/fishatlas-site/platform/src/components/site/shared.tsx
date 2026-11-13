"use client";
import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import { useEffect, useState, type ReactNode } from "react";
import type { CatalogEntity, Knowledge, Locale, Range } from "@/domain/types";
export { text, kindName, value, range } from "./format";
import { text, kindName, value, range, entityName, entityUnit } from "./format";
export function SiteShell({
  locale,
  children,
}: {
  locale: Locale;
  children: ReactNode;
}) {
  const pathname = usePathname();
  const search = useSearchParams();
  useEffect(() => {
    document.documentElement.lang = locale;
  }, [locale]);
  const other = locale === "zh-CN" ? "en" : "zh-CN";
  const languageQuery = new URLSearchParams(search.toString());
  const from = languageQuery.get("from");
  if (from)
    languageQuery.set("from", from.replace(`/${locale}/`, `/${other}/`));
  return (
    <>
      <a className="skip-link" href="#main">
        {text(locale, "跳至内容", "Skip to content")}
      </a>
      <header className="site-header">
        <Link className="brand" href={`/${locale}`} aria-label="FishAtlas">
          <span className="brand-mark" aria-hidden="true">
            ⌁
          </span>{" "}
          FishAtlas
        </Link>
        <nav aria-label={text(locale, "主导航", "Main navigation")}>
          <Link
            aria-current={pathname.includes("/aquatic") ? "page" : undefined}
            href={`/${locale}/aquatic`}
          >
            {text(locale, "水族库", "Aquatic library")}
          </Link>
          <Link
            aria-current={pathname.includes("/materials") ? "page" : undefined}
            href={`/${locale}/materials`}
          >
            {text(locale, "造景材料", "Materials")}
          </Link>
        </nav>
        <Link
          className="language-link"
          lang={other}
          href={`${pathname.replace(`/${locale}`, `/${other}`)}${languageQuery.size ? `?${languageQuery.toString()}` : ""}`}
        >
          {other === "en" ? "EN" : "中文"}
        </Link>
      </header>
      <main id="main">{children}</main>
      <footer className="site-footer">
        <Link className="brand" href={`/${locale}`}>
          FishAtlas
        </Link>
        <Link href={`/${locale}/materials`}>
          {text(locale, "造景材料", "Aquascaping materials")}
        </Link>
        <Link href={`/${locale}/credits`}>{text(locale,"图片鸣谢","Image credits")}</Link><span>
          {text(
            locale,
            "认识生命，照料日常。",
            "Know aquatic life. Care for it.",
          )}
        </span>
      </footer>
    </>
  );
}
export function Photo({
  entity,
  locale,
  priority = false,
  className = "",
}: {
  entity: CatalogEntity;
  locale: Locale;
  priority?: boolean;
  className?: string;
}) {
  const [failed, setFailed] = useState(false);
  if (!entity.media.photo || failed)
    return (
      <div className={`photo-missing ${className}`}>
        {text(locale, "照片待补充", "Photo not available")}
      </div>
    );
  return (
    <img
      className={`entity-photo ${className}`}
      src={entity.media.photo}
      alt={entity.locales[locale]?.alt||entity.scientificName||entity.id}
      width="800"
      height="600"
      loading={priority ? "eager" : "lazy"}
      fetchPriority={priority ? "high" : "auto"}
      onError={() => setFailed(true)}
    />
  );
}
export function EntityFacts({
  entity: e,
  locale,
  compact = false,
}: {
  entity: CatalogEntity;
  locale: Locale;
  compact?: boolean;
}) {
  let facts: [string, string][] = e.care
    ? [
        [
          text(locale, "成体体长", "Adult length"),
          value(e.care.adultLengthMm, locale, (v) => `${v / 10} cm`),
        ],
        [
          text(locale, "水温", "Temperature"),
          value(e.care.temperatureC, locale, (v) => `${range(v)} °C`),
        ],
        [
          text(locale, "最小缸体", "Minimum tank"),
          value(e.care.minimumTankL, locale, (v) => `${v} L`),
        ],
        [
          text(locale, "群体", "Group"),
          value(
            e.care.group,
            locale,
            (v) => `${v.minimum}${text(locale, " 起", " minimum")}`,
          ),
        ],
      ]
    : e.plant
      ? [
          [
            text(locale, "成株高度", "Mature height"),
            `${e.plant.adultHeightMm.min / 10}–${e.plant.adultHeightMm.max / 10} cm`,
          ],
          [
            text(locale, "种植方式", "Planting"),
            text(
              locale,
              e.plant.attachment === "epiphyte"
                ? "附生"
                : e.plant.attachment === "rooted"
                  ? "底栽"
                  : "漂浮",
              e.plant.attachment,
            ),
          ],
          [
            text(locale, "光照", "Light"),
            value(e.plant.light, locale, (v) =>
              v
                .map(
                  (s) =>
                    ({
                      low: text(locale, "低", "Low"),
                      medium: text(locale, "中", "Medium"),
                      high: text(locale, "高", "High"),
                    })[s],
                )
                .join(" / "),
            ),
          ],
          [
            "CO₂",
            value(
              e.plant.co2,
              locale,
              (v) =>
                ({
                  optional: text(locale, "可选", "Optional"),
                  recommended: text(locale, "建议", "Recommended"),
                  required: text(locale, "需要", "Required"),
                  none: text(locale, "不需", "None"),
                })[v],
            ),
          ],
        ]
      : [
          [
            text(locale, "尺寸", "Dimensions"),
            `${e.scene.widthMm / 10} × ${e.scene.depthMm / 10} × ${e.scene.heightMm / 10} cm`,
          ],
          [text(locale, "单位", "Unit"), entityUnit(e,locale)],
          [
            text(locale, "水质影响", "Water effect"),
            value(
              e.material?.waterEffect,
              locale,
              (v) =>
                ({
                  inert: text(locale, "惰性", "Inert"),
                  tannins: text(locale, "单宁释出", "Tannins"),
                  softening: text(locale, "软化", "Softening"),
                  hardening: text(locale, "增硬", "Hardening"),
                  conditional: text(locale, "视条件而定", "Conditional"),
                })[v],
            ),
          ],
        ];
  if (!compact && e.care)
    facts = [
      ...facts,
      ["pH", value(e.care.ph, locale, range)],
      ["GH", value(e.care.gh, locale, (v) => `${range(v)} °dGH`)],
      [
        text(locale, "最小缸长", "Minimum length"),
        value(e.care.minimumTankLengthMm, locale, (v) => `${v / 10} cm`),
      ],
      [
        text(locale, "水流", "Flow"),
        value(e.care.flow, locale, (v) =>
          v
            .map(
              (s) =>
                ({
                  minimal: text(locale, "极弱", "Minimal"),
                  gentle: text(locale, "轻柔", "Gentle"),
                  moderate: text(locale, "中等", "Moderate"),
                  strong: text(locale, "强", "Strong"),
                })[s],
            )
            .join(" / "),
        ),
      ],
    ];
  if (!compact && e.care) {
    const keys = [
      "adultLengthMm",
      "temperatureC",
      "minimumTankL",
      "group",
      "ph",
      "gh",
      "minimumTankLengthMm",
      "flow",
    ] as const;
    facts = facts.map((fact, i) => [
      fact[0],
      `${fact[1]}${(e.care?.editorialState?.[keys[i]] === "needsReview" || e.care?.[keys[i]].editorialState === "needsReview") ? text(locale, "（待核对）", " (needs review)") : ""}`,
    ]);
  }
  return (
    <dl className={compact ? "facts compact-facts" : "facts"}>
      {facts.map(([label, val]) => (
        <div key={label}>
          <dt>{label}</dt>
          <dd>{val}</dd>
        </div>
      ))}
    </dl>
  );
}
export function EntityCard({
  entity,
  locale,
  returnTo = "",
}: {
  entity: CatalogEntity;
  locale: Locale;
  returnTo?: string;
}) {
  return (
    <article
      className={`entity-card ${!entity.media.photo ? "without-photo" : ""}`}
    >
      <Link
        className="card-image-link"
        onClick={() => {
          try {
            sessionStorage.setItem(
              `scroll:${returnTo}`,
              String(window.scrollY),
            );
          } catch {}
        }}
        href={`/${locale}/aquatic/${entity.id}${returnTo ? `?from=${encodeURIComponent(returnTo)}` : ""}`}
      >
        <Photo entity={entity} locale={locale} />
      </Link>
      <div className="card-content">
        <Link
          className="card-title"
          onClick={() => {
            try {
              sessionStorage.setItem(
                `scroll:${returnTo}`,
                String(window.scrollY),
              );
            } catch {}
          }}
          href={`/${locale}/aquatic/${entity.id}${returnTo ? `?from=${encodeURIComponent(returnTo)}` : ""}`}
        >
          <h2>{entityName(entity,locale)}</h2>
        </Link>
        <i className="scientific-name">{entity.scientificName}</i>
        <EntityFacts entity={entity} locale={locale} compact />
      </div>
    </article>
  );
}
