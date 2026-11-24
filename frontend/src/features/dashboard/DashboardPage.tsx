import { Anchor, Box, Grid, Group, SimpleGrid, Skeleton, Stack, Table, Text } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { movementsApi, reportsApi, stockApi } from '@/api/endpoints';
import { useAuth } from '@/app/auth';
import { EmptyState, Metric, Metrics, PageHeader, QueryState, StatusBadge, StockLevel, TableSkeleton } from '@/components';
import { dayjs, formatMoney, formatNumber, formatRelative, dayStartUtc, dayEndUtc } from '@/lib/format';
import { useActiveBranch, useBranches } from '@/lib/branches';
import { ChartSkeleton, DailySalesChart, Panel, RankingBars } from '@/features/reports/widgets';


function MoreLink({ to, children }: { to: string; children: string }) {
  return <Anchor component={Link} to={to} size="sm">{children}</Anchor>;
}

export function DashboardPage() {
  const { isOwner } = useAuth();
  const [activeBranch] = useActiveBranch();
  const { nameOf } = useBranches();
  const branchName = nameOf(activeBranch);

  // Last 30 days including today, scoped to the active branch.
  const range = {
    from: dayStartUtc(dayjs().subtract(29, 'day').toDate()),
    to: dayEndUtc(new Date()),
    branchId: activeBranch,
  };

  const summary = useQuery({
    queryKey: ['reports', 'sales-summary', range],
    queryFn: () => reportsApi.salesSummary(range),
    enabled: isOwner,
  });
  const daily = useQuery({ queryKey: ['reports', 'sales-daily', range], queryFn: () => reportsApi.salesDaily(range) });
  const top = useQuery({
    queryKey: ['reports', 'top-products', range, 5],
    queryFn: () => reportsApi.topProducts({ ...range, limit: 5 }),
  });
  const low = useQuery({
    queryKey: ['stock', 'low', activeBranch, 8],
    queryFn: () => stockApi.lowStock({ branchId: activeBranch, pageSize: 8 }),
  });
  const movements = useQuery({ queryKey: ['movements', 'page', 1], queryFn: () => movementsApi.page(1) });

  const days = daily.data ?? [];
  const sumAmount = days.reduce((s, d) => s + d.amount, 0);
  const sumCount = days.reduce((s, d) => s + d.count, 0);
  const sumUnits = days.reduce((s, d) => s + d.units, 0);

  return (
    <>
      <PageHeader
        title="Resumen"
        description={`${branchName}, últimos 30 días`}
      />

      <Stack gap="md">
        {isOwner ? (
          <Metrics>
            <Metric label="Ventas" value={formatMoney(summary.data?.totalAmount)} loading={summary.isLoading} />
            <Metric label="Número de ventas" value={formatNumber(summary.data?.salesCount)} loading={summary.isLoading} />
            <Metric label="Ticket promedio" value={formatMoney(summary.data?.averageTicket)} loading={summary.isLoading} />
            <Metric label="Unidades vendidas" value={formatNumber(summary.data?.units)} loading={summary.isLoading} />
          </Metrics>
        ) : (
          <Metrics cols={3}>
            <Metric label="Número de ventas" value={formatNumber(sumCount)} loading={daily.isLoading} />
            <Metric label="Unidades vendidas" value={formatNumber(sumUnits)} loading={daily.isLoading} />
            <Metric label="Bajo el mínimo" value={formatNumber(low.data?.total)} loading={low.isLoading} color={low.data?.total ? 'amber.8' : undefined} />
          </Metrics>
        )}

        <Grid gap="md">
          <Grid.Col span={{ base: 12, md: 8 }}>
            <Panel title="Ventas diarias" action={isOwner ? undefined : <Text size="xs" c="dimmed" className="tabular">{formatMoney(sumAmount)} en total</Text>}>
              <QueryState
                isLoading={daily.isLoading}
                error={daily.error}
                onRetry={daily.refetch}
                isEmpty={days.length === 0}
                skeleton={<ChartSkeleton />}
                empty={<EmptyState title="Sin ventas en este período" />}
              >
                <DailySalesChart data={days} range={range} />
              </QueryState>
            </Panel>
          </Grid.Col>

          <Grid.Col span={{ base: 12, md: 4 }}>
          <Panel
            title="Por reponer"
            action={<MoreLink to="/stock">Ver inventario</MoreLink>}
            p="sm"
          >
            <QueryState
              isLoading={low.isLoading}
              error={low.error}
              onRetry={low.refetch}
              isEmpty={(low.data?.items.length ?? 0) === 0}
              skeleton={<TableSkeleton rows={5} />}
              empty={<EmptyState title="Nada por reponer" />}
            >
              <Stack gap="sm">
                {low.data?.items.map((item) => (
                  <Group key={item.stockId} justify="space-between" wrap="nowrap" gap="xs">
                    <Box style={{ minWidth: 0 }}>
                      <Text size="sm" fw={500} truncate>{item.productName}</Text>
                      <Text size="xs" c="dimmed" truncate>{item.productSku}</Text>
                    </Box>
                    <StockLevel quantity={item.quantity} minimum={item.minimumStock} compact />
                  </Group>
                ))}
                {(low.data?.total ?? 0) > 8 && (
                  <Text size="xs" c="dimmed" className="tabular">y {formatNumber((low.data?.total ?? 0) - 8)} más por reponer</Text>
                )}
              </Stack>
            </QueryState>
          </Panel>
          </Grid.Col>
        </Grid>

        <SimpleGrid cols={{ base: 1, md: 2 }} spacing="md">
          <Panel title="Últimos movimientos" action={<MoreLink to="/movements">Ver todos</MoreLink>} p="sm">
            <QueryState
              isLoading={movements.isLoading}
              error={movements.error}
              onRetry={movements.refetch}
              isEmpty={(movements.data?.items.length ?? 0) === 0}
              skeleton={<TableSkeleton rows={6} />}
              empty={<EmptyState title="Sin movimientos" />}
            >
              <Table.ScrollContainer minWidth={380}>
                <Table verticalSpacing={6}>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Producto</Table.Th>
                      <Table.Th>Tipo</Table.Th>
                      <Table.Th ta="right">Cantidad</Table.Th>
                      <Table.Th ta="right">Cuándo</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {movements.data?.items.slice(0, 6).map((m) => (
                      <Table.Tr key={m.id}>
                        <Table.Td maw={200}><Text size="sm" truncate>{m.productName}</Text></Table.Td>
                        <Table.Td><StatusBadge status={m.type} size="xs" /></Table.Td>
                        <Table.Td ta="right" className="tabular">{formatNumber(m.quantity)}</Table.Td>
                        <Table.Td ta="right"><Text size="xs" c="dimmed" component="span">{formatRelative(m.date)}</Text></Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Table.ScrollContainer>
            </QueryState>
          </Panel>

          <Panel title="Más vendidos">
            <QueryState
              isLoading={top.isLoading}
              error={top.error}
              onRetry={top.refetch}
              isEmpty={(top.data?.length ?? 0) === 0}
              skeleton={<Stack gap={8}>{Array.from({ length: 5 }, (_, i) => <Skeleton key={i} h={24} />)}</Stack>}
              empty={<EmptyState title="Sin ventas en este período" />}
            >
              <RankingBars
                name="Ingresos"
                format={formatMoney}
                rows={(top.data ?? []).map((p) => ({
                  label: p.productName.length > 18 ? `${p.productName.slice(0, 17)}…` : p.productName,
                  value: p.revenue,
                }))}
              />
            </QueryState>
          </Panel>
        </SimpleGrid>
      </Stack>
    </>
  );
}
