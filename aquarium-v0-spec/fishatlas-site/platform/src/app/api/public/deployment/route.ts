import { getActivePointer, getReleaseManifest } from "@/server/catalog";
export async function GET() {
  const active = await getActivePointer();
  return Response.json(
    {
      ...active,
      manifest: active.releaseId
        ? await getReleaseManifest(active.releaseId)
        : null,
    },
    { headers: { "Cache-Control": "no-store" } },
  );
}
