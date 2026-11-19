export const ordersResources = ['orders', 'purchase-orders'] as const;

// Orders resources keep the same path in the gateway and in the orders service.
export const ordersRoutes = ordersResources.map(resource => ({
  prefix: `/api/${resource}`,
  rewritePrefix: `/api/${resource}`,
}));
