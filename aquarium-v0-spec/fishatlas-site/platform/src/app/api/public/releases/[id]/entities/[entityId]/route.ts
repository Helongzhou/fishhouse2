import { getPublicRelease } from "@/server/catalog";
import { catalogHeaders } from "@/server/http";
export async function GET(
  _request: Request,
  { params }: { params: Promise<{ id: string; entityId: string }> },
) {
  try {
    const { id, entityId } = await params;
    const entity = (await getPublicRelease(id)).catalog.find(
      (e) => e.id === entityId,
    );
    return entity
      ? Response.json(entity, { headers: catalogHeaders })
      : Response.json({ error: "Not found" }, { status: 404 });
  } catch {
    return Response.json({ error: "Release not found" }, { status: 404 });
  }
}
