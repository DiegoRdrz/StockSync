import { ActionIcon, Anchor, Box, Collapse, Stack, Table, Text, Tooltip } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { IconChevronDown, IconChevronRight } from '@tabler/icons-react';
import { Fragment, useState } from 'react';
import { Link } from 'react-router-dom';
import { reportsApi } from '@/api/endpoints';
import type { ReportQuery } from '@/api/types';
import { useAuth } from '@/app/auth';
import { EmptyState, ErrorState, Metric, Metrics, QueryState, TablePager, TableSkeleton } from '@/components';
import { useBranches } from '@/lib/branches';
import { formatDateTime, formatMoney, formatNumber } from '@/lib/format';
import { ChartSkeleton, DailySalesChart, LockedNote, Panel } from './widgets';

export function SalesTab({ query }: { query: ReportQuery }) {
  const { isOwner } = useAuth();
  const summary = useQuery({ queryKey: ['reports', 'sales-summary', query], queryFn: () => reportsApi.salesSummary(query), enabled: isOwner });
  const daily = useQuery({ queryKey: ['reports', 'sales-daily', query], queryFn: () => reportsApi.salesDaily(query) });
  const top = useQuery({ queryKey: ['reports', 'top-products', query, 10], queryFn: () => reportsApi.topProducts({ ...query, limit: 10 }) });

  return (
    <Stack gap="md">
      {isOwner ? (
        <>
          {summary.error ? (
            <ErrorState error={summary.error} onRetry={summary.refetch} />
          ) : (
            <Metrics>
              <Metric label="Ventas" value={formatMoney(summary.data?.totalAmount)} loading={summary.isLoading} />
              <Metric label="Número de ventas" value={formatNumber(summary.data?.salesCount)} loading={summary.isLoading} />
              <Metric label="Ticket promedio" value={formatMoney(summary.data?.averageTicket)} loading={summary.isLoading} />
              <Metric label="Unidades vendidas" value={formatNumber(summary.data?.units)} loading={summary.isLoading} />
            </Metrics>
          )}
        </>
      ) : (
        <LockedNote label="Los totales de ventas están disponibles para propietarios" />
      )}

      <Panel title="Ventas por día">
        <QueryState
          isLoading={daily.isLoading}
          error={daily.error}
          onRetry={daily.refetch}
          isEmpty={(daily.data?.length ?? 0) === 0}
          skeleton={<ChartSkeleton />}
          empty={<EmptyState title="Sin ventas en este período" />}
        >
          <DailySalesChart data={daily.data ?? []} range={query} />
        </QueryState>
      </Panel>

      <Panel title="Productos más vendidos" p="0">
        <QueryState
          isLoading={top.isLoading}
          error={top.error}
          onRetry={top.refetch}
          isEmpty={(top.data?.length ?? 0) === 0}
          empty={<EmptyState title="Sin ventas para clasificar" />}
        >
          <Table.ScrollContainer minWidth={420}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Producto</Table.Th>
                  <Table.Th ta="right">Unidades</Table.Th>
                  <Table.Th ta="right">Ingresos</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {top.data?.map((p) => (
                  <Table.Tr key={p.productId}>
                    <Table.Td>
                      <Text size="sm" fw={500}>{p.productName}</Text>
                      <Text size="xs" c="dimmed">{p.productSku}</Text>
                    </Table.Td>
                    <Table.Td ta="right" className="tabular">{formatNumber(p.quantity)}</Table.Td>
                    <Table.Td ta="right" className="tabular">{formatMoney(p.revenue)}</Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </QueryState>
      </Panel>

      {isOwner ? <SalesDetail query={query} /> : <LockedNote label="El detalle de cada venta está disponible para propietarios" />}
    </Stack>
  );
}

function SalesDetail({ query }: { query: ReportQuery }) {
  const [page, setPage] = useState(1);
  const [open, setOpen] = useState<string | null>(null);
  const { nameOf } = useBranches();
  const key = JSON.stringify(query);
  const [lastKey, setLastKey] = useState(key);
  if (key !== lastKey) {
    setLastKey(key);
    setPage(1);
    setOpen(null);
  }
  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['reports', 'sales-detail', query, page],
    queryFn: () => reportsApi.salesDetail({ ...query, page, pageSize: 10 }),
    placeholderData: (prev) => prev,
  });

  return (
    <Panel title="Detalle de ventas" p="0">
      <QueryState
        isLoading={isLoading}
        error={error}
        onRetry={refetch}
        isEmpty={(data?.items.length ?? 0) === 0}
        skeleton={<TableSkeleton />}
        empty={<EmptyState title="Sin ventas en este período" />}
      >
        <Table.ScrollContainer minWidth={560}>
          <Table>
            <Table.Thead>
              <Table.Tr>
                <Table.Th w={40} />
                <Table.Th>Confirmada</Table.Th>
                <Table.Th>Sucursal</Table.Th>
                <Table.Th>Orden</Table.Th>
                <Table.Th ta="right">Total</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {data?.items.map((sale) => {
                const expanded = open === sale.orderId;
                return (
                  <Fragment key={sale.orderId}>
                    <Table.Tr>
                      <Table.Td>
                        <Tooltip label={expanded ? 'Ocultar productos' : 'Ver productos'}>
                          <ActionIcon
                            variant="subtle"
                            color="gray"
                            aria-label={expanded ? 'Ocultar productos de la venta' : 'Ver productos de la venta'}
                            aria-expanded={expanded}
                            onClick={() => setOpen(expanded ? null : sale.orderId)}
                          >
                            {expanded ? <IconChevronDown size={16} /> : <IconChevronRight size={16} />}
                          </ActionIcon>
                        </Tooltip>
                      </Table.Td>
                      <Table.Td>{formatDateTime(sale.confirmedAt)}</Table.Td>
                      <Table.Td>{nameOf(sale.branchId)}</Table.Td>
                      <Table.Td>
                        <Anchor component={Link} to={`/orders/${sale.orderId}`} size="sm" className="tabular">
                          {sale.orderId.slice(0, 8)}
                        </Anchor>
                      </Table.Td>
                      <Table.Td ta="right" className="tabular">{formatMoney(sale.total)}</Table.Td>
                    </Table.Tr>
                    {expanded && (
                      <Table.Tr>
                        <Table.Td />
                        <Table.Td colSpan={4} p={0}>
                          <Collapse expanded>
                            <Box p="xs" bg="var(--mantine-color-gray-0)">
                              <Table withRowBorders={false} verticalSpacing={4}>
                                <Table.Tbody>
                                  {sale.lines.map((l) => (
                                    <Table.Tr key={l.lineNumber}>
                                      <Table.Td><Text size="sm">{l.productName}</Text></Table.Td>
                                      <Table.Td ta="right" className="tabular">{formatNumber(l.quantity)} × {formatMoney(l.unitPrice)}</Table.Td>
                                      <Table.Td ta="right" className="tabular">{formatMoney(l.lineTotal)}</Table.Td>
                                    </Table.Tr>
                                  ))}
                                </Table.Tbody>
                              </Table>
                            </Box>
                          </Collapse>
                        </Table.Td>
                      </Table.Tr>
                    )}
                  </Fragment>
                );
              })}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
        <TablePager page={page} totalPages={data?.totalPages ?? 0} total={data?.total} onChange={setPage} noun="ventas" />
      </QueryState>
    </Panel>
  );
}

