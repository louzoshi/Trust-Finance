import {
  Bar,
  BarChart,
  CartesianGrid,
  LabelList,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { formatCompactCurrency, formatCurrency } from "../../lib/format";

export interface MonthPoint {
  month: string; // short label, e.g. "Feb"
  total: number;
}

export interface CategoryPoint {
  category: string;
  total: number;
}

const AXIS_TICK = { fill: "var(--text-muted)", fontSize: 12 } as const;

interface TooltipEntry {
  value?: number | string;
}

interface ChartTooltipProps {
  active?: boolean;
  payload?: readonly TooltipEntry[];
  label?: string | number;
}

function ChartTooltip({ active, payload, label }: ChartTooltipProps) {
  if (!active || !payload?.length) return null;
  const value = Number(payload[0].value ?? 0);
  return (
    <div className="chart-tooltip">
      <span className="chart-tooltip-label">{label}</span>
      <span className="chart-tooltip-value">{formatCurrency(value)}</span>
    </div>
  );
}

/** Monthly transaction volume — single-series column chart. */
export function MonthlyVolumeChart({ data }: { data: MonthPoint[] }) {
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
        <Bar
          dataKey="total"
          fill="var(--series-1)"
          barSize={20}
          radius={[4, 4, 0, 0]}
        />
      </BarChart>
    </ResponsiveContainer>
  );
}

/** Spend by category — single-series horizontal bars, value at the tip. */
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
          fill="var(--series-1)"
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
