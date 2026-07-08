import type { AuthUser } from "../types/api";

// JwtSecurityTokenHandler maps claim types to short JWT names on the way
// out (nameid/unique_name/role); the full XML-schema URIs are kept as a
// fallback in case the API ever disables that mapping.
const CLAIM_ID = [
  "nameid",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
];
const CLAIM_NAME = [
  "unique_name",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
];
const CLAIM_ROLE = [
  "role",
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
];

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

function claim(payload: JwtPayload, keys: string[]): unknown {
  for (const key of keys) {
    if (payload[key] !== undefined) return payload[key];
  }
  return undefined;
}

/** Returns the authenticated user from a JWT, or null if invalid/expired. */
export function userFromToken(token: string): AuthUser | null {
  const payload = decodePayload(token);
  if (!payload) return null;

  if (payload.exp !== undefined && payload.exp * 1000 < Date.now()) return null;

  const id = Number(claim(payload, CLAIM_ID));
  const name = claim(payload, CLAIM_NAME);
  const role = claim(payload, CLAIM_ROLE);
  if (!Number.isFinite(id) || typeof name !== "string") return null;

  return { id, name, role: typeof role === "string" ? role : "user" };
}
