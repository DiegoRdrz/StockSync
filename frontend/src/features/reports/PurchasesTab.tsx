import { BarChart } from '@mantine/charts';
import { Stack } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { reportsApi } from '@/api/endpoints';
import type { ReportQuery } from '@/api/types';
import { useAuth } from '@/app/auth';
import { EmptyState, Metric, Metrics, QueryState } from '@/components';
import { formatDay, formatMoney, formatMoneyCompact, formatNumber } from '@/lib/format';
import { ChartSkeleton, LockedNote, Panel } from './widgets';

export function PurchasesTab({ query }: { query: ReportQuery }) {
  const { isOwner } = useAuth();
  const summary = useQuery({ queryKey: ['reports', 'purchases-summary', query], queryFn: () => reportsApi.purchasesSummary(query), enabled: isOwner });
  const daily = useQuery({ queryKey: ['reports', 'purchases-daily', query], queryFn: () => reportsApi.purchasesDaily(query), enabled: isOwner });

  if (!isOwner) return <LockedNote label="Los reportes de compras están disponibles para propietarios" />;

  const rows = (daily.data ?? []).map((d) => ({ day: formatDay(d.day), amount: d.amount }));
  return (
    <Stack gap="md">
      <Metrics cols={3}>
        <Metric label="Total comprado" value={formatMoney(summary.data?.totalAmount)} loading={summary.isLoading} />
        <Metric label="Compras recibidas" value={formatNumber(summary.data?.receivedPurchases)} loading={summary.isLoading} />
        <Metric label="Unidades recibidas" value={formatNumber(summary.data?.unitsReceived)} loading={summary.isLoading} />
      </Metrics>
      <Panel title="Compras recibidas por día">
        <QueryState
          isLoading={daily.isLoading}
          error={daily.error ?? summary.error}
          onRetry={() => { daily.refetch(); summary.refetch(); }}
          isEmpty={rows.length === 0}
          skeleton={<ChartSkeleton />}
          empty={<EmptyState title="Sin compras recibidas en este período" description="Las compras cuentan cuando se reciben en la orden de compra." />}
        >
          <BarChart
            h={240}
            data={rows}
            dataKey="day"
            series={[{ name: 'amount', label: 'Compras', color: 'blue.6' }]}
            gridAxis="y"
            tickLine="none"
            valueFormatter={formatMoney}
            yAxisProps={{ tickFormatter: formatMoneyCompact, width: 64 }}
            xAxisProps={{ minTickGap: 24 }}
          />
        </QueryState>
      </Panel>
    </Stack>
  );
}
