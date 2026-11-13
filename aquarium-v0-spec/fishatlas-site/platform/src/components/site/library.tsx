"use client";
import { useEffect, useMemo, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import type { CatalogRelease, Locale } from "@/domain/types";
import { EntityCard } from "./shared";
import { kindName, text } from "./format";
import {
  animalKinds,
  availableWaterTypes,
  matchesType,
  publicType,
  presetFilters,
  removeActiveFilter,
  filterEntities,
  normalizeFilters,
  numericError,
  optionLabel,
  traitValues,
  values,
} from "./library-filters";

export function Library({
  release,
  locale,
  materials = false,
}: {
  release: CatalogRelease;
  locale: Locale;
  materials?: boolean;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const sp = useSearchParams();
  const current = sp.toString();
  const source = useMemo(
    () =>
      release.catalog.filter(
        (e) =>
          e.active &&
          (materials
            ? !animalKinds.includes(e.kind) && e.kind !== "plant"
            : animalKinds.includes(e.kind) || e.kind === "plant"),
      ),
    [release, materials],
  );
  const query = normalizeFilters(current, source, release.categories);
  const normalized = query.toString();
  const [draft, setDraft] = useState(normalized);
  const [error, setError] = useState("");
  const [expanded, setExpanded] = useState(false);
  useEffect(() => { const media=window.matchMedia('(min-width: 768px)'); const update=()=>setExpanded(media.matches); update(); media.addEventListener('change',update); return()=>media.removeEventListener('change',update); }, []);
  useEffect(() => {
    setDraft(normalized);
    setError("");
  }, [normalized]);
  useEffect(() => {
    document.documentElement.lang = locale;
    try {
      const scroll = sessionStorage.getItem(`scroll:${pathname}?${normalized}`);
      if (scroll)
        requestAnimationFrame(() => window.scrollTo(0, Number(scroll)));
    } catch {}
  }, [locale, pathname]);
  const results = filterEntities(source, query, locale, release.categories);
  const urlError = numericError(query, locale);
  const dp = new URLSearchParams(draft);
  const type = query.get("type");
  const isPlant = type === "plant";
  const scoped = source.filter((e) => matchesType(e, type));
  const waterChoices = availableWaterTypes(scoped);
  const go = (p: URLSearchParams) =>
    router.push(`${pathname}${p.size ? `?${p}` : ""}`, { scroll: false });
  function setParam(key: string, value: string) {
    const p = new URLSearchParams(normalized);
    value ? p.set(key, value) : p.delete(key);
    if (key === "type") {
      const tags = new Set(
        source.filter((e) => matchesType(e, value)).flatMap((e) => traitValues(e,"category",release.categories)),
      );
      const categories = values(p, "category").filter((v) => tags.has(v));
      p.delete("category");
      if (categories.length) p.set("category", categories.join(","));
      if (value === "plant" || materials)
        for (const key of [
          "social",
          "body",
          "size",
          "reproduction",
          "layer",
          "oxygen",
          "flow",
          "difficulty",
          "tank",
          "temperament",
          "diet",
        ])
          p.delete(key);
    }
    go(p);
  }
  function updateDraft(key: string, value: string) {
    const p = new URLSearchParams(draft);
    value ? p.set(key, value) : p.delete(key);
    setDraft(p.toString());
  }
  function toggle(key: string, value: string, checked: boolean) {
    const p = new URLSearchParams(draft);
    const next = values(p, key).filter((v) => v !== value);
    if (checked) next.push(value);
    p.delete(key);
    if (next.length) p.set(key, next.join(","));
    setDraft(p.toString());
  }
  function apply(event: React.FormEvent) {
    event.preventDefault();
    const invalid = numericError(dp, locale);
    if (invalid) {
      setError(invalid);
      return;
    }
    setError("");
    go(dp);
    setExpanded(false);
    document.getElementById("filter-trigger")?.focus();
  }
  function reset() {
    setDraft("");
    setError("");
    go(new URLSearchParams());
  }
  function remove(key: string, value: string) {
    go(removeActiveFilter(query, key, value));
  }
  const active = [...query.keys()]
    .filter((key) => !["q", "sort"].includes(key) && (key !== "type" || type === "shrimp" || type === "snail"))
    .flatMap((key) => values(query, key).map((value) => ({ key, value })));
  const activeLabel = (key: string, value: string) => key === "type"
    ? kindName(value, locale)
    : optionLabel(key, value, locale, release);
  const options = (
    key: string,
    title: string,
    candidates = scoped.flatMap((e) => traitValues(e, key, release.categories)),
  ) => {
    const choices = [...new Set(candidates)].filter(Boolean);
    if (!choices.length) return null;
    return (
      <fieldset>
        <legend>{title}</legend>
        <div className="filter-options">
          {choices.map((v) => (
            <label key={v}>
              <input
                type="checkbox"
                name={key}
                value={v}
                checked={values(dp, key).includes(v)}
                onChange={(ev) => toggle(key, v, ev.target.checked)}
              />
              {optionLabel(key, v, locale, release)}
            </label>
          ))}
        </div>
      </fieldset>
    );
  };
  const select = (key: string, title: string) => (
    <label className="field">
      {title}
      <select
        name={key}
        value={dp.get(key) || ""}
        onChange={(e) => updateDraft(key, e.target.value)}
      >
        <option value="">{text(locale, "全部", "All")}</option>
        {[...new Set(scoped.flatMap((e) => traitValues(e, key, release.categories)))].map((v) => (
          <option key={v} value={v}>
            {optionLabel(key, v, locale, release)}
          </option>
        ))}
      </select>
    </label>
  );
  return (
    <div className="page library-page">
      <div className="page-title-row">
        <h1>
          {text(
            locale,
            materials ? "造景材料" : "水族库",
            materials ? "Aquascaping materials" : "Aquatic library",
          )}
        </h1>
        <Link href={`/${locale}/${materials ? "aquatic" : "materials"}`}>
          {text(
            locale,
            materials ? "查看生物" : "查看材料",
            materials ? "Browse living things" : "Browse materials",
          )}
        </Link>
      </div>
      <form
        className="search-form library-search"
        onSubmit={(ev) => {
          ev.preventDefault();
          setParam("q", new FormData(ev.currentTarget).get("q") as string);
        }}
      >
        <input
          name="q"
          type="search"
          autoComplete="off"
          aria-label={text(locale, "搜索水族库", "Search aquatic library")}
          placeholder={text(
            locale,
            "搜索名称、学名或别名…",
            "Search names, scientific names or aliases…",
          )}
          defaultValue={query.get("q") || ""}
          key={query.get("q")}
        />
        <button type="submit">{text(locale, "搜索", "Search")}</button>
      </form>
      {!materials && waterChoices.length > 0 && (
        <div className="water-tabs" aria-label={text(locale, "水体", "Water type")}>
          <span>{text(locale, "水体", "Water type")}</span>
          <button onClick={() => setParam("water", "")} aria-pressed={!query.has("water")}>
            {text(locale, "全部", "All")}
          </button>
          {waterChoices.map((water) => (
            <button key={water} onClick={() => setParam("water", water)} aria-pressed={values(query, "water").includes(water)}>
              {optionLabel("water", water, locale, release)}
            </button>
          ))}
        </div>
      )}
      <div className="type-tabs" aria-label={text(locale, "类型", "Types")}>
        <button onClick={() => setParam("type", "")} aria-pressed={!type}>
          {text(locale, "全部", "All")}
        </button>
        {(materials
          ? ["rock", "sand", "wood", "substrate"]
          : ["fish", "plant", "invertebrate"]
        ).map((k) => (
          <button
            key={k}
            onClick={() => setParam("type", k)}
            aria-pressed={publicType({ kind: type || "" }) === k}
          >
            {kindName(k, locale)}
          </button>
        ))}
      </div>
      {!materials && !isPlant && (
        <div
          className="preset-bar"
          aria-label={text(locale, "快捷筛选", "Quick filters")}
        >
          {[
            {
              label: text(locale, "40 L 以内", "Up to 40 L"),
              params: { tank: "40" },
            },
            {
              label: text(locale, "温和群游", "Peaceful schools"),
              params: { social: "schooling", temperament: "peaceful" },
            },
            {
              label: text(locale, "上层", "Top swimmers"),
              params: { layer: "top" },
            },
            {
              label: text(locale, "中层群游", "Midwater schools"),
              params: { layer: "mid", social: "schooling" },
            },
            {
              label: text(locale, "底层", "Bottom dwellers"),
              params: { layer: "bottom" },
            },
            {
              label: text(locale, "卵胎生", "Livebearers"),
              params: { reproduction: "livebearing" },
            },
          ].map((preset) => (
            <button
              className="text-button"
              key={preset.label}
              aria-pressed={Object.entries(preset.params).every(
                ([key, value]) => query.get(key) === value,
              )}
              onClick={() => {
                go(presetFilters(query, preset.params));
              }}
            >
              {preset.label}
            </button>
          ))}
        </div>
      )}
      {active.length > 0 && (
        <div
          className="active-filters"
          aria-label={text(locale, "已选条件", "Selected filters")}
        >
          {active.map(({ key, value }) => (
            <button
              key={`${key}-${value}`}
              className="active-chip secondary"
              onClick={() => remove(key, value)}
              aria-label={`${text(locale, "移除", "Remove")} ${activeLabel(key, value)}`}
            >
              {activeLabel(key, value)}{" "}
              <span aria-hidden="true">×</span>
            </button>
          ))}
        </div>
      )}
      <div className="library-layout">
        <details
          className="filter-shell"
          open={expanded}
          onToggle={(ev) => setExpanded(ev.currentTarget.open)}
        >
          <summary id="filter-trigger">
            {text(locale, "筛选", "Filters")}
            {active.length ? ` (${active.length})` : ""}
          </summary>
          <form className="filter-form" id="filter-form" onSubmit={apply}>
            {options("category", text(locale, "分类", "Category"))}
            {options("color", text(locale, "颜色", "Color"))}
            {!materials && !isPlant && (
              <>
                <label className="field">
                  {text(locale, "我的缸体 L", "My tank volume L")}
                  <input
                    name="tank"
                    type="number"
                    min="1"
                    max="100000"
                    value={dp.get("tank") || ""}
                    onChange={(e) => updateDraft("tank", e.target.value)}
                  />
                </label>
                {select(
                  "difficulty",
                  text(locale, "饲养难度", "Care difficulty"),
                )}
                <details className="advanced-filters">
                  <summary>
                    {text(locale, "更多条件", "More conditions")}
                  </summary>
                  {options("size", text(locale, "成体大小", "Adult size"))}
                  {options(
                    "reproduction",
                    text(locale, "繁殖方式", "Reproduction"),
                  )}
                  {options("social", text(locale, "群体", "Social pattern"))}
                  {options("body", text(locale, "体形", "Body shape"))}
                  {options("layer", text(locale, "水层", "Water layer"))}
                  {select("temperament", text(locale, "性情", "Temperament"))}
                  {select("diet", text(locale, "食性", "Diet"))}
                  {select("oxygen", text(locale, "溶氧需求", "Oxygen demand"))}
                  {options("flow", text(locale, "水流", "Flow"), [
                    "minimal",
                    "gentle",
                    "moderate",
                    "strong",
                  ])}
                  {(["temp", "ph"] as const).map((key) => (
                    <fieldset key={key}>
                      <legend>
                        {key === "temp"
                          ? text(locale, "水温 °C", "Temperature °C")
                          : "pH"}
                      </legend>
                      <div className="range-inputs">
                        {["Min", "Max"].map((m, i) => (
                          <input
                            key={m}
                            name={`${key}${m}`}
                            aria-label={`${key === "temp" ? text(locale, "水温", "Temperature") : "pH"} ${text(locale, i ? "最高" : "最低", i ? "maximum" : "minimum")}`}
                            type="number"
                            min="0"
                            max={key === "temp" ? 40 : 14}
                            step="0.1"
                            value={dp.get(`${key}${m}`) || ""}
                            placeholder={text(
                              locale,
                              i ? "最高" : "最低",
                              i ? "Max" : "Min",
                            )}
                            onChange={(e) =>
                              updateDraft(`${key}${m}`, e.target.value)
                            }
                          />
                        ))}
                      </div>
                    </fieldset>
                  ))}
                  <p className="hint">
                    {text(
                      locale,
                      "区间重叠即匹配；未知条件不视为满足。",
                      "Ranges match when they overlap. Unknown values do not satisfy a filter.",
                    )}
                  </p>
                </details>
              </>
            )}
            {error && (
              <p id="filter-error" role="alert" className="error">
                {error}
              </p>
            )}
            <div className="button-row">
              <button type="button" className="secondary" onClick={reset}>
                {text(locale, "清空", "Clear")}
              </button>
              <button type="submit">{text(locale, "应用", "Apply")}</button>
            </div>
          </form>
        </details>
        <section aria-label={text(locale, "搜索结果", "Search results")}>
          <div className="result-toolbar">
            <span aria-live="polite">
              {results.length} {text(locale, "项", "results")}
            </span>
            <label>
              {text(locale, "排序", "Sort")}
              <select
                name="sort"
                value={query.get("sort") || ""}
                onChange={(e) => setParam("sort", e.target.value)}
              >
                <option value="">{text(locale, "默认", "Default")}</option>
                <option value="name">{text(locale, "名称", "Name")}</option>
                <option value="length">
                  {text(locale, "体型从小到大", "Adult length, small first")}
                </option>
                <option value="tank">
                  {text(locale, "缸体从小到大", "Tank volume, small first")}
                </option>
                <option value="difficulty">
                  {text(locale, "难度", "Difficulty")}
                </option>
              </select>
            </label>
          </div>
          {urlError ? (
            <div className="empty-state error" role="alert">
              <p>{urlError}</p>
              <button onClick={reset}>
                {text(locale, "清空筛选", "Clear filters")}
              </button>
            </div>
          ) : (
            <div className="entity-grid">
              {results.map((entity) => (
                <EntityCard
                  key={entity.id}
                  entity={entity}
                  locale={locale}
                  returnTo={`${pathname}?${normalized}`}
                />
              ))}
            </div>
          )}
          {!urlError && !results.length && (
            <div className="empty-state">
              <p>
                {text(
                  locale,
                  "没有符合这些条件的条目。",
                  "No entries match these conditions.",
                )}
              </p>
              <button onClick={reset}>
                {text(locale, "清空筛选", "Clear filters")}
              </button>
            </div>
          )}
        </section>
      </div>
    </div>
  );
}
