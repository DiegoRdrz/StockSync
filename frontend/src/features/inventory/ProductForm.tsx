import { Button, Group, Modal, NumberInput, Select, SimpleGrid, Stack, Textarea, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ApiError } from '@/api/client';
import { productsApi } from '@/api/endpoints';
import type { CategoryResponse, ProductRequest, ProductResponse } from '@/api/types';
import { notifyError, notifySuccess } from '@/lib/notify';
import { requiredError } from '@/lib/validation';

interface FormValues {
  name: string;
  sku: string;
  description: string;
  purchasePrice: number | string;
  salePrice: number | string;
  minimumStock: number | string;
  categoryId: string | null;
}

const priceError = (v: number | string) => (v === '' || Number(v) < 0 || Number.isNaN(Number(v)) ? 'Escribe un precio de 0 o más' : null);

const toValues = (p: ProductResponse | null): FormValues => ({
  name: p?.name ?? '',
  sku: p?.sku ?? '',
  description: p?.description ?? '',
  purchasePrice: p?.purchasePrice ?? 0,
  salePrice: p?.salePrice ?? 0,
  minimumStock: p?.minimumStock ?? 0,
  categoryId: p?.categoryId ?? null,
});

interface ProductFormModalProps {
  opened: boolean;
  /** Product being edited, or null to create. */
  product: ProductResponse | null;
  categories: CategoryResponse[];
  onClose: () => void;
}

/** Mount with a `key` so every opening starts from the product's current values. */
export function ProductFormModal({ opened, product, categories, onClose }: ProductFormModalProps) {
  const qc = useQueryClient();
  const form = useForm<FormValues>({
    initialValues: toValues(product),
    validateInputOnBlur: true,
    validate: {
      name: requiredError('El nombre'),
      sku: requiredError('El SKU'),
      purchasePrice: priceError,
      salePrice: priceError,
      minimumStock: (v) => (v !== '' && Number.isInteger(Number(v)) && Number(v) >= 0 ? null : 'Escribe un entero de 0 o más'),
    },
  });

  const mutation = useMutation({
    mutationFn: (v: FormValues) => {
      const body: ProductRequest = {
        name: v.name.trim(),
        sku: v.sku.trim(),
        description: v.description.trim() || null,
        purchasePrice: Number(v.purchasePrice),
        salePrice: Number(v.salePrice),
        minimumStock: Number(v.minimumStock),
        categoryId: v.categoryId,
      };
      return product ? productsApi.update(product.id, body) : productsApi.create(body);
    },
    onSuccess: async () => {
      notifySuccess(product ? 'Producto guardado' : 'Producto creado');
      await Promise.all([
        qc.invalidateQueries({ queryKey: ['products'] }),
        qc.invalidateQueries({ queryKey: ['categories'] }),
        qc.invalidateQueries({ queryKey: ['stock'] }),
      ]);
      onClose();
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 400) form.setErrors(error.formErrors());
      if (error instanceof ApiError && error.status === 409) form.setFieldError('sku', 'Ya existe un producto con este SKU');
      notifyError(error, product ? 'No se pudo guardar el producto' : 'No se pudo crear el producto');
    },
  });

  const money = { min: 0, decimalScale: 2, allowNegative: false, prefix: '$ ', thousandSeparator: '.', decimalSeparator: ',' } as const;

  return (
    <Modal opened={opened} onClose={onClose} title={product ? 'Editar producto' : 'Nuevo producto'} centered size="lg">
      <form onSubmit={form.onSubmit((v) => mutation.mutate(v))}>
        <Stack gap="sm">
          <SimpleGrid cols={{ base: 1, xs: 2 }} spacing="sm">
            <TextInput label="Nombre" data-autofocus withAsterisk {...form.getInputProps('name')} />
            <TextInput label="SKU" withAsterisk {...form.getInputProps('sku')} />
          </SimpleGrid>
          <Textarea label="Descripción" autosize minRows={2} maxRows={4} {...form.getInputProps('description')} />
          <SimpleGrid cols={{ base: 1, xs: 2 }} spacing="sm">
            <NumberInput label="Precio de compra" {...money} {...form.getInputProps('purchasePrice')} />
            <NumberInput label="Precio de venta" {...money} {...form.getInputProps('salePrice')} />
          </SimpleGrid>
          <SimpleGrid cols={{ base: 1, xs: 2 }} spacing="sm">
            <NumberInput
              label="Stock mínimo"
              description="Se avisa cuando una sucursal baja de esta cantidad"
              min={0}
              allowDecimal={false}
              allowNegative={false}
              thousandSeparator="."
              {...form.getInputProps('minimumStock')}
            />
            <Select
              label="Categoría"
              placeholder="Sin categoría"
              clearable
              searchable
              data={categories.filter((c) => c.isActive || c.id === product?.categoryId).map((c) => ({ value: c.id, label: c.name }))}
              {...form.getInputProps('categoryId')}
            />
          </SimpleGrid>
          <Group justify="flex-end" mt="xs">
            <Button variant="default" onClick={onClose}>Cancelar</Button>
            <Button type="submit" loading={mutation.isPending}>{product ? 'Guardar cambios' : 'Crear producto'}</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
