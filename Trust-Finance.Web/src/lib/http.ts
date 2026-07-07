import type { ApiResult } from "../types/api";

const BASE_URL: string = import.meta.env.VITE_API_URL ?? "/api";
const TOKEN_KEY = "tf.token";

export class ApiError extends Error {
  readonly status: number;
  readonly errors: string[];

  constructor(status: number, errors: string[]) {
    super(errors[0] ?? `Request failed with status ${status}`);
    this.status = status;
    this.errors = errors;
  }
}

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token);
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY);
}

function isEnvelope(body: unknown): body is ApiResult<unknown> {
  return (
    typeof body === "object" &&
    body !== null &&
    ("data" in body || "errors" in body)
  );
}

async function request<T>(
  method: string,
  path: string,
  body?: unknown,
): Promise<T> {
  const headers: Record<string, string> = {};
  if (body !== undefined) headers["Content-Type"] = "application/json";

  const token = getToken();
  if (token) headers["Authorization"] = `Bearer ${token}`;

  const response = await fetch(`${BASE_URL}${path}`, {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });

  if (response.status === 401 && token) {
    // Token expired or revoked: drop it and restart at the login page.
    clearToken();
    window.location.assign("/login");
    throw new ApiError(401, ["Session expired"]);
  }

  const text = await response.text();
  const parsed: unknown = text ? JSON.parse(text) : null;

  if (!response.ok) {
    const errors = isEnvelope(parsed)
      ? parsed.errors
      : [`Request failed with status ${response.status}`];
    throw new ApiError(response.status, errors);
  }

  // Most endpoints wrap results in ResultViewModel; a few return the
  // entity directly, so tolerate both shapes.
  if (isEnvelope(parsed)) return parsed.data as T;
  return parsed as T;
}

export const http = {
  get: <T>(path: string) => request<T>("GET", path),
  post: <T>(path: string, body: unknown) => request<T>("POST", path, body),
  put: <T>(path: string, body: unknown) => request<T>("PUT", path, body),
  delete: <T>(path: string) => request<T>("DELETE", path),
};
