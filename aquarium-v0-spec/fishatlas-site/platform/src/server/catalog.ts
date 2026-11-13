import { cache } from "react";
import { createHash } from "node:crypto";
import catalog from "@/data/catalog.json";
import type { CatalogRelease } from "@/domain/types";

const release = catalog as unknown as CatalogRelease;

export class ReleaseUnavailable extends Error {}

/** The checked-in catalog is the sole publication source; no database fallback. */
export const getPublicRelease = cache(
  async (releaseId?: string): Promise<CatalogRelease> => {
    if (releaseId !== undefined && releaseId !== release.id) {
      throw new ReleaseUnavailable("Release not found");
    }
    return release;
  },
);

export async function getActivePointer() {
  return { releaseId: release.id, generation: 1 };
}

export async function getReleaseManifest(id: string) {
  if (id !== release.id) return null;
  return {
    releaseId: release.id,
    createdAt: release.createdAt,
    source: "local-catalog",
    checksum: createHash("sha256").update(JSON.stringify(catalog)).digest("hex"),
    entityCount: release.catalog.length,
    capabilities: release.capabilities,
  };
}
