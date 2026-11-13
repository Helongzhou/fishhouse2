import { getPublicRelease, getReleaseManifest } from "@/server/catalog";
import { catalogHeaders } from "@/server/http";
export async function GET(
  request: Request,
  { params }: { params: Promise<{ id: string; resource: string }> },
) {
  const { id, resource } = await params;
  try {
    const release = await getPublicRelease(id);
    if (resource === "manifest")
      return Response.json(await getReleaseManifest(id), {
        headers: catalogHeaders,
      });
    if (resource === "catalog")
      return Response.json(release, { headers: catalogHeaders });
    return Response.json({ error: "Not found" }, { status: 404 });
  } catch {
    return Response.json(
      { error: "Release not found" },
      { status: 404, headers: { "Cache-Control": "no-store" } },
    );
  }
}
