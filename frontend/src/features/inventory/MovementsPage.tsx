import { Paper, Table, Text } from '@mantine/core';

import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { movementsApi } from '@/api/endpoints';
import { EmptyState, PageHeader, QueryState, StatusBadge, TablePager } from '@/components';
import { useBranches } from '@/lib/branches';
import { formatDateTime, formatNumber } from '@/lib/format';
import { MovementAmount, movementDelta } from './MovementAmount';

export function MovementsPage() {
  const [page, setPage] = useState(1);
  const { nameOf } = useBranches();
  const query = useQuery({
    queryKey: ['movements', 'page', page],
    queryFn: () => movementsApi.page(page),
    placeholderData: (prev) => prev,
  });
  const data = query.data;

  return (
    <>
      <PageHeader
        title="Movimientos"
      />
      <Paper withBorder>
        <QueryState
          isLoading={query.isLoading}
          error={query.error}
          onRetry={() => query.refetch()}
          isEmpty={!data || data.items.length === 0}
          empty={
            <EmptyState
              title="Aún no hay movimientos"
            />
          }
        >
          <Table.ScrollContainer minWidth={760}>
            <Table highlightOnHover verticalSpacing={6} stickyHeader>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Fecha</Table.Th>
                  <Table.Th>Producto</Table.Th>
                  <Table.Th>Sucursal</Table.Th>
                  <Table.Th>Tipo</Table.Th>
                  <Table.Th ta="right">Cantidad</Table.Th>
                  <Table.Th ta="right">Saldo</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {data?.items.map((m) => (
                  <Table.Tr key={m.id}>
                    <Table.Td>
                      <Text size="sm" className="tabular" style={{ whiteSpace: 'nowrap' }}>
                        {formatDateTime(m.date)}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm" fw={500} lineClamp={1}>{m.productName}</Text>
                      <Text size="xs" c="dimmed">{m.productSku}</Text>
                    </Table.Td>
                    <Table.Td><Text size="sm">{nameOf(m.branchId)}</Text></Table.Td>
                    <Table.Td><StatusBadge status={m.type} /></Table.Td>
                    <Table.Td ta="right"><MovementAmount delta={movementDelta(m)} /></Table.Td>
                    <Table.Td ta="right">
                      <Text size="sm" className="tabular" c="dimmed">
                        {formatNumber(m.previousQuantity)} → <Text span fw={600} c="inherit" inherit>{formatNumber(m.newQuantity)}</Text>
                      </Text>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          {data && <TablePager page={page} totalPages={data.totalPages} total={data.total} onChange={setPage} noun="movimientos" />}
        </QueryState>
      </Paper>
    </>
  );
}
