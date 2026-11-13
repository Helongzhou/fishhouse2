import { Suspense, type ReactNode } from "react";
import { notFound } from "next/navigation";
import { SiteShell } from "@/components/site/shared";
import type { Locale } from "@/domain/types";
export default async function LocaleLayout({
  children,
  params,
}: {
  children: ReactNode;
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  if (locale !== "zh-CN" && locale !== "en") notFound();
  return (
    <Suspense>
      <SiteShell locale={locale as Locale}>{children}</SiteShell>
    </Suspense>
  );
}
