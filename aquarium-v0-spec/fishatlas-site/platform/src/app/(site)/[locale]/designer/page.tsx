import { redirect } from "next/navigation";
import type { Locale } from "@/domain/types";

export default async function RetiredDesignerPage({
  params,
}: {
  params: Promise<{ locale: Locale }>;
}) {
  const { locale } = await params;
  redirect(`/${locale}/aquatic`);
}
