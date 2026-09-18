import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { listCategories } from "../../api/categories";
import {
  createTransaction,
  deleteTransaction,
  updateTransaction,
} from "../../api/transactions";
import { listTransactions } from "../../api/transactions";
import { ErrorList } from "../../components/ErrorList";
import {
  formatCurrency,
  formatDate,
  toDateInputValue,
} from "../../lib/format";
import type {
  Transaction,
  TransactionPayload,
  TransactionType,
} from "../../types/api";

interface FormState {
  description: string;
  amount: string;
  date: string;
  type: TransactionType;
  categoryId: string;
}

const emptyForm: FormState = {
  description: "",
  amount: "",
  date: toDateInputValue(new Date().toISOString()),
  // Most entries in a personal ledger are payments, so this is the cheaper
  // default: it is the one the user has to change least often.
  type: "Expense",
  categoryId: "",
};

function toPayload(form: FormState): TransactionPayload {
  return {
    description: form.description.trim(),
    amount: Number(form.amount),
    date: `${form.date}T00:00:00`,
    type: form.type,
    categoryId: Number(form.categoryId),
  };
}

export function TransactionsPage() {
  const queryClient = useQueryClient();
  const [form, setForm] = useState<FormState>(emptyForm);
  const [editingId, setEditingId] = useState<number | null>(null);

  const transactionsQuery = useQuery({
    queryKey: ["transactions"],
    queryFn: listTransactions,
  });
  const categoriesQuery = useQuery({
    queryKey: ["categories"],
    queryFn: listCategories,
  });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ["transactions"] });

  const saveMutation = useMutation({
    mutationFn: (payload: TransactionPayload) =>
      editingId === null
        ? createTransaction(payload)
        : updateTransaction(editingId, payload),
    onSuccess: () => {
      invalidate();
      setForm(emptyForm);
      setEditingId(null);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: deleteTransaction,
    onSuccess: invalidate,
  });

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    saveMutation.mutate(toPayload(form));
  }

  function startEdit(transaction: Transaction) {
    setEditingId(transaction.id);
    setForm({
      description: transaction.description,
      amount: String(transaction.amount),
      date: toDateInputValue(transaction.date),
      type: transaction.type,
      categoryId: String(transaction.categoryId),
    });
  }

  function cancelEdit() {
    setEditingId(null);
    setForm(emptyForm);
  }

  function handleDelete(transaction: Transaction) {
    if (window.confirm(`Delete "${transaction.description}"?`)) {
      deleteMutation.mutate(transaction.id);
    }
  }

  const categories = categoriesQuery.data ?? [];
  const categoryNames = new Map(categories.map((c) => [c.id, c.name]));
  const transactions = [...(transactionsQuery.data ?? [])].sort((a, b) =>
    b.date.localeCompare(a.date),
  );

  return (
    <>
      <h1 className="page-title">Transactions</h1>

      <section className="card">
        <h2 className="card-title">
          {editingId === null ? "New transaction" : "Edit transaction"}
        </h2>
        <form className="inline-form" onSubmit={handleSubmit}>
          <label>
            Description
            <input
              value={form.description}
              onChange={(e) =>
                setForm({ ...form, description: e.target.value })
              }
              minLength={3}
              maxLength={100}
              required
            />
          </label>
          <label>
            Amount
            <input
              type="number"
              step="0.01"
              min="0.01"
              value={form.amount}
              onChange={(e) => setForm({ ...form, amount: e.target.value })}
              required
            />
          </label>
          <label>
            Date
            <input
              type="date"
              value={form.date}
              onChange={(e) => setForm({ ...form, date: e.target.value })}
              required
            />
          </label>
          <label>
            Type
            <select
              value={form.type}
              onChange={(e) =>
                setForm({ ...form, type: e.target.value as TransactionType })
              }
              required
            >
              <option value="Expense">Expense</option>
              <option value="Income">Income</option>
            </select>
          </label>
          <label>
            Category
            <select
              value={form.categoryId}
              onChange={(e) => setForm({ ...form, categoryId: e.target.value })}
              required
            >
              <option value="" disabled>
                Select…
              </option>
              {categories.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </label>
          <div className="form-actions">
            <button
              type="submit"
              className="btn-primary"
              disabled={saveMutation.isPending}
            >
              {editingId === null ? "Add" : "Save"}
            </button>
            {editingId !== null && (
              <button type="button" className="btn-ghost" onClick={cancelEdit}>
                Cancel
              </button>
            )}
          </div>
        </form>
        <ErrorList error={saveMutation.error} />
        {categories.length === 0 && !categoriesQuery.isLoading && (
          <p className="page-status">
            Create a category first — transactions need one.
          </p>
        )}
      </section>

      <section className="card">
        <ErrorList error={transactionsQuery.error ?? deleteMutation.error} />
        {transactionsQuery.isLoading ? (
          <p className="page-status">Loading transactions…</p>
        ) : transactions.length === 0 ? (
          <p className="page-status">No transactions yet.</p>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Description</th>
                <th>Category</th>
                <th>Type</th>
                <th className="num">Amount</th>
                <th className="actions" aria-label="Actions" />
              </tr>
            </thead>
            <tbody>
              {transactions.map((t) => (
                <tr key={t.id}>
                  <td>{formatDate(t.date)}</td>
                  <td>{t.description}</td>
                  <td>{categoryNames.get(t.categoryId) ?? "—"}</td>
                  <td>
                    <span
                      className={
                        t.type === "Income"
                          ? "text-income border-income/40 rounded-full border px-2 py-0.5 text-xs"
                          : "text-expense border-expense/40 rounded-full border px-2 py-0.5 text-xs"
                      }
                    >
                      {t.type}
                    </span>
                  </td>
                  <td
                    className={
                      t.type === "Income"
                        ? "num text-income"
                        : "num text-expense"
                    }
                  >
                    {t.type === "Income" ? "+" : "−"}
                    {formatCurrency(t.amount)}
                  </td>
                  <td className="actions">
                    <button
                      type="button"
                      className="btn-ghost"
                      onClick={() => startEdit(t)}
                    >
                      Edit
                    </button>
                    <button
                      type="button"
                      className="btn-ghost btn-danger"
                      onClick={() => handleDelete(t)}
                    >
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </>
  );
}
