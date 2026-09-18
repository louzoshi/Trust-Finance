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
  MonthlyFlowChart,
  type CategoryPoint,
  type MonthPoint,
} from "./charts";

const MONTHS_SHOWN = 6;
const TOP_CATEGORIES = 6;

interface DashboardStats {
  monthIncome: number;
  monthExpense: number;
  monthBalance: number;
  /** Last month's balance, or null when there was nothing to compare against. */
  previousBalance: number | null;
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

  // Income and expense are tracked apart all the way through: summing them into
  // one number is what made the old dashboard report volume instead of a balance.
  const flowByMonth = new Map<string, { income: number; expense: number }>(
    keys.map((k) => [k, { income: 0, expense: 0 }]),
  );

  for (const t of transactions) {
    const flow = flowByMonth.get(monthKey(t.date));
    if (!flow) continue;
    if (t.type === "Income") flow.income += t.amount;
    else flow.expense += t.amount;
  }

  const current = flowByMonth.get(currentKey) ?? { income: 0, expense: 0 };
  const previous = flowByMonth.get(previousKey);
  const previousHadActivity =
    previous !== undefined && (previous.income > 0 || previous.expense > 0);

  const currentMonth = transactions.filter(
    (t) => monthKey(t.date) === currentKey,
  );

  // Only expenses break down by category. A ranking that mixed a salary in with
  // the grocery bill would put income at the top and say nothing about spending.
  const categoryNames = new Map(categories.map((c) => [c.id, c.name]));
  const spendByCategory = new Map<string, number>();
  for (const t of currentMonth) {
    if (t.type !== "Expense") continue;
    const name = categoryNames.get(t.categoryId) ?? "Uncategorized";
    spendByCategory.set(name, (spendByCategory.get(name) ?? 0) + t.amount);
  }

  const ranked = [...spendByCategory.entries()].sort((a, b) => b[1] - a[1]);
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
    monthIncome: current.income,
    monthExpense: current.expense,
    monthBalance: current.income - current.expense,
    previousBalance: previousHadActivity
      ? previous.income - previous.expense
      : null,
    topCategory: ranked[0]?.[0] ?? "—",
    byMonth: keys.map((key) => {
      const flow = flowByMonth.get(key) ?? { income: 0, expense: 0 };
      return {
        month: monthLabel(key),
        income: flow.income,
        expense: flow.expense,
      };
    }),
    byCategory,
    recent,
  };
}

function StatTile({
  label,
  value,
  hint,
  tone,
}: {
  label: string;
  value: string;
  hint?: string;
  tone?: "income" | "expense";
}) {
  return (
    <div className="card stat-tile">
      <span className="stat-label">{label}</span>
      <span
        className={
          tone === "income"
            ? "stat-value text-income"
            : tone === "expense"
              ? "stat-value text-expense"
              : "stat-value"
        }
      >
        {value}
      </span>
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
          Add your first transaction to see your balance, income against
          expenses and where the money goes.
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
          label="Income this month"
          value={formatCurrency(stats.monthIncome)}
          tone="income"
        />
        <StatTile
          label="Expenses this month"
          value={formatCurrency(stats.monthExpense)}
          tone="expense"
        />
        <StatTile
          label="Balance this month"
          // The sign is spelled out rather than left to colour alone.
          value={`${stats.monthBalance < 0 ? "−" : "+"}${formatCurrency(Math.abs(stats.monthBalance))}`}
          tone={stats.monthBalance < 0 ? "expense" : "income"}
          hint={
            stats.previousBalance === null
              ? "no activity last month"
              : `${formatCurrency(stats.previousBalance)} last month`
          }
        />
        <StatTile label="Top spending category" value={stats.topCategory} />
      </div>

      <div className="chart-grid">
        <section className="card">
          <h2 className="card-title">Income vs expenses</h2>
          <p className="card-subtitle">Last {MONTHS_SHOWN} months</p>
          <MonthlyFlowChart data={stats.byMonth} />
        </section>

        <section className="card">
          <h2 className="card-title">Spending by category</h2>
          <p className="card-subtitle">Current month, expenses only</p>
          {stats.byCategory.length === 0 ? (
            <p className="page-status">No spending this month.</p>
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
                <td
                  className={
                    t.type === "Income" ? "num text-income" : "num text-expense"
                  }
                >
                  {t.type === "Income" ? "+" : "−"}
                  {formatCurrency(t.amount)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </>
  );
}
