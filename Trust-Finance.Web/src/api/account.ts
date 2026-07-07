import { http } from "../lib/http";
import type { LoginPayload, RegisterPayload } from "../types/api";

export function login(payload: LoginPayload): Promise<string> {
  return http.post<string>("/account/login", payload);
}

export function register(payload: RegisterPayload): Promise<unknown> {
  return http.post<unknown>("/account/register", payload);
}
