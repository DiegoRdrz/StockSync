import { AreaChart, BarChart } from '@mantine/charts';
import { Group, Paper, Skeleton, Text, Title } from '@mantine/core';
import type { ReactNode } from 'react';
import type { DailySales } from '@/api/types';
import { dayjs, formatDay, formatMoney, formatMoneyCompact, formatNumber } from '@/lib/format';

/** Discreet placeholder for Owner-only data when the user is an employee. */
export function LockedNote({ label = 'Disponible para propietarios' }: { label?: string }) {
  return <Text size="sm" c="dimmed" py="xs">{label}</Text>;
}

export function EventualNote() {
  return <Text size="xs" c="dimmed">Los reportes se actualizan unos segundos después de cada venta o movimiento.</Text>;
}

export function Panel({ title, action, children, p = 'md' }: { title: string; action?: ReactNode; children: ReactNode; p?: string }) {
  return (
    <Paper p={p} component="section" aria-label={title}>
      {/* Flush panels (p="0") hold tables; the header keeps its own padding. */}
      <Group justify="space-between" mb="xs" wrap="nowrap" gap="xs" px={p === '0' ? 'md' : 0} pt={p === '0' ? 'md' : 0}>
        <Title order={2} fz="md">{title}</Title>
        {action}
      </Group>
      {children}
    </Paper>
  );
}

/** Reporting groups by UTC day and omits empty days; rebuild the full sequence. */
function fillDays(data: DailySales[], range?: { from?: string; to?: string }): DailySales[] {
  if (!range?.from || !range.to) return data;
  const byDay = new Map(data.map((d) => [dayjs.utc(d.day).format('YYYY-MM-DD'), d]));
  const result: DailySales[] = [];
  const end = dayjs.utc(range.to).startOf('day');
  for (let day = dayjs.utc(range.from).startOf('day'); !day.isAfter(end) && result.length <= 366; day = day.add(1, 'day')) {
    const key = day.format('YYYY-MM-DD');
    result.push(byDay.get(key) ?? { day: day.toISOString(), amount: 0, count: 0, units: 0 });
  }
  return result;
}

/** One series (sales amount per day). Single series: no legend, the title names it. */
export function DailySalesChart({
  data,
  range,
  height = 240,
}: {
  data: DailySales[];
  /** Query bounds; days without sales inside them are drawn as zero instead of being skipped. */
  range?: { from?: string; to?: string };
  height?: number;
}) {
  const rows = fillDays(data, range).map((d) => ({ day: formatDay(d.day), amount: d.amount }));
  return (
    <AreaChart
      h={height}
      data={rows}
      dataKey="day"
      series={[{ name: 'amount', label: 'Ventas', color: 'pine.7' }]}
      curveType="monotone"
      withDots={rows.length < 12}
      fillOpacity={0.18}
      strokeWidth={2}
      gridAxis="y"
      tickLine="none"
      valueFormatter={formatMoney}
      yAxisProps={{ tickFormatter: formatMoneyCompact, width: 64 }}
      xAxisProps={{ minTickGap: 24 }}
      areaChartProps={{ accessibilityLayer: true }}
    />
  );
}

export interface BarRow { label: string; value: number }

/** Horizontal ranking bars (single series). */
export function RankingBars({ rows, format = formatNumber, name }: { rows: BarRow[]; format?: (n: number) => string; name: string }) {
  return (
    <BarChart
      h={Math.max(120, rows.length * 34 + 24)}
      data={rows}
      dataKey="label"
      orientation="vertical"
      series={[{ name: 'value', label: name, color: 'pine.7' }]}
      gridAxis="x"
      tickLine="none"
      valueFormatter={format}
      yAxisProps={{ width: 120 }}
      xAxisProps={{ tickFormatter: format === formatNumber ? undefined : formatMoneyCompact }}
      barProps={{ radius: 4 }}
    />
  );
}

export function ChartSkeleton({ h = 240 }: { h?: number }) {
  return <Skeleton h={h} aria-busy="true" aria-label="Cargando gráfica" />;
}
