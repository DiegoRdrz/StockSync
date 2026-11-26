import { Button, Drawer, Group, Modal, NumberInput, Paper, Stack, Table, Text } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { ApiError } from '@/api/client';
import { stockApi } from '@/api/endpoints';
import type { ProductResponse, StockResponse } from '@/api/types';
import { ProductSelect, QueryState, StatusBadge, TablePager } from '@/components';
import { formatDateTime, formatNumber } from '@/lib/format';
import { notifyError, notifySuccess } from '@/lib/notify';
import { MovementAmount, movementDelta } from './MovementAmount';
import { STOCK_KEYS } from './hooks';

function useInvalidateStock() {
  const qc = useQueryClient();
  return () => Promise.all(STOCK_KEYS.map((key) => qc.invalidateQueries({ queryKey: key })));
}

interface RowModalProps {
  stock: StockResponse | null;
  product: ProductResponse | undefined;
  onClose: () => void;
}

/** Receipt or issue of units against one stock row. */
export function MovementModal({ kind, stock, product, onClose }: RowModalProps & { kind: 'receipt' | 'issue' }) {
  const invalidate = useInvalidateStock();
  const isReceipt = kind === 'receipt';
  const form = useForm({
    initialValues: { quantity: 1 as number | string },
    validateInputOnBlur: true,
    validate: {
      quantity: (v) => {
        const n = Number(v);
        if (!Number.isInteger(n) || n <= 0) return 'Escribe una cantidad mayor a 0';
        if (!isReceipt && stock && n > stock.quantity) return `Solo hay ${formatNumber(stock.quantity)} unidades disponibles`;
        return null;
      },
    },
  });
  const preview = Number(form.values.quantity) || 0;
  const label = isReceipt ? 'Registrar entrada' : 'Registrar salida';

  const mutation = useMutation({
    mutationFn: (quantity: number) =>
      isReceipt ? stockApi.receive(stock!.id, quantity) : stockApi.issue(stock!.id, quantity),
    onSuccess: async () => {
      notifySuccess(isReceipt ? 'Entrada registrada' : 'Salida registrada');
      await invalidate();
      onClose();
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 400) form.setErrors(error.formErrors());
      notifyError(error, `No se pudo ${isReceipt ? 'registrar la entrada' : 'registrar la salida'}`);
    },
  });

  const current = stock?.quantity ?? 0;
  const result = isReceipt ? current + preview : current - preview;

  return (
    <Modal opened={!!stock} onClose={onClose} title={label} centered>
      <form onSubmit={form.onSubmit((v) => mutation.mutate(Number(v.quantity)))}>
        <Stack gap="sm">
          <div>
            <Text fw={600}>{product?.name ?? 'Producto'}</Text>
            <Text size="xs" c="dimmed">{product?.sku}</Text>
          </div>
          <NumberInput
            label="Cantidad"
            data-autofocus
            min={1}
            allowDecimal={false}
            allowNegative={false}
            thousandSeparator="."
            decimalSeparator=","
            {...form.getInputProps('quantity')}
          />
          <Paper bg="var(--mantine-color-gray-light)" p="xs" radius="sm">
            <Group justify="space-between">
              <Text size="sm" c="dimmed">Saldo actual</Text>
              <Text size="sm" className="tabular">{formatNumber(current)}</Text>
            </Group>
            <Group justify="space-between">
              <Text size="sm" c="dimmed">Saldo resultante</Text>
              <Text size="sm" fw={600} className="tabular" c={result < 0 ? 'red.7' : undefined}>
                {formatNumber(result)}
              </Text>
            </Group>
          </Paper>
          <Group justify="flex-end" mt="xs">
            <Button variant="default" onClick={onClose}>Cancelar</Button>
            <Button type="submit" loading={mutation.isPending} color={isReceipt ? 'pine' : 'red'}>
              {label}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

/** Owner: set the exact quantity (manual adjustment). */
export function AdjustModal({ stock, product, onClose }: RowModalProps) {
  const invalidate = useInvalidateStock();
  const form = useForm({
    initialValues: { quantity: stock?.quantity ?? 0 },
    validateInputOnBlur: true,
    validate: { quantity: (v) => (Number.isInteger(Number(v)) && Number(v) >= 0 ? null : 'Escribe una cantidad de 0 o más') },
  });
  const mutation = useMutation({
    mutationFn: (newQuantity: number) => stockApi.setQuantity(stock!.id, { newQuantity }),
    onSuccess: async () => {
      notifySuccess('Cantidad ajustada');
      await invalidate();
      onClose();
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 400) form.setErrors(error.formErrors());
      notifyError(error, 'No se pudo ajustar la cantidad');
    },
  });
  return (
    <Modal opened={!!stock} onClose={onClose} title="Ajustar cantidad" centered>
      {stock && (
        <form key={stock.id} onSubmit={form.onSubmit((v) => mutation.mutate(Number(v.quantity)))}>
          <Stack gap="sm">
            <div>
              <Text fw={600}>{product?.name ?? 'Producto'}</Text>
              <Text size="xs" c="dimmed">Actualmente hay {formatNumber(stock.quantity)} unidades.</Text>
            </div>
            <NumberInput
              label="Cantidad real"
              description="Queda registrado como un movimiento de ajuste."
              data-autofocus
              min={0}
              allowDecimal={false}
              allowNegative={false}
              thousandSeparator="."
              decimalSeparator=","
              {...form.getInputProps('quantity')}
            />
            <Group justify="flex-end" mt="xs">
              <Button variant="default" onClick={onClose}>Cancelar</Button>
              <Button type="submit" loading={mutation.isPending}>Ajustar cantidad</Button>
            </Group>
          </Stack>
        </form>
      )}
    </Modal>
  );
}

/** Owner: create the product <-> branch assignment with its initial quantity. */
export function AssignModal({ opened, branchId, branchName, onClose }: { opened: boolean; branchId: string; branchName: string; onClose: () => void }) {
  const invalidate = useInvalidateStock();
  const form = useForm<{ productId: string; quantity: number | string }>({
    initialValues: { productId: '', quantity: 0 },
    validateInputOnBlur: true,
    validate: {
      productId: (v) => (v ? null : 'Elige un producto'),
      quantity: (v) => (Number.isInteger(Number(v)) && Number(v) >= 0 ? null : 'Escribe una cantidad de 0 o más'),
    },
  });
  const close = () => {
    form.reset();
    onClose();
  };
  const mutation = useMutation({
    mutationFn: (v: { productId: string; quantity: number }) => stockApi.create({ ...v, branchId }),
    onSuccess: async () => {
      notifySuccess('Producto asignado');
      await invalidate();
      close();
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 400) form.setErrors(error.formErrors());
      if (error instanceof ApiError && error.status === 409) {
        form.setFieldError('productId', 'Este producto ya está asignado a la sucursal');
      }
      notifyError(error, 'No se pudo asignar el producto');
    },
  });
  return (
    <Modal opened={opened} onClose={close} title="Asignar producto" centered>
      <form onSubmit={form.onSubmit((v) => mutation.mutate({ productId: v.productId, quantity: Number(v.quantity) }))}>
        <Stack gap="sm">
          <Text size="sm" c="dimmed">El producto quedará disponible en {branchName}.</Text>
          <ProductSelect
            label="Producto"
            data-autofocus
            value={form.values.productId || null}
            error={form.errors.productId}
            onChange={(id) => form.setFieldValue('productId', id ?? '')}
          />
          <NumberInput
            label="Cantidad inicial"
            min={0}
            allowDecimal={false}
            allowNegative={false}
            thousandSeparator="."
            decimalSeparator=","
            {...form.getInputProps('quantity')}
          />
          <Group justify="flex-end" mt="xs">
            <Button variant="default" onClick={close}>Cancelar</Button>
            <Button type="submit" loading={mutation.isPending}>Asignar producto</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

/** Movement history of one stock row. */
export function HistoryDrawer({ stock, product, onClose }: RowModalProps) {
  const [page, setPage] = useState(1);
  const query = useQuery({
    queryKey: ['stock', 'movements', stock?.id, page],
    queryFn: () => stockApi.movements(stock!.id, { page, pageSize: 10 }),
    enabled: !!stock,
    placeholderData: (prev) => prev,
  });
  const data = query.data;
  const handleClose = () => {
    setPage(1);
    onClose();
  };
  return (
    <Drawer opened={!!stock} onClose={handleClose} position="right" size="lg" title={<Text fw={650}>Historial de {product?.name ?? 'producto'}</Text>}>
      <QueryState
        isLoading={query.isLoading}
        error={query.error}
        onRetry={() => query.refetch()}
        isEmpty={!data || data.items.length === 0}
      >
        <Table.ScrollContainer minWidth={420}>
          <Table verticalSpacing={6}>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Fecha</Table.Th>
                <Table.Th>Tipo</Table.Th>
                <Table.Th ta="right">Cantidad</Table.Th>
                <Table.Th ta="right">Saldo</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {data?.items.map((m) => (
                <Table.Tr key={m.id}>
                  <Table.Td><Text size="sm" className="tabular">{formatDateTime(m.date)}</Text></Table.Td>
                  <Table.Td><StatusBadge status={m.type} /></Table.Td>
                  <Table.Td ta="right"><MovementAmount delta={movementDelta(m)} /></Table.Td>
                  <Table.Td ta="right">
                    <Text size="sm" className="tabular" c="dimmed">
                      {formatNumber(m.previousQuantity)} → {formatNumber(m.newQuantity)}
                    </Text>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
        {data && <TablePager page={page} totalPages={data.totalPages} total={data.total} onChange={setPage} noun="movimientos" />}
      </QueryState>
    </Drawer>
  );
}
