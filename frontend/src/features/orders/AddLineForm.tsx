import { Button, Group, NumberInput, Paper, Text, Title } from '@mantine/core';
import { IconPlus } from '@tabler/icons-react';
import { useState } from 'react';
import type { ProductResponse } from '@/api/types';
import { ProductSelect } from '@/components';
import { formatMoney } from '@/lib/format';
import { errorMessage } from '@/lib/notify';
import type { DocumentBase, DocumentKind } from './documents';
import { useLineMutations } from './useDocument';

/** "Agregar producto": the unit price comes from Inventory, so only an estimate is shown here. */
export function AddLineForm({ kind, doc, busy }: { kind: DocumentKind; doc: DocumentBase; busy: boolean }) {
  const { add } = useLineMutations(kind, doc.id);
  const [product, setProduct] = useState<ProductResponse | null>(null);
  const [quantity, setQuantity] = useState<number | string>(1);
  const [error, setError] = useState<string | null>(null);

  const qty = typeof quantity === 'number' ? quantity : Number(quantity);
  const price = product?.[kind.priceField];

  const submit = () => {
    if (!product) return setError('Selecciona un producto');
    if (!Number.isInteger(qty) || qty < 1) return setError('La cantidad debe ser un entero mayor que cero');
    if (doc.lines.some((l) => l.productId === product.id))
      return setError('Este producto ya está en el documento. Cambia su cantidad en la tabla.');
    setError(null);
    add.mutate(
      { productId: product.id, quantity: qty },
      {
        onSuccess: () => {
          setProduct(null);
          setQuantity(1);
        },
        onError: (e) => setError(errorMessage(e)),
      },
    );
  };

  return (
    <Paper withBorder p="sm" component="form" aria-label="Agregar producto" onSubmit={(e) => { e.preventDefault(); submit(); }}>
      <Title order={3} size="h5" mb="xs">Agregar producto</Title>
      <Group align="flex-start" gap="sm" wrap="wrap">
        <ProductSelect
          label="Producto"
          priceField={kind.priceField}
          value={product?.id ?? null}
          onChange={(_, p) => { setProduct(p); setError(null); }}
          error={error}
          style={{ flex: '1 1 240px' }}
          disabled={busy}
        />
        <NumberInput
          label="Cantidad"
          min={1}
          allowDecimal={false}
          allowNegative={false}
          clampBehavior="strict"
          value={quantity}
          onChange={setQuantity}
          w={{ base: '100%', xs: 110 }}
          disabled={busy}
        />
        <Button type="submit" mt={{ base: 0, xs: 25.5 }} leftSection={<IconPlus size={16} />} loading={add.isPending} disabled={busy}>
          Agregar
        </Button>
      </Group>
      {price !== undefined && qty > 0 && (
        <Text size="xs" c="dimmed" mt="xs" className="tabular">
          {`Subtotal estimado ${formatMoney(price * qty)}, ${formatMoney(price)} c/u`}
        </Text>
      )}
    </Paper>
  );
}
