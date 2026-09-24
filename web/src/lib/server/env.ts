import "server-only";

/** Server-only secrets. Importing this module from a client component fails the build (server-only). */
export function serviceRoleKey(): string | null {
  return process.env.SUPABASE_SERVICE_ROLE_KEY || null;
}

export function treasurySecretKey(): Uint8Array | null {
  const raw = process.env.TREASURY_SECRET_KEY;
  if (!raw) return null;
  try {
    const arr: unknown = JSON.parse(raw);
    if (!Array.isArray(arr) || arr.length !== 64 || !arr.every((n) => Number.isInteger(n) && n >= 0 && n <= 255)) return null;
    return Uint8Array.from(arr as number[]);
  } catch {
    return null;
  }
}
