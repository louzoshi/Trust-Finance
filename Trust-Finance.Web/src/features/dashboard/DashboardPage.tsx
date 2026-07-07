import { useQuery } from "@tanstack/react-query";
import { useMemo } from "react";
import { Link } from "react-router-dom";
import { listCategories } from "../../api/categories";
import { listTransactions } from "../../api/transactions";
import { ErrorList } from "../../components/ErrorList";
import {
  formatCurrency,
  formatDate,
  monthKey,
  monthLabel,
} from "../../lib/format";
import type { Category, Transaction } from "../../types/api";
import {
  CategoryBreakdownChart,
  MonthlyVolumeChart,
  type CategoryPoint,
  type MonthPoint,
} from "./charts";

const MONTHS_SHOWN = 6;
const TOP_CATEGORIES = 6;

interface DashboardStats {
  monthTotal: number;
  deltaPct: number | null;
  monthCount: number;
  monthAverage: number;
  topCategory: string;
  byMonth: MonthPoint[];
  byCategory: CategoryPoint[];
  recent: Transaction[];
}

function lastMonthKeys(count: number): string[] {
  const now = new Date();
  const keys: string[] = [];
  for (let i = count - 1; i >= 0; i--) {
    const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
    keys.push(`${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}`);
  }
  return keys;
}

function computeStats(
  transactions: Transaction[],
  categories: Category[],
): DashboardStats {
  const keys = lastMonthKeys(MONTHS_SHOWN);
  const currentKey = keys[keys.length - 1];
  const previousKey = keys[keys.length - 2];

  const totalsByMonth = new Map<string, number>(keys.map((k) => [k, 0]));
  const currentMonth = transactions.filter(
    (t) => monthKey(t.date) === currentKey,
  );

  for (const t of transactions) {
    const key = monthKey(t.date);
    if (totalsByMonth.has(key)) {
      totalsByMonth.set(key, (totalsByMonth.get(key) ?? 0) + t.amount);
    }
  }

  const monthTotal = totalsByMonth.get(currentKey) ?? 0;
  const previousTotal = totalsByMonth.get(previousKey) ?? 0;
  const deltaPct =
    previousTotal > 0
      ? ((monthTotal - previousTotal) / previousTotal) * 100
      : null;

  const categoryNames = new Map(categories.map((c) => [c.id, c.name]));
  const totalsByCategory = new Map<string, number>();
  for (const t of currentMonth) {
    const name = categoryNames.get(t.categoryId) ?? "Uncategorized";
    totalsByCategory.set(name, (totalsByCategory.get(name) ?? 0) + t.amount);
  }

  const ranked = [...totalsByCategory.entries()].sort((a, b) => b[1] - a[1]);
  const top = ranked.slice(0, TOP_CATEGORIES);
  const tail = ranked.slice(TOP_CATEGORIES);
  const byCategory: CategoryPoint[] = top.map(([category, total]) => ({
    category,
    total,
  }));
  if (tail.length > 0) {
    byCategory.push({
      category: "Other",
      total: tail.reduce((sum, [, total]) => sum + total, 0),
    });
  }

  const recent = [...transactions]
    .sort((a, b) => b.date.localeCompare(a.date))
    .slice(0, 5);

  return {
    monthTotal,
    deltaPct,
    monthCount: currentMonth.length,
    monthAverage: currentMonth.length ? monthTotal / currentMonth.length : 0,
    topCategory: ranked[0]?.[0] ?? "—",
    byMonth: keys.map((key) => ({
      month: monthLabel(key),
      total: totalsByMonth.get(key) ?? 0,
    })),
    byCategory,
    recent,
  };
}

function StatTile({
  label,
  value,
  hint,
}: {
  label: string;
  value: string;
  hint?: string;
}) {
  return (
    <div className="card stat-tile">
      <span className="stat-label">{label}</span>
      <span className="stat-value">{value}</span>
      {hint && <span className="stat-hint">{hint}</span>}
    </div>
  );
}

export function DashboardPage() {
  const transactionsQuery = useQuery({
    queryKey: ["transactions"],
    queryFn: listTransactions,
  });
  const categoriesQuery = useQuery({
    queryKey: ["categories"],
    queryFn: listCategories,
  });

  const stats = useMemo(
    () =>
      transactionsQuery.data && categoriesQuery.data
        ? computeStats(transactionsQuery.data, categoriesQuery.data)
        : null,
    [transactionsQuery.data, categoriesQuery.data],
  );

  const error = transactionsQuery.error ?? categoriesQuery.error;
  if (error) return <ErrorList error={error} />;
  if (!stats) return <p className="page-status">Loading dashboard…</p>;

  const categoryNames = new Map(
    (categoriesQuery.data ?? []).map((c) => [c.id, c.name]),
  );

  if (transactionsQuery.data?.length === 0) {
    return (
      <div className="empty-state card">
        <h2>No transactions yet</h2>
        <p>
          Add your first transaction to see monthly volume, category breakdown
          and stats here.
        </p>
        <Link className="btn-primary" to="/transactions">
          Add a transaction
        </Link>
      </div>
    );
  }

  return (
    <>
      <h1 className="page-title">Dashboard</h1>

      <div className="stat-row">
        <StatTile
          label="This month"
          value={formatCurrency(stats.monthTotal)}
          hint={
            stats.deltaPct === null
              ? "no data for last month"
              : `${stats.deltaPct >= 0 ? "+" : ""}${stats.deltaPct.toFixed(1)}% vs last month`
          }
        />
        <StatTile
          label="Transactions this month"
          value={String(stats.monthCount)}
        />
        <StatTile
          label="Average transaction"
          value={formatCurrency(stats.monthAverage)}
        />
        <StatTile label="Top category" value={stats.topCategory} />
      </div>

      <div className="chart-grid">
        <section className="card">
          <h2 className="card-title">Monthly volume</h2>
          <p className="card-subtitle">Last {MONTHS_SHOWN} months, all categories</p>
          <MonthlyVolumeChart data={stats.byMonth} />
        </section>

        <section className="card">
          <h2 className="card-title">By category</h2>
          <p className="card-subtitle">Current month</p>
          {stats.byCategory.length === 0 ? (
            <p className="page-status">No transactions this month.</p>
          ) : (
            <CategoryBreakdownChart data={stats.byCategory} />
          )}
        </section>
      </div>

      <section className="card">
        <div className="card-header">
          <h2 className="card-title">Recent transactions</h2>
          <Link to="/transactions" className="card-link">
            View all
          </Link>
        </div>
        <table className="data-table">
          <thead>
            <tr>
              <th>Date</th>
              <th>Description</th>
              <th>Category</th>
              <th className="num">Amount</th>
            </tr>
          </thead>
          <tbody>
            {stats.recent.map((t) => (
              <tr key={t.id}>
                <td>{formatDate(t.date)}</td>
                <td>{t.description}</td>
                <td>{categoryNames.get(t.categoryId) ?? "—"}</td>
                <td className="num">{formatCurrency(t.amount)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </>
  );
}
