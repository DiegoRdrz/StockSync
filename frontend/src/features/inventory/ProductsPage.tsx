import { Badge, Button, Drawer, Group, Paper, Select, Table, Text, TextInput } from '@mantine/core';
import { useDebouncedValue } from '@mantine/hooks';
import { IconEdit, IconPlus, IconSearch, IconStack2, IconTrash } from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { categoriesApi, productsApi, stockApi } from '@/api/endpoints';
import type { ProductResponse } from '@/api/types';
import { EmptyState, Money, OwnerAction, PageHeader, QueryState, RowAction, StockLevel, TablePager } from '@/components';
import { useBranches } from '@/lib/branches';
import { formatNumber } from '@/lib/format';
import { confirmAction, notifyError, notifySuccess } from '@/lib/notify';
import { ProductFormModal } from './ProductForm';
import { useClampPage } from '@/lib/useClampPage';

const PAGE_SIZE = 15;

const margin = (p: ProductResponse) => (p.salePrice > 0 ? `${(((p.salePrice - p.purchasePrice) / p.salePrice) * 100).toFixed(1).replace('.', ',')} %` : '—');

function StockDrawer({ product, onClose }: { product: ProductResponse | null; onClose: () => void }) {
  const { nameOf } = useBranches();
  const query = useQuery({
    queryKey: ['stock', 'product', product?.id],
    queryFn: () => stockApi.byProduct(product!.id, { pageSize: 100 }),
    enabled: !!product,
  });
  return (
    <Drawer opened={!!product} onClose={onClose} position="right" size="md" title={<Text fw={650}>Existencias de {product?.name}</Text>}>
      <QueryState
        isLoading={query.isLoading}
        error={query.error}
        onRetry={() => query.refetch()}
        isEmpty={!query.data || query.data.items.length === 0}
        empty={<EmptyState title="Sin existencias" description="Asígnalo a una sucursal desde Stock." />}
      >
        <Table.ScrollContainer minWidth={360}>
          <Table verticalSpacing={6}>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Sucursal</Table.Th>
                <Table.Th>Nivel de stock</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {query.data?.items.map((s) => (
                <Table.Tr key={s.id}>
                  <Table.Td><Text size="sm">{nameOf(s.branchId)}</Text></Table.Td>
                  <Table.Td><StockLevel quantity={s.quantity} minimum={product?.minimumStock ?? 0} /></Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      </QueryState>
    </Drawer>
  );
}

export function ProductsPage() {
  const qc = useQueryClient();
  const [name, setName] = useState('');
  const [sku, setSku] = useState('');
  const [categoryId, setCategoryId] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [debouncedName] = useDebouncedValue(name.trim(), 300);
  const [debouncedSku] = useDebouncedValue(sku.trim(), 300);
  const [form, setForm] = useState<{ product: ProductResponse | null; seq: number } | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [stockOf, setStockOf] = useState<ProductResponse | null>(null);

  const categories = useQuery({ queryKey: ['categories', 'all'], queryFn: () => categoriesApi.list({ pageSize: 100 }), staleTime: 60_000 });
  const categoryName = new Map((categories.data?.items ?? []).map((c) => [c.id, c.name]));

  const query = useQuery({
    queryKey: ['products', 'list', debouncedName, debouncedSku, categoryId, page],
    queryFn: () =>
      productsApi.list({ name: debouncedName || undefined, sku: debouncedSku || undefined, categoryId: categoryId || undefined, page, pageSize: PAGE_SIZE }),
    placeholderData: (prev) => prev,
  });
  const data = query.data;
  const filtered = !!(debouncedName || debouncedSku || categoryId);

  const deactivate = useMutation({
    mutationFn: (id: string) => productsApi.remove(id),
    onSuccess: async () => {
      notifySuccess('Producto desactivado');
      await Promise.all([qc.invalidateQueries({ queryKey: ['products'] }), qc.invalidateQueries({ queryKey: ['stock'] })]);
    },
    onError: (e) => notifyError(e, 'No se pudo desactivar el producto'),
  });

  const openForm = (product: ProductResponse | null) => {
    setForm((f) => ({ product, seq: (f?.seq ?? 0) + 1 }));
    setFormOpen(true);
  };
  const askDeactivate = (p: ProductResponse) =>
    confirmAction({
      title: 'Desactivar producto',
      message: `${p.name} dejará de aparecer para nuevas ventas y compras y no recibirá más movimientos. Solo se puede desactivar si no le quedan unidades en ninguna sucursal.`,
      confirmLabel: 'Desactivar producto',
      destructive: true,
      onConfirm: () => deactivate.mutate(p.id),
    });

  useClampPage(page, data?.totalPages, setPage);

  return (
    <>
      <PageHeader
        title="Productos"
        actions={
          <OwnerAction>
            <Button leftSection={<IconPlus size={16} />} onClick={() => openForm(null)}>Nuevo producto</Button>
          </OwnerAction>
        }
      />
      <Paper withBorder>
        <Group p="sm" gap="sm" align="flex-end">
          <TextInput
            aria-label="Filtrar por nombre"
            placeholder="Nombre"
            leftSection={<IconSearch size={16} />}
            value={name}
            onChange={(e) => { setName(e.currentTarget.value); setPage(1); }}
            w={{ base: '100%', xs: 220 }}
          />
          <TextInput
            aria-label="Filtrar por SKU"
            placeholder="SKU"
            value={sku}
            onChange={(e) => { setSku(e.currentTarget.value); setPage(1); }}
            w={{ base: '100%', xs: 160 }}
          />
          <Select
            aria-label="Filtrar por categoría"
            placeholder="Todas las categorías"
            clearable
            searchable
            data={(categories.data?.items ?? []).map((c) => ({ value: c.id, label: c.name }))}
            value={categoryId}
            onChange={(v) => { setCategoryId(v); setPage(1); }}
            w={{ base: '100%', xs: 220 }}
          />
        </Group>
        <QueryState
          isLoading={query.isLoading}
          error={query.error}
          onRetry={() => query.refetch()}
          isEmpty={!data || data.items.length === 0}
          empty={
            filtered ? (
              <EmptyState title="Sin resultados" />
            ) : (
              <EmptyState title="Aún no hay productos" />
            )
          }
        >
          <Table.ScrollContainer minWidth={900}>
            <Table highlightOnHover verticalSpacing={6} stickyHeader>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Producto</Table.Th>
                  <Table.Th>Categoría</Table.Th>
                  <Table.Th ta="right">Compra</Table.Th>
                  <Table.Th ta="right">Venta</Table.Th>
                  <Table.Th ta="right">Margen</Table.Th>
                  <Table.Th ta="right">Mínimo</Table.Th>
                  <Table.Th>Estado</Table.Th>
                  <Table.Th ta="right">Acciones</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {data?.items.map((p) => (
                  <Table.Tr key={p.id} c={p.isActive ? undefined : 'dimmed'}>
                    <Table.Td>
                      <Text size="sm" fw={500} lineClamp={1} inherit>{p.name}</Text>
                      <Text size="xs" c="dimmed">{p.sku}</Text>
                    </Table.Td>
                    <Table.Td><Text size="sm" inherit>{(p.categoryId && categoryName.get(p.categoryId)) || '—'}</Text></Table.Td>
                    <Table.Td ta="right"><Money value={p.purchasePrice} size="sm" /></Table.Td>
                    <Table.Td ta="right"><Money value={p.salePrice} size="sm" /></Table.Td>
                    <Table.Td ta="right"><Text size="sm" className="tabular" inherit>{margin(p)}</Text></Table.Td>
                    <Table.Td ta="right"><Text size="sm" className="tabular" inherit>{formatNumber(p.minimumStock)}</Text></Table.Td>
                    <Table.Td>{p.isActive ? <Text size="xs" c="dimmed">Activo</Text> : <Badge color="gray" variant="light" size="sm">Inactivo</Badge>}</Table.Td>
                    <Table.Td>
                      <Group gap={4} justify="flex-end" wrap="nowrap">
                        <RowAction label="Ver existencias" ariaLabel={`Ver existencias de ${p.name}`} onClick={() => setStockOf(p)}>
                          <IconStack2 size={18} />
                        </RowAction>
                        <RowAction ownerOnly label="Editar" ariaLabel={`Editar ${p.name}`} onClick={() => openForm(p)}>
                          <IconEdit size={18} />
                        </RowAction>
                        <RowAction ownerOnly color="red" label={p.isActive ? 'Desactivar' : 'Ya está inactivo'} ariaLabel={`Desactivar ${p.name}`} disabled={!p.isActive} onClick={() => askDeactivate(p)}>
                          <IconTrash size={18} />
                        </RowAction>
                      </Group>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          {data && <TablePager page={page} totalPages={data.totalPages} total={data.total} onChange={setPage} noun="productos" />}
        </QueryState>
      </Paper>

      {form && (
        <ProductFormModal
          key={form.seq}
          opened={formOpen}
          product={form.product}
          categories={categories.data?.items ?? []}
          onClose={() => setFormOpen(false)}
        />
      )}
      <StockDrawer product={stockOf} onClose={() => setStockOf(null)} />
    </>
  );
}
