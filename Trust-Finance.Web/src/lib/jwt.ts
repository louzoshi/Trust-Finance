import type { AuthUser } from "../types/api";

// ASP.NET Core issues claims under the full XML-schema URIs.
const CLAIM_ID =
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
const CLAIM_NAME = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";
const CLAIM_ROLE = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

interface JwtPayload {
  [claim: string]: unknown;
  exp?: number;
}

function decodePayload(token: string): JwtPayload | null {
  const segments = token.split(".");
  if (segments.length !== 3) return null;
  try {
    const base64 = segments[1].replace(/-/g, "+").replace(/_/g, "/");
    return JSON.parse(atob(base64)) as JwtPayload;
  } catch {
    return null;
  }
}

/** Returns the authenticated user from a JWT, or null if invalid/expired. */
export function userFromToken(token: string): AuthUser | null {
  const payload = decodePayload(token);
  if (!payload) return null;

  if (payload.exp !== undefined && payload.exp * 1000 < Date.now()) return null;

  const id = Number(payload[CLAIM_ID]);
  const name = payload[CLAIM_NAME];
  const role = payload[CLAIM_ROLE];
  if (!Number.isFinite(id) || typeof name !== "string") return null;

  return { id, name, role: typeof role === "string" ? role : "user" };
}
