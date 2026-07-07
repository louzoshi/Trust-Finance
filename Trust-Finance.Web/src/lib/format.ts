const currency = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
});

const compact = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  notation: "compact",
  maximumFractionDigits: 1,
});

export function formatCurrency(value: number): string {
  return currency.format(value);
}

export function formatCompactCurrency(value: number): string {
  return compact.format(value);
}

export function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
  });
}

/** yyyy-MM-dd for <input type="date"> values. */
export function toDateInputValue(iso: string): string {
  return iso.slice(0, 10);
}

export function monthKey(iso: string): string {
  return iso.slice(0, 7); // yyyy-MM
}

export function monthLabel(key: string): string {
  const [year, month] = key.split("-").map(Number);
  return new Date(year, month - 1, 1).toLocaleDateString("en-US", {
    month: "short",
  });
}
