export const identityResources = ['auth', 'users', 'tenants'] as const;

export const identityRoutes = [
  ...identityResources.map(resource => ({
    prefix: `/api/identity/${resource}`,
    rewritePrefix: `/api/${resource}`,
  })),
  ...identityResources.map(resource => ({
    prefix: `/api/${resource}`,
    rewritePrefix: `/api/${resource}`,
  })),
];

