import { Badge, Button, Grid, Group, Modal, Paper, Stack, Table, Text, Textarea, TextInput, UnstyledButton } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useDebouncedValue } from '@mantine/hooks';
import { IconEdit, IconPlus, IconSearch, IconTrash } from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { ApiError } from '@/api/client';
import { categoriesApi } from '@/api/endpoints';
import type { CategoryResponse } from '@/api/types';
import { EmptyState, Money, OwnerAction, PageHeader, QueryState, RowAction, TablePager } from '@/components';
import { formatNumber } from '@/lib/format';
import { confirmAction, notifyError, notifySuccess } from '@/lib/notify';
import { requiredError } from '@/lib/validation';
import { useClampPage } from '@/lib/useClampPage';

function CategoryFormModal({ category, opened, onClose }: { category: CategoryResponse | null; opened: boolean; onClose: () => void }) {
  const qc = useQueryClient();
  const form = useForm({
    initialValues: { name: category?.name ?? '', description: category?.description ?? '' },
    validateInputOnBlur: true,
    validate: { name: requiredError('El nombre') },
  });
  const mutation = useMutation({
    mutationFn: (v: { name: string; description: string }) => {
      const body = { name: v.name.trim(), description: v.description.trim() || null };
      return category ? categoriesApi.update(category.id, body) : categoriesApi.create(body);
    },
    onSuccess: async () => {
      notifySuccess(category ? 'Categoría guardada' : 'Categoría creada');
      await qc.invalidateQueries({ queryKey: ['categories'] });
      onClose();
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 400) form.setErrors(error.formErrors());
      if (error instanceof ApiError && error.status === 409) form.setFieldError('name', 'Ya existe una categoría con este nombre');
      notifyError(error, category ? 'No se pudo guardar la categoría' : 'No se pudo crear la categoría');
    },
  });
  return (
    <Modal opened={opened} onClose={onClose} title={category ? 'Editar categoría' : 'Nueva categoría'} centered>
      <form onSubmit={form.onSubmit((v) => mutation.mutate(v))}>
        <Stack gap="sm">
          <TextInput label="Nombre" data-autofocus withAsterisk {...form.getInputProps('name')} />
          <Textarea label="Descripción" autosize minRows={2} maxRows={4} {...form.getInputProps('description')} />
          <Group justify="flex-end" mt="xs">
            <Button variant="default" onClick={onClose}>Cancelar</Button>
            <Button type="submit" loading={mutation.isPending}>{category ? 'Guardar cambios' : 'Crear categoría'}</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

function CategoryProducts({ category }: { category: CategoryResponse }) {
  const [page, setPage] = useState(1);
  const query = useQuery({
    queryKey: ['categories', 'products', category.id, page],
    queryFn: () => categoriesApi.products(category.id, { page, pageSize: 10 }),
    placeholderData: (prev) => prev,
  });
  const data = query.data;
  useClampPage(page, data?.totalPages, setPage);

  return (
    <QueryState
      isLoading={query.isLoading}
      error={query.error}
      onRetry={() => query.refetch()}
      isEmpty={!data || data.items.length === 0}
      empty={<EmptyState title="Sin productos" description="Asigna esta categoría a un producto desde Productos." />}
    >
      <Table.ScrollContainer minWidth={420}>
        <Table verticalSpacing={6}>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Producto</Table.Th>
              <Table.Th ta="right">Venta</Table.Th>
              <Table.Th ta="right">Mínimo</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {data?.items.map((p) => (
              <Table.Tr key={p.id} c={p.isActive ? undefined : 'dimmed'}>
                <Table.Td>
                  <Text size="sm" fw={500} inherit lineClamp={1}>{p.name}</Text>
                  <Text size="xs" c="dimmed">{p.sku}{p.isActive ? '' : ' (inactivo)'}</Text>
                </Table.Td>
                <Table.Td ta="right"><Money value={p.salePrice} size="sm" /></Table.Td>
                <Table.Td ta="right"><Text size="sm" className="tabular" inherit>{formatNumber(p.minimumStock)}</Text></Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
      {data && <TablePager page={page} totalPages={data.totalPages} total={data.total} onChange={setPage} noun="productos" />}
    </QueryState>
  );
}

export function CategoriesPage() {
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const [debounced] = useDebouncedValue(search.trim(), 300);
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [editing, setEditing] = useState<{ category: CategoryResponse | null; seq: number } | null>(null);
  const [formOpen, setFormOpen] = useState(false);

  const query = useQuery({
    queryKey: ['categories', 'list', debounced, page],
    queryFn: () => categoriesApi.list({ name: debounced || undefined, page, pageSize: 20 }),
    placeholderData: (prev) => prev,
  });
  const data = query.data;
  const selected = data?.items.find((c) => c.id === selectedId) ?? null;

  const remove = useMutation({
    mutationFn: (id: string) => categoriesApi.remove(id),
    onSuccess: async (_, id) => {
      notifySuccess('Categoría eliminada');
      if (id === selectedId) setSelectedId(null);
      await qc.invalidateQueries({ queryKey: ['categories'] });
      await qc.invalidateQueries({ queryKey: ['products'] });
    },
    onError: (e) => notifyError(e, 'No se pudo eliminar la categoría'),
  });

  const openForm = (category: CategoryResponse | null) => {
    setEditing((e) => ({ category, seq: (e?.seq ?? 0) + 1 }));
    setFormOpen(true);
  };
  const askRemove = (c: CategoryResponse) =>
    confirmAction({
      title: 'Eliminar categoría',
      message: `Se eliminará la categoría ${c.name}. No es posible si todavía tiene productos activos.`,
      confirmLabel: 'Eliminar categoría',
      destructive: true,
      onConfirm: () => remove.mutate(c.id),
    });

  useClampPage(page, data?.totalPages, setPage);

  return (
    <>
      <PageHeader
        title="Categorías"
        actions={
          <OwnerAction>
            <Button leftSection={<IconPlus size={16} />} onClick={() => openForm(null)}>Nueva categoría</Button>
          </OwnerAction>
        }
      />
      <Grid gap="md">
        <Grid.Col span={{ base: 12, md: 5 }}>
          <Paper withBorder>
            <Group p="sm">
              <TextInput
                aria-label="Buscar categoría"
                placeholder="Buscar categoría"
                leftSection={<IconSearch size={16} />}
                value={search}
                onChange={(e) => { setSearch(e.currentTarget.value); setPage(1); }}
                style={{ flex: 1 }}
              />
            </Group>
            <QueryState
              isLoading={query.isLoading}
              error={query.error}
              onRetry={() => query.refetch()}
              isEmpty={!data || data.items.length === 0}
              empty={
                debounced ? (
                  <EmptyState title="Sin resultados" />
                ) : (
                  <EmptyState title="Aún no hay categorías" />
                )
              }
            >
              <Stack gap={0}>
                {data?.items.map((c) => (
                  <Group
                    key={c.id}
                    wrap="nowrap"
                    gap="xs"
                    px="sm"
                    py={6}
                    style={{ borderTop: '1px solid var(--mantine-color-default-border)', background: c.id === selectedId ? 'var(--mantine-color-pine-light)' : undefined }}
                  >
                    <UnstyledButton onClick={() => setSelectedId(c.id)} aria-pressed={c.id === selectedId} style={{ flex: 1, minWidth: 0 }} py={4}>
                      <Group gap="xs" wrap="nowrap">
                        <Text size="sm" fw={500} lineClamp={1}>{c.name}</Text>
                        {!c.isActive && <Badge color="gray" variant="light" size="sm">Inactiva</Badge>}
                      </Group>
                      {c.description && <Text size="xs" c="dimmed" lineClamp={1}>{c.description}</Text>}
                    </UnstyledButton>
                    <RowAction ownerOnly label="Editar" ariaLabel={`Editar ${c.name}`} onClick={() => openForm(c)}>
                      <IconEdit size={18} />
                    </RowAction>
                    <RowAction ownerOnly color="red" label="Eliminar" ariaLabel={`Eliminar ${c.name}`} onClick={() => askRemove(c)}>
                      <IconTrash size={18} />
                    </RowAction>
                  </Group>
                ))}
              </Stack>
              {data && <TablePager page={page} totalPages={data.totalPages} total={data.total} onChange={setPage} noun="categorías" />}
            </QueryState>
          </Paper>
        </Grid.Col>
        <Grid.Col span={{ base: 12, md: 7 }}>
          <Paper withBorder>
            {selected ? (
              <>
                <Stack gap={2} p="sm">
                  <Text fw={600}>Productos en {selected.name}</Text>
                  {selected.description && <Text size="sm" c="dimmed">{selected.description}</Text>}
                </Stack>
                <CategoryProducts key={selected.id} category={selected} />
              </>
            ) : (
              <EmptyState title="Elige una categoría" />
            )}
          </Paper>
        </Grid.Col>
      </Grid>
      {editing && <CategoryFormModal key={editing.seq} category={editing.category} opened={formOpen} onClose={() => setFormOpen(false)} />}
    </>
  );
}
