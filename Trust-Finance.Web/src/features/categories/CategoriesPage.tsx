import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { createCategory, listCategories } from "../../api/categories";
import { ErrorList } from "../../components/ErrorList";

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

  const categoriesQuery = useQuery({
    queryKey: ["categories"],
    queryFn: listCategories,
  });

  const createMutation = useMutation({
    mutationFn: createCategory,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["categories"] });
      setName("");
    },
  });

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    createMutation.mutate({ name: name.trim(), slug: slugify(name) });
  }

  const categories = categoriesQuery.data ?? [];

  return (
    <>
      <h1 className="page-title">Categories</h1>

      <section className="card">
        <h2 className="card-title">New category</h2>
        <form className="inline-form" onSubmit={handleSubmit}>
          <label>
            Name
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
            />
          </label>
          <div className="form-actions">
            <button
              type="submit"
              className="btn-primary"
              disabled={createMutation.isPending}
            >
              Add
            </button>
          </div>
        </form>
        <ErrorList error={createMutation.error} />
      </section>

      <section className="card">
        <ErrorList error={categoriesQuery.error} />
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
              </tr>
            </thead>
            <tbody>
              {categories.map((c) => (
                <tr key={c.id}>
                  <td>{c.name}</td>
                  <td className="mono">{c.slug}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </>
  );
}
