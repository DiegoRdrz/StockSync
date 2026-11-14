export const inventoryResources = ['products', 'categories', 'stock', 'movements'] as const;

export const inventoryRoutes = inventoryResources.map(resource => ({
  prefix: `/api/inventory/${resource}`,
  rewritePrefix: `/api/${resource}`,
}));

export function toPublicInventoryPath(path: string): string {
  const normalizedPath = path.toLowerCase();
  for (const route of inventoryRoutes) {
    if (normalizedPath === route.rewritePrefix || normalizedPath.startsWith(route.rewritePrefix + '/')) {
      return route.prefix + path.slice(route.rewritePrefix.length);
    }
  }
  return path;
}
