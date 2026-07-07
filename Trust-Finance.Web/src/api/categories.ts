import { http } from "../lib/http";
import type { Category, CategoryPayload } from "../types/api";

export function listCategories(): Promise<Category[]> {
  return http.get<Category[]>("/categories");
}

export function createCategory(payload: CategoryPayload): Promise<Category> {
  return http.post<Category>("/categories", payload);
}
