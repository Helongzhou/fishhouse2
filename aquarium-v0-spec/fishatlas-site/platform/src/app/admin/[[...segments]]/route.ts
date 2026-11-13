/** Explicit retirement response avoids the locale page's streamed not-found status. */
function retired() {
  return new Response(null, {
    status: 410,
    headers: { "Cache-Control": "no-store" },
  });
}

export const GET = retired;
export const HEAD = retired;
export const POST = retired;
export const PUT = retired;
export const PATCH = retired;
export const DELETE = retired;
export const OPTIONS = retired;
