import { useQuery } from '@tanstack/react-query';
import { productsApi } from '@/api/endpoints';
import type { ProductResponse } from '@/api/types';

const PAGE_SIZE = 100;
const MAX_PAGES = 10;

/**
 * Whole product catalogue (up to 1,000 items) as a Map by id. Stock, movements and
 * reports only return product IDs, so several screens resolve names through this.
 */
export function useProductMap() {
  const query = useQuery({
    queryKey: ['products', 'all'],
    queryFn: async () => {
      const first = await productsApi.list({ page: 1, pageSize: PAGE_SIZE });
      const items = [...first.items];
      const last = Math.min(first.totalPages, MAX_PAGES);
      for (let page = 2; page <= last; page++) {
        items.push(...(await productsApi.list({ page, pageSize: PAGE_SIZE })).items);
      }
      return new Map<string, ProductResponse>(items.map((p) => [p.id, p]));
    },
    staleTime: 30_000,
  });
  return query;
}
