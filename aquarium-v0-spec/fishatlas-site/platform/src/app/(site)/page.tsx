import { redirect } from "next/navigation";
export default async function RootPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const q = await searchParams;
  const locale = q.lang === "en" ? "en" : "zh-CN";
  const view = q.view;
  const p = new URLSearchParams();
  Object.entries(q).forEach(([k, v]) => {
    if (!["lang", "view", "id", "species"].includes(k) && v)
      for (const x of Array.isArray(v) ? v : [v]) p.append(k, x);
  });
  const suffix = p.size ? `?${p}` : "";
  if (view === "database") redirect(`/${locale}/aquatic${suffix}`);
  if (view === "detail" && (q.id || q.species))
    redirect(`/${locale}/aquatic/${q.id || q.species}${suffix}`);
  if (view === "calculator") redirect(`/${locale}/aquatic${suffix}`);
  redirect(`/${locale}${suffix}`);
}
