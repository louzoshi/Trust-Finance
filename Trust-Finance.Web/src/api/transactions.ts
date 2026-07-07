import { http } from "../lib/http";
import type { Transaction, TransactionPayload } from "../types/api";

export function listTransactions(): Promise<Transaction[]> {
  return http.get<Transaction[]>("/transactions");
}

export function createTransaction(
  payload: TransactionPayload,
): Promise<Transaction> {
  return http.post<Transaction>("/transactions", payload);
}

export function updateTransaction(
  id: number,
  payload: TransactionPayload,
): Promise<Transaction> {
  return http.put<Transaction>(`/transactions/${id}`, payload);
}

export function deleteTransaction(id: number): Promise<Transaction> {
  return http.delete<Transaction>(`/transactions/${id}`);
}
