import { Group, Select, Text, type SelectProps } from '@mantine/core';
import { useDebouncedValue } from '@mantine/hooks';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { productsApi } from '@/api/endpoints';
import type { ProductResponse } from '@/api/types';
import { formatMoney } from '@/lib/format';

const optionLabel = (p: ProductResponse) => `${p.name} (${p.sku})`;

type ProductSelectProps = Omit<SelectProps, 'data' | 'onChange' | 'searchValue' | 'onSearchChange'> & {
  onChange: (productId: string | null, product: ProductResponse | null) => void;
  /** Which price to show next to each option. */
  priceField?: 'salePrice' | 'purchasePrice';
};

/** Searchable product picker backed by GET /api/products?name=... */
export function ProductSelect({ onChange, priceField = 'salePrice', ...props }: ProductSelectProps) {
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<ProductResponse | null>(null);
  // After a pick, Mantine puts the option label in the search box; that label is not a
  // name filter, so search with an empty term until the user types something else.
  const term = selected && search === optionLabel(selected) ? '' : search;
  const [debounced] = useDebouncedValue(term, 250);
  const { data, isFetching } = useQuery({
    queryKey: ['products', 'picker', debounced],
    queryFn: () => productsApi.list({ name: debounced || undefined, pageSize: 30 }),
  });
  const products = (data?.items ?? []).filter((p) => p.isActive);
  // Keep the chosen product as an option even when the current results do not include it.
  if (selected && !products.some((p) => p.id === selected.id)) products.unshift(selected);
  const byId = new Map(products.map((p) => [p.id, p]));

  return (
    <Select
      searchable
      clearable
      placeholder="Busca por nombre"
      nothingFoundMessage={isFetching ? 'Buscando…' : 'Ningún producto coincide'}
      filter={({ options }) => options}
      searchValue={search}
      onSearchChange={setSearch}
      data={products.map((p) => ({ value: p.id, label: optionLabel(p) }))}
      renderOption={({ option }) => {
        const product = byId.get(option.value);
        return (
          <Group justify="space-between" wrap="nowrap" w="100%">
            <div>
              <Text size="sm">{product?.name}</Text>
              <Text size="xs" c="dimmed">{product?.sku}</Text>
            </div>
            {product && (
              <Text size="xs" c="dimmed" className="tabular">
                {formatMoney(product[priceField])}
              </Text>
            )}
          </Group>
        );
      }}
      onChange={(value) => {
        const product = value ? byId.get(value) ?? null : null;
        setSelected(product);
        onChange(value, product);
      }}
      {...props}
    />
  );
}
