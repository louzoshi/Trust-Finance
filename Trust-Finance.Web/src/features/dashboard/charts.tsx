import {
  Bar,
  BarChart,
  CartesianGrid,
  LabelList,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { formatCompactCurrency, formatCurrency } from "../../lib/format";

export interface MonthPoint {
  month: string; // short label, e.g. "Feb"
  income: number;
  expense: number;
}

export interface CategoryPoint {
  category: string;
  total: number;
}

const AXIS_TICK = { fill: "var(--text-muted)", fontSize: 12 } as const;

interface TooltipEntry {
  value?: number | string;
  name?: string;
  color?: string;
}

interface ChartTooltipProps {
  active?: boolean;
  payload?: readonly TooltipEntry[];
  label?: string | number;
}

function ChartTooltip({ active, payload, label }: ChartTooltipProps) {
  if (!active || !payload?.length) return null;
  return (
    <div className="chart-tooltip">
      <span className="chart-tooltip-label">{label}</span>
      {payload.map((entry, i) => (
        <span
          key={i}
          className="chart-tooltip-value"
          style={entry.color ? { color: entry.color } : undefined}
        >
          {payload.length > 1 && entry.name ? `${entry.name}: ` : ""}
          {formatCurrency(Number(entry.value ?? 0))}
        </span>
      ))}
    </div>
  );
}

/**
 * Income against expense, month by month — the pair that answers whether a month
 * closed up or down. Blue against red rather than green against red: the palette
 * has to survive red-green colour blindness, and the legend carries the names.
 */
export function MonthlyFlowChart({ data }: { data: MonthPoint[] }) {
  return (
    <ResponsiveContainer width="100%" height={240}>
      <BarChart data={data} margin={{ top: 8, right: 8, bottom: 0, left: 8 }}>
        <CartesianGrid
          vertical={false}
          stroke="var(--grid)"
          strokeWidth={1}
        />
        <XAxis
          dataKey="month"
          tick={AXIS_TICK}
          axisLine={{ stroke: "var(--axis)" }}
          tickLine={false}
        />
        <YAxis
          tick={AXIS_TICK}
          tickFormatter={(v: number) => formatCompactCurrency(v)}
          axisLine={false}
          tickLine={false}
          width={56}
        />
        <Tooltip
          content={<ChartTooltip />}
          cursor={{ fill: "var(--hover-wash)" }}
        />
        <Legend
          wrapperStyle={{ fontSize: 12, color: "var(--text-secondary)" }}
        />
        <Bar
          dataKey="income"
          name="Income"
          fill="var(--color-income)"
          barSize={14}
          radius={[4, 4, 0, 0]}
        />
        <Bar
          dataKey="expense"
          name="Expense"
          fill="var(--color-expense)"
          barSize={14}
          radius={[4, 4, 0, 0]}
        />
      </BarChart>
    </ResponsiveContainer>
  );
}

/** Spending by category — expenses only, value at the tip. */
export function CategoryBreakdownChart({ data }: { data: CategoryPoint[] }) {
  const height = Math.max(160, data.length * 40 + 24);
  return (
    <ResponsiveContainer width="100%" height={height}>
      <BarChart
        data={data}
        layout="vertical"
        margin={{ top: 4, right: 56, bottom: 0, left: 8 }}
      >
        <XAxis type="number" hide />
        <YAxis
          type="category"
          dataKey="category"
          tick={AXIS_TICK}
          axisLine={{ stroke: "var(--axis)" }}
          tickLine={false}
          width={110}
        />
        <Tooltip
          content={<ChartTooltip />}
          cursor={{ fill: "var(--hover-wash)" }}
        />
        <Bar
          dataKey="total"
          fill="var(--color-expense)"
          barSize={16}
          radius={[0, 4, 4, 0]}
        >
          <LabelList
            dataKey="total"
            position="right"
            formatter={(value) => formatCompactCurrency(Number(value))}
            fill="var(--text-secondary)"
            fontSize={12}
          />
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}
