import { BarChart } from '@mantine/charts';
import { Stack, Table, Text } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { reportsApi } from '@/api/endpoints';
import type { ReportQuery } from '@/api/types';
import { EmptyState, QueryState, StatusBadge, statusLabel, StockLevel, TablePager, TableSkeleton } from '@/components';
import { useBranches } from '@/lib/branches';
import { formatDay, formatNumber } from '@/lib/format';
import { useProductNames } from './useProductNames';
import { ChartSkeleton, Panel, RankingBars } from './widgets';

const signed = (n: number) => (n > 0 ? `+${formatNumber(n)}` : formatNumber(n));

export function InventoryTab({ query }: { query: ReportQuery }) {
  const summary = useQuery({ queryKey: ['reports', 'movements-summary', query], queryFn: () => reportsApi.movementsSummary(query) });
  const { nameOf, skuOf } = useProductNames();
  const data = summary.data;
  const isEmpty = !!data && data.byType.length === 0 && data.byDay.length === 0 && data.byProduct.length === 0;

  const netByDay = (data?.byDay ?? []).map((d) => ({
    day: formatDay(d.day),
    entradas: Math.max(d.netChange, 0),
    salidas: Math.min(d.netChange, 0),
  }));

  return (
    <Stack gap="md">
      <QueryState
        isLoading={summary.isLoading}
        error={summary.error}
        onRetry={summary.refetch}
        isEmpty={isEmpty}
        skeleton={<ChartSkeleton h={320} />}
        empty={<EmptyState title="Sin movimientos en este período" />}
      >
        <Panel title="Unidades movidas por tipo" p="md">
          <RankingBars
            name="Unidades"
            rows={(data?.byType ?? []).map((t) => ({ label: statusLabel(t.type), value: t.quantity }))}
          />
          <Table.ScrollContainer minWidth={380} mt="sm">
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Tipo</Table.Th>
                  <Table.Th ta="right">Movimientos</Table.Th>
                  <Table.Th ta="right">Unidades</Table.Th>
                  <Table.Th ta="right">Cambio neto</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {data?.byType.map((t) => (
                  <Table.Tr key={t.type}>
                    <Table.Td><StatusBadge status={t.type} size="xs" /></Table.Td>
                    <Table.Td ta="right" className="tabular">{formatNumber(t.count)}</Table.Td>
                    <Table.Td ta="right" className="tabular">{formatNumber(t.quantity)}</Table.Td>
                    <Table.Td ta="right" className="tabular">{signed(t.netChange)}</Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Panel>

        <Panel title="Cambio neto de unidades por día">
          <BarChart
            h={240}
            data={netByDay}
            dataKey="day"
            type="stacked"
            series={[
              { name: 'entradas', label: 'Aumento de stock', color: 'pine.7' },
              { name: 'salidas', label: 'Disminución de stock', color: 'gray.5' },
            ]}
            withLegend
            legendProps={{ verticalAlign: 'bottom', height: 28 }}
            gridAxis="y"
            tickLine="none"
            valueFormatter={signed}
            xAxisProps={{ minTickGap: 24 }}
          />
        </Panel>

        <Panel title="Movimientos por producto" p="0">
          <Table.ScrollContainer minWidth={460}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Producto</Table.Th>
                  <Table.Th ta="right">Movimientos</Table.Th>
                  <Table.Th ta="right">Unidades</Table.Th>
                  <Table.Th ta="right">Cambio neto</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {data?.byProduct.map((p) => (
                  <Table.Tr key={p.productId}>
                    <Table.Td>
                      <Text size="sm" fw={500}>{nameOf(p.productId)}</Text>
                      {skuOf(p.productId) && <Text size="xs" c="dimmed">{skuOf(p.productId)}</Text>}
                    </Table.Td>
                    <Table.Td ta="right" className="tabular">{formatNumber(p.count)}</Table.Td>
                    <Table.Td ta="right" className="tabular">{formatNumber(p.quantity)}</Table.Td>
                    <Table.Td ta="right" className="tabular">{signed(p.netChange)}</Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Panel>
      </QueryState>

      <ReportLowStock branchId={query.branchId} />
    </Stack>
  );
}

function ReportLowStock({ branchId }: { branchId?: string }) {
  const [page, setPage] = useState(1);
  const [lastBranch, setLastBranch] = useState(branchId);
  if (branchId !== lastBranch) {
    setLastBranch(branchId);
    setPage(1);
  }
  const { nameOf } = useBranches();
  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['reports', 'low-stock', branchId, page],
    queryFn: () => reportsApi.lowStock({ branchId, page, pageSize: 10 }),
    placeholderData: (prev) => prev,
  });
  return (
    <Panel title="Productos bajo el mínimo" p="0">
      <QueryState
        isLoading={isLoading}
        error={error}
        onRetry={refetch}
        isEmpty={(data?.items.length ?? 0) === 0}
        skeleton={<TableSkeleton />}
        empty={<EmptyState title="Nada por reponer" />}
      >
        <Table.ScrollContainer minWidth={560}>
          <Table>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Producto</Table.Th>
                <Table.Th>Sucursal</Table.Th>
                <Table.Th>Nivel</Table.Th>
                <Table.Th ta="right">Faltan</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {data?.items.map((i) => (
                <Table.Tr key={i.stockId}>
                  <Table.Td>
                    <Text size="sm" fw={500}>{i.productName}</Text>
                    <Text size="xs" c="dimmed">{i.productSku}</Text>
                  </Table.Td>
                  <Table.Td>{nameOf(i.branchId)}</Table.Td>
                  <Table.Td><StockLevel quantity={i.quantity} minimum={i.minimumStock} compact /></Table.Td>
                  <Table.Td ta="right" className="tabular">{formatNumber(i.shortage)}</Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
        <TablePager page={page} totalPages={data?.totalPages ?? 0} total={data?.total} onChange={setPage} noun="productos" />
      </QueryState>
    </Panel>
  );
}
