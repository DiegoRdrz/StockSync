export { useProductMap } from '@/lib/products';

/** Invalidation keys shared by every stock mutation. */
export const STOCK_KEYS = [['stock'], ['movements'], ['reports']] as const;
