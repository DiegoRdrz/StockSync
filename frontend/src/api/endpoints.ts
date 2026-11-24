// One function per gateway route. Pages call these through TanStack Query.
import { http, request } from './client';
import type {
  AuthResponse,
  CategoryFilter,
  CategoryRequest,
  CategoryResponse,
  ChangePasswordRequest,
  CreateUserRequest,
  CurrentUserResponse,
  DailyPurchases,
  DailySales,
  DocumentFilter,
  IdentityPagedResult,
  LineQuantityRequest,
  LineRequest,
  LoginRequest,
  LowStockResponse,
  MovementSummary,
  NotificationFilter,
  NotificationRecipientFilter,
  NotificationRecipientRequest,
  NotificationRecipientResponse,
  NotificationResponse,
  OrderRequest,
  OrderResponse,
  OrderStatus,
  PageQuery,
  PagedResult,
  ProductFilter,
  ProductRequest,
  ProductResponse,
  ProductSales,
  PurchaseOrderRequest,
  PurchaseOrderResponse,
  PurchaseOrderStatus,
  PurchaseSummary,
  RegisterTenantRequest,
  ReportLowStock,
  ReportQuery,
  SaleDetail,
  SalesSummary,
  StockMovementListResponse,
  StockMovementResponse,
  StockQuantityRequest,
  StockRequest,
  StockResponse,
  TenantResponse,
  UpdateTenantRequest,
  UpdateUserRequest,
  UserFilter,
  UserResponse,
} from './types';

export const authApi = {
  login: (body: LoginRequest) =>
    request<AuthResponse>('/api/auth/login', { method: 'POST', body, anonymous: true }),
  registerTenant: (body: RegisterTenantRequest) =>
    request<AuthResponse>('/api/auth/register-tenant', { method: 'POST', body, anonymous: true }),
  me: () => http.get<CurrentUserResponse>('/api/auth/me'),
};

export const usersApi = {
  list: (filter: UserFilter) => http.get<IdentityPagedResult<UserResponse>>('/api/users', filter),
  get: (id: string) => http.get<UserResponse>(`/api/users/${id}`),
  create: (body: CreateUserRequest) => http.post<UserResponse>('/api/users', body),
  update: (id: string, body: UpdateUserRequest) => http.put<UserResponse>(`/api/users/${id}`, body),
  changePassword: (id: string, body: ChangePasswordRequest) =>
    http.put<void>(`/api/users/${id}/password`, body),
  deactivate: (id: string) => http.del(`/api/users/${id}`),
};

export const tenantsApi = {
  current: () => http.get<TenantResponse>('/api/tenants/current'),
  update: (body: UpdateTenantRequest) => http.put<TenantResponse>('/api/tenants/current', body),
};

export const categoriesApi = {
  list: (filter: CategoryFilter) => http.get<PagedResult<CategoryResponse>>('/api/categories', filter),
  get: (id: string) => http.get<CategoryResponse>(`/api/categories/${id}`),
  products: (id: string, query: PageQuery) =>
    http.get<PagedResult<ProductResponse>>(`/api/categories/${id}/products`, query),
  create: (body: CategoryRequest) => http.post<CategoryResponse>('/api/categories', body),
  update: (id: string, body: CategoryRequest) => http.put<CategoryResponse>(`/api/categories/${id}`, body),
  remove: (id: string) => http.del(`/api/categories/${id}`),
};

export const productsApi = {
  list: (filter: ProductFilter) => http.get<PagedResult<ProductResponse>>('/api/products', filter),
  get: (id: string) => http.get<ProductResponse>(`/api/products/${id}`),
  create: (body: ProductRequest) => http.post<ProductResponse>('/api/products', body),
  update: (id: string, body: ProductRequest) => http.put<ProductResponse>(`/api/products/${id}`, body),
  remove: (id: string) => http.del(`/api/products/${id}`),
};

export const stockApi = {
  get: (id: string) => http.get<StockResponse>(`/api/stock/${id}`),
  byBranch: (branchId: string, query: PageQuery) =>
    http.get<PagedResult<StockResponse>>(`/api/stock/branch/${branchId}`, query),
  byProduct: (productId: string, query: PageQuery) =>
    http.get<PagedResult<StockResponse>>(`/api/stock/product/${productId}`, query),
  lowStock: (query: PageQuery & { branchId?: string }) =>
    http.get<PagedResult<LowStockResponse>>('/api/stock/low-stock', query),
  create: (body: StockRequest) => http.post<StockResponse>('/api/stock', body),
  setQuantity: (id: string, body: StockQuantityRequest) => http.put<StockResponse>(`/api/stock/${id}`, body),
  remove: (id: string) => http.del(`/api/stock/${id}`),
  receive: (id: string, quantity: number) =>
    http.post<StockMovementResponse>(`/api/stock/${id}/receipts`, { quantity }),
  issue: (id: string, quantity: number) =>
    http.post<StockMovementResponse>(`/api/stock/${id}/issues`, { quantity }),
  movements: (id: string, query: PageQuery) =>
    http.get<PagedResult<StockMovementResponse>>(`/api/stock/${id}/movements`, query),
};

export const movementsApi = {
  /** Fixed pages of 10, newest first. */
  page: (page: number) =>
    page <= 1
      ? http.get<PagedResult<StockMovementListResponse>>('/api/movements')
      : http.get<PagedResult<StockMovementListResponse>>(`/api/movements/page/${page}`),
};

export const ordersApi = {
  list: (filter: DocumentFilter<OrderStatus>) => http.get<PagedResult<OrderResponse>>('/api/orders', filter),
  get: (id: string) => http.get<OrderResponse>(`/api/orders/${id}`),
  create: (body: OrderRequest) => http.post<OrderResponse>('/api/orders', body),
  addLine: (id: string, body: LineRequest) => http.post<OrderResponse>(`/api/orders/${id}/lines`, body),
  updateLine: (id: string, lineId: string, body: LineQuantityRequest) =>
    http.put<OrderResponse>(`/api/orders/${id}/lines/${lineId}`, body),
  removeLine: (id: string, lineId: string) => http.del<OrderResponse>(`/api/orders/${id}/lines/${lineId}`),
  confirm: (id: string) => http.post<OrderResponse>(`/api/orders/${id}/confirm`),
  cancel: (id: string) => http.post<OrderResponse>(`/api/orders/${id}/cancel`),
};

export const purchaseOrdersApi = {
  list: (filter: DocumentFilter<PurchaseOrderStatus>) =>
    http.get<PagedResult<PurchaseOrderResponse>>('/api/purchase-orders', filter),
  get: (id: string) => http.get<PurchaseOrderResponse>(`/api/purchase-orders/${id}`),
  create: (body: PurchaseOrderRequest) => http.post<PurchaseOrderResponse>('/api/purchase-orders', body),
  addLine: (id: string, body: LineRequest) =>
    http.post<PurchaseOrderResponse>(`/api/purchase-orders/${id}/lines`, body),
  updateLine: (id: string, lineId: string, body: LineQuantityRequest) =>
    http.put<PurchaseOrderResponse>(`/api/purchase-orders/${id}/lines/${lineId}`, body),
  removeLine: (id: string, lineId: string) =>
    http.del<PurchaseOrderResponse>(`/api/purchase-orders/${id}/lines/${lineId}`),
  place: (id: string) => http.post<PurchaseOrderResponse>(`/api/purchase-orders/${id}/place`),
  receive: (id: string) => http.post<PurchaseOrderResponse>(`/api/purchase-orders/${id}/receive`),
  cancel: (id: string) => http.post<PurchaseOrderResponse>(`/api/purchase-orders/${id}/cancel`),
};

export const reportsApi = {
  /** Owner only. */
  salesSummary: (q: ReportQuery) => http.get<SalesSummary>('/api/reports/sales/summary', q),
  salesDaily: (q: ReportQuery) => http.get<DailySales[]>('/api/reports/sales/daily', q),
  topProducts: (q: ReportQuery & { limit?: number }) =>
    http.get<ProductSales[]>('/api/reports/sales/top-products', q),
  /** Owner only. */
  salesDetail: (q: ReportQuery & PageQuery) => http.get<PagedResult<SaleDetail>>('/api/reports/sales', q),
  /** Owner only. */
  purchasesSummary: (q: ReportQuery) => http.get<PurchaseSummary>('/api/reports/purchases/summary', q),
  /** Owner only. */
  purchasesDaily: (q: ReportQuery) => http.get<DailyPurchases[]>('/api/reports/purchases/daily', q),
  movementsSummary: (q: ReportQuery) =>
    http.get<MovementSummary>('/api/reports/inventory/movements/summary', q),
  lowStock: (q: PageQuery & { branchId?: string }) =>
    http.get<PagedResult<ReportLowStock>>('/api/reports/inventory/low-stock', q),
};

export const notificationsApi = {
  list: (filter: NotificationFilter) =>
    http.get<PagedResult<NotificationResponse>>('/api/notifications', filter),
  get: (id: string) => http.get<NotificationResponse>(`/api/notifications/${id}`),
};

export const recipientsApi = {
  list: (filter: NotificationRecipientFilter) =>
    http.get<PagedResult<NotificationRecipientResponse>>('/api/notifications/recipients', filter),
  create: (body: NotificationRecipientRequest) =>
    http.post<NotificationRecipientResponse>('/api/notifications/recipients', body),
  update: (id: string, body: NotificationRecipientRequest) =>
    http.put<NotificationRecipientResponse>(`/api/notifications/recipients/${id}`, body),
  remove: (id: string) => http.del(`/api/notifications/recipients/${id}`),
};
