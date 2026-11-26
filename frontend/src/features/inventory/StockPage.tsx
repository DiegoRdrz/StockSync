import { Box, Button, Group, Paper, SegmentedControl, Table, Text, TextInput } from '@mantine/core';
import { IconAdjustments, IconArrowDown, IconArrowUp, IconHistory, IconPlus, IconSearch, IconTrash } from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { stockApi } from '@/api/endpoints';
import type { ProductResponse, StockResponse } from '@/api/types';
import { EmptyState, Metric, Metrics, OwnerAction, PageHeader, QueryState, RowAction, StockLevel, TablePager } from '@/components';
import { useActiveBranch, useBranches } from '@/lib/branches';
import { formatNumber } from '@/lib/format';
import { confirmAction, notifyError, notifySuccess } from '@/lib/notify';
import { STOCK_KEYS, useProductMap } from './hooks';
import { AdjustModal, AssignModal, HistoryDrawer, MovementModal } from './StockModals';

const PAGE_SIZE = 10;
const MAX_PAGES = 10;

type Dialog =
  | { kind: 'receipt' | 'issue' | 'adjust' | 'history'; stock: StockResponse }
  | { kind: 'assign' }
  | null;

export function StockPage() {
  const [branchId] = useActiveBranch();
  const { nameOf } = useBranches();
  const qc = useQueryClient();
  const [view, setView] = useState('all');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [dialog, setDialog] = useState<Dialog>(null);
  const [lastDialog, setLastDialog] = useState<Dialog>(null);
  const [seq, setSeq] = useState(0);
  const open = (d: Dialog) => {
    setSeq((n) => n + 1);
    setDialog(d);
    if (d) setLastDialog(d);
  };

  const products = useProductMap();
  const stockQuery = useQuery({
    queryKey: ['stock', 'branch', branchId],
    queryFn: async () => {
      const first = await stockApi.byBranch(branchId, { page: 1, pageSize: 100 });
      const items = [...first.items];
      for (let p = 2; p <= Math.min(first.totalPages, MAX_PAGES); p++) {
        items.push(...(await stockApi.byBranch(branchId, { page: p, pageSize: 100 })).items);
      }
      return items;
    },
  });
  const lowQuery = useQuery({
    queryKey: ['stock', 'low', branchId],
    queryFn: () => stockApi.lowStock({ branchId, page: 1, pageSize: 1 }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => stockApi.remove(id),
    onSuccess: async () => {
      notifySuccess('Asignación eliminada');
      await Promise.all(STOCK_KEYS.map((key) => qc.invalidateQueries({ queryKey: key })));
    },
    onError: (e) => notifyError(e, 'No se pudo eliminar la asignación'),
  });

  const rows = useMemo(() => {
    const map = products.data;
    const term = search.trim().toLowerCase();
    return (stockQuery.data ?? [])
      .map((stock) => ({ stock, product: map?.get(stock.productId) }))
      .filter(({ stock, product }) => {
        if (view === 'low' && !(product && stock.quantity < product.minimumStock)) return false;
        if (!term) return true;
        return !!product && (product.name.toLowerCase().includes(term) || product.sku.toLowerCase().includes(term));
      })
      .sort((a, b) => (a.product?.name ?? '').localeCompare(b.product?.name ?? '', 'es'));
  }, [stockQuery.data, products.data, search, view]);

  const totalPages = Math.max(1, Math.ceil(rows.length / PAGE_SIZE));
  const current = Math.min(page, totalPages);
  const visible = rows.slice((current - 1) * PAGE_SIZE, current * PAGE_SIZE);
  const depleted = (stockQuery.data ?? []).filter((s) => s.quantity <= 0).length;
  const productOf = (s: StockResponse): ProductResponse | undefined => products.data?.get(s.productId);

  const askRemove = (stock: StockResponse) =>
    confirmAction({
      title: 'Eliminar asignación',
      message: `Se quitará ${productOf(stock)?.name ?? 'el producto'} de ${nameOf(branchId)}. Solo es posible si no tiene unidades ni historial de movimientos.`,
      confirmLabel: 'Eliminar asignación',
      destructive: true,
      onConfirm: () => remove.mutate(stock.id),
    });

  const rowDialog = lastDialog && lastDialog.kind !== 'assign' ? lastDialog : null;
  const isOpen = (kind: string) => dialog?.kind === kind;
  const close = () => setDialog(null);

  return (
    <>
      <PageHeader
        title={`Stock en ${nameOf(branchId)}`}
        actions={
          <OwnerAction>
            <Button leftSection={<IconPlus size={16} />} onClick={() => open({ kind: 'assign' })}>
              Asignar producto
            </Button>
          </OwnerAction>
        }
      />

      <Box mb="lg">
        <Metrics cols={3}>
          <Metric label="Productos asignados" value={formatNumber(stockQuery.data?.length)} loading={stockQuery.isLoading} />
          <Metric label="Bajo el mínimo" value={formatNumber(lowQuery.data?.total)} loading={lowQuery.isLoading} color={lowQuery.data?.total ? 'amber.8' : undefined} />
          <Metric label="Agotados" value={formatNumber(depleted)} loading={stockQuery.isLoading} color={depleted ? 'red.7' : undefined} />
        </Metrics>
      </Box>

      <Paper withBorder>
        <Group justify="space-between" p="sm" gap="sm">
          <SegmentedControl
            value={view}
            onChange={(v) => {
              setView(v);
              setPage(1);
            }}
            data={[
              { value: 'all', label: 'Todo' },
              { value: 'low', label: 'Bajo mínimo' },
            ]}
          />
          <TextInput
            aria-label="Buscar por nombre o SKU"
            placeholder="Buscar por nombre o SKU"
            leftSection={<IconSearch size={16} />}
            value={search}
            onChange={(e) => {
              setSearch(e.currentTarget.value);
              setPage(1);
            }}
            w={{ base: '100%', xs: 280 }}
          />
        </Group>

        <QueryState
          isLoading={stockQuery.isLoading || products.isLoading}
          error={stockQuery.error ?? products.error}
          onRetry={() => {
            void stockQuery.refetch();
            void products.refetch();
          }}
          isEmpty={rows.length === 0}
          empty={
            (stockQuery.data?.length ?? 0) === 0 ? (
              <EmptyState
                title="Esta sucursal aún no tiene productos"
                description="Asigna un producto para poder registrar entradas y salidas."
              />
            ) : (
              <EmptyState
                title={view === 'low' && !search ? 'Nada por debajo del mínimo' : 'Ningún producto coincide'}
                
              />
            )
          }
        >
          <Table.ScrollContainer minWidth={640}>
            <Table highlightOnHover verticalSpacing={6} stickyHeader>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Producto</Table.Th>
                  <Table.Th>Nivel de stock</Table.Th>
                  <Table.Th ta="right">Acciones</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {visible.map(({ stock, product }) => (
                  <Table.Tr key={stock.id}>
                    <Table.Td>
                      <Text size="sm" fw={500} lineClamp={1}>{product?.name ?? 'Producto desconocido'}</Text>
                      <Text size="xs" c="dimmed">{product?.sku ?? '—'}</Text>
                    </Table.Td>
                    <Table.Td>
                      <StockLevel quantity={stock.quantity} minimum={product?.minimumStock ?? 0} />
                    </Table.Td>
                    <Table.Td>
                      <Group gap={4} justify="flex-end" wrap="nowrap">
                        <RowAction ownerOnly color="pine" label="Registrar entrada" ariaLabel={`Registrar entrada de ${product?.name ?? 'producto'}`} disabled={product ? !product.isActive : false} onClick={() => open({ kind: 'receipt', stock })}>
                          <IconArrowDown size={18} />
                        </RowAction>
                        <RowAction ownerOnly color="red" label={product && !product.isActive ? 'Producto inactivo' : 'Registrar salida'} ariaLabel={`Registrar salida de ${product?.name ?? 'producto'}`} disabled={product ? !product.isActive : false} onClick={() => open({ kind: 'issue', stock })}>
                          <IconArrowUp size={18} />
                        </RowAction>
                        <RowAction ownerOnly label="Ajustar cantidad" ariaLabel={`Ajustar cantidad de ${product?.name ?? 'producto'}`} onClick={() => open({ kind: 'adjust', stock })}>
                          <IconAdjustments size={18} />
                        </RowAction>
                        <RowAction label="Ver historial" ariaLabel={`Ver historial de ${product?.name ?? 'producto'}`} onClick={() => open({ kind: 'history', stock })}>
                          <IconHistory size={18} />
                        </RowAction>
                        <RowAction ownerOnly color="red" label="Eliminar asignación" ariaLabel={`Eliminar asignación de ${product?.name ?? 'producto'}`} onClick={() => askRemove(stock)}>
                          <IconTrash size={18} />
                        </RowAction>
                      </Group>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          <TablePager page={current} totalPages={totalPages} total={rows.length} onChange={setPage} noun="productos" />
        </QueryState>
      </Paper>

      <AssignModal opened={isOpen('assign')} branchId={branchId} branchName={nameOf(branchId)} onClose={close} />
      {rowDialog && (
        <>
          <MovementModal
            key={`r-${rowDialog.stock.id}-${seq}`}
            kind="receipt"
            stock={isOpen('receipt') ? rowDialog.stock : null}
            product={productOf(rowDialog.stock)}
            onClose={close}
          />
          <MovementModal
            key={`i-${rowDialog.stock.id}-${seq}`}
            kind="issue"
            stock={isOpen('issue') ? rowDialog.stock : null}
            product={productOf(rowDialog.stock)}
            onClose={close}
          />
          <AdjustModal
            key={`a-${rowDialog.stock.id}-${seq}`}
            stock={isOpen('adjust') ? rowDialog.stock : null}
            product={productOf(rowDialog.stock)}
            onClose={close}
          />
          <HistoryDrawer
            key={`h-${rowDialog.stock.id}-${seq}`}
            stock={isOpen('history') ? rowDialog.stock : null}
            product={productOf(rowDialog.stock)}
            onClose={close}
          />
        </>
      )}
    </>
  );
}
