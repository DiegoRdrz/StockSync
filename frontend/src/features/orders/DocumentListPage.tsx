import { Anchor, Button, Paper, Table, Text } from '@mantine/core';
import { IconPlus } from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { EmptyState, PageHeader, QueryState, StatusBadge, TablePager, Money } from '@/components';
import { useBranches } from '@/lib/branches';
import { formatDateTime, formatNumber, shortId, dayStartUtc, dayEndUtc } from '@/lib/format';
import { CreateDocumentModal } from './CreateDocumentModal';
import { DocumentFilters, emptyFilters, type DocumentFilterValues } from './DocumentFilters';
import type { DocumentKind } from './documents';
import { listKey } from './useDocument';

const PAGE_SIZE = 20;

interface DocumentListPageProps {
  kind: DocumentKind;
  title: string;
}

/** Filtered, paginated list shared by sales and purchases. Rows open the detail. */
export function DocumentListPage({ kind, title }: DocumentListPageProps) {
  const navigate = useNavigate();
  const { nameOf } = useBranches();
  const [filters, setFilters] = useState<DocumentFilterValues>(emptyFilters);
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState(false);

  const query = useQuery({
    queryKey: [...listKey(kind), filters, page],
    queryFn: () =>
      kind.api.list({
        status: filters.status ?? undefined,
        branchId: filters.branchId || undefined,
        from: dayStartUtc(filters.range[0]),
        to: dayEndUtc(filters.range[1]),
        page,
        pageSize: PAGE_SIZE,
      }),
    placeholderData: (previous) => previous,
  });
  const items = query.data?.items ?? [];
  const hasFilters = !!(filters.status || filters.branchId || filters.range[0]);

  return (
    <>
      <PageHeader
        title={title}
        actions={
          <Button leftSection={<IconPlus size={16} />} onClick={() => setCreating(true)}>
            {`Nueva ${kind.singular}`}
          </Button>
        }
      />
      <DocumentFilters
        statuses={kind.statuses}
        value={filters}
        onChange={(next) => {
          setFilters(next);
          setPage(1);
        }}
      />
      <Paper withBorder>
        <QueryState
          isLoading={query.isLoading}
          error={query.error}
          onRetry={() => void query.refetch()}
          isEmpty={items.length === 0}
          empty={
            <EmptyState
              title={hasFilters ? 'Ningún resultado con estos filtros' : `Aún no hay ${kind.plural}`}
              action={
                hasFilters ? (
                  <Button variant="light" onClick={() => setFilters(emptyFilters)}>Limpiar filtros</Button>
                ) : (
                  <Button leftSection={<IconPlus size={16} />} onClick={() => setCreating(true)}>{`Nueva ${kind.singular}`}</Button>
                )
              }
            />
          }
        >
          <Table.ScrollContainer minWidth={kind.hasSupplier ? 760 : 640}>
            <Table highlightOnHover verticalSpacing={8} horizontalSpacing="sm" stickyHeader aria-label={`Lista de ${kind.plural}`}>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>N.º</Table.Th>
                  <Table.Th>Creada</Table.Th>
                  <Table.Th>Sucursal</Table.Th>
                  {kind.hasSupplier && <Table.Th>Proveedor</Table.Th>}
                  <Table.Th ta="right">Líneas</Table.Th>
                  <Table.Th ta="right">Total</Table.Th>
                  <Table.Th>Estado</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {items.map((doc) => (
                  <Table.Tr key={doc.id} style={{ cursor: 'pointer' }} onClick={() => navigate(`${kind.path}/${doc.id}`)}>
                    <Table.Td>
                      <Anchor
                        component={Link}
                        to={`${kind.path}/${doc.id}`}
                        size="sm"
                        fw={600}
                        ff="monospace"
                        onClick={(e) => e.stopPropagation()}
                        aria-label={`Abrir ${kind.singular} ${shortId(doc.id)}`}
                      >
                        {shortId(doc.id)}
                      </Anchor>
                    </Table.Td>
                    <Table.Td className="tabular">{formatDateTime(doc.createdAt)}</Table.Td>
                    <Table.Td>{nameOf(doc.branchId)}</Table.Td>
                    {kind.hasSupplier && (
                      <Table.Td maw={200}><Text size="sm" truncate title={doc.supplierName}>{doc.supplierName}</Text></Table.Td>
                    )}
                    <Table.Td ta="right" className="tabular">{formatNumber(doc.lines.length)}</Table.Td>
                    <Table.Td ta="right"><Money value={doc.total} fw={600} /></Table.Td>
                    <Table.Td><StatusBadge status={doc.status} /></Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          <TablePager page={page} totalPages={query.data?.totalPages ?? 1} total={query.data?.total} onChange={setPage} noun={kind.plural} />
        </QueryState>
      </Paper>
      <CreateDocumentModal kind={kind} opened={creating} onClose={() => setCreating(false)} />
    </>
  );
}
