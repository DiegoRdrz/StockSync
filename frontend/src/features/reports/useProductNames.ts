import { useProductMap } from '@/lib/products';

/** Resolves product names for reports that only return product IDs. */
export function useProductNames() {
  const { data: map } = useProductMap();
  return {
    nameOf: (id: string) => map?.get(id)?.name ?? `Producto ${id.slice(0, 8)}`,
    skuOf: (id: string) => map?.get(id)?.sku,
  };
}
