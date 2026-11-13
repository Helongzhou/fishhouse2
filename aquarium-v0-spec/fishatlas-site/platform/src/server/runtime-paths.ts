import path from "node:path";
/** Durable workspace/mounted volume root; standalone changes cwd to its build directory. */
export function getProjectRoot(): string {
  const configured = process.env.FISHATLAS_PROJECT_ROOT;
  if (configured) {
    if (!path.isAbsolute(configured))
      throw new Error(
        "FISHATLAS_PROJECT_ROOT must be an absolute persistent directory",
      );
    return path.normalize(configured);
  }
  const cwd = process.cwd();
  if (cwd.endsWith(`${path.sep}.next${path.sep}standalone`)) {
    throw new Error(
      "Standalone requires FISHATLAS_PROJECT_ROOT to point to the persistent project directory",
    );
  }
  return cwd;
}
function within(folder: string, parts: string[]) {
  const base = path.resolve(getProjectRoot(), folder);
  const destination = path.resolve(base, ...parts);
  if (destination !== base && !destination.startsWith(`${base}${path.sep}`)) {
    throw new Error("Asset path leaves its persistent directory");
  }
  return destination;
}
export const publicPath = (...parts: string[]) => within("public", parts);
export const mediaPath = (...parts: string[]) => within("media", parts);
