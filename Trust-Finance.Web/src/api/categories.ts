import { http } from "../lib/http";
import type { Category, CategoryPayload } from "../types/api";

export function listCategories(): Promise<Category[]> {
  return http.get<Category[]>("/categories");
}

export function createCategory(payload: CategoryPayload): Promise<Category> {
  return http.post<Category>("/categories", payload);
}

export function updateCategory(
  id: number,
  payload: CategoryPayload,
): Promise<Category> {
  return http.put<Category>(`/categories/${id}`, payload);
}

export function deleteCategory(id: number): Promise<Category> {
  return http.delete<Category>(`/categories/${id}`);
}
