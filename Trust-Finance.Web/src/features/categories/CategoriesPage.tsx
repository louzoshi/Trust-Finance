import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import {
  createCategory,
  deleteCategory,
  listCategories,
  updateCategory,
} from "../../api/categories";
import { ErrorList } from "../../components/ErrorList";
import type { Category, CategoryPayload } from "../../types/api";

function slugify(value: string): string {
  return value
    .toLowerCase()
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/(^-|-$)/g, "");
}

export function CategoriesPage() {
  const queryClient = useQueryClient();
  const [name, setName] = useState("");
  const [editingId, setEditingId] = useState<number | null>(null);

  const categoriesQuery = useQuery({
    queryKey: ["categories"],
    queryFn: listCategories,
  });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ["categories"] });

  const saveMutation = useMutation({
    mutationFn: (payload: CategoryPayload) =>
      editingId === null
        ? createCategory(payload)
        : updateCategory(editingId, payload),
    onSuccess: () => {
      invalidate();
      setName("");
      setEditingId(null);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: deleteCategory,
    onSuccess: () => {
      invalidate();
      // transactions cascade-delete with their category
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
    },
  });

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    saveMutation.mutate({ name: name.trim(), slug: slugify(name) });
  }

  function startEdit(category: Category) {
    setEditingId(category.id);
    setName(category.name);
  }

  function cancelEdit() {
    setEditingId(null);
    setName("");
  }

  function handleDelete(category: Category) {
    if (
      window.confirm(
        `Delete "${category.name}"? All of its transactions will be deleted too.`,
      )
    ) {
      deleteMutation.mutate(category.id);
    }
  }

  const categories = categoriesQuery.data ?? [];

  return (
    <>
      <h1 className="page-title">Categories</h1>

      <section className="card">
        <h2 className="card-title">
          {editingId === null ? "New category" : "Edit category"}
        </h2>
        <form className="inline-form" onSubmit={handleSubmit}>
          <label>
            Name
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              minLength={3}
              maxLength={40}
              required
            />
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
      </section>

      <section className="card">
        <ErrorList error={categoriesQuery.error ?? deleteMutation.error} />
        {categoriesQuery.isLoading ? (
          <p className="page-status">Loading categories…</p>
        ) : categories.length === 0 ? (
          <p className="page-status">No categories yet.</p>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Slug</th>
                <th className="actions" aria-label="Actions" />
              </tr>
            </thead>
            <tbody>
              {categories.map((c) => (
                <tr key={c.id}>
                  <td>{c.name}</td>
                  <td className="mono">{c.slug}</td>
                  <td className="actions">
                    <button
                      type="button"
                      className="btn-ghost"
                      onClick={() => startEdit(c)}
                    >
                      Edit
                    </button>
                    <button
                      type="button"
                      className="btn-ghost btn-danger"
                      onClick={() => handleDelete(c)}
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
