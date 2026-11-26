// Contracts mirrored from the .NET services (camelCase JSON). Keep in sync with the C# DTOs.

/** Inventory, Orders, Reporting and Notification paginate with `total`. */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

/** Identity paginates with `totalCount` instead of `total`. */
export interface IdentityPagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PageQuery {
  page?: number;
  pageSize?: number;
}

// ---------- Identity ----------
/** Identity serializes RoleType as a number; the JWT carries the role name. */
export const RoleType = { Owner: 1, Employee: 2 } as const;
export type RoleTypeValue = (typeof RoleType)[keyof typeof RoleType];
export type RoleName = 'Owner' | 'Employee';

export interface UserResponse {
  id: string;
  tenantId: string;
  email: string;
  firstName: string;
  lastName: string;
  role: RoleTypeValue;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface TenantResponse {
  id: string;
  name: string;
  taxId: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface AuthResponse {
  accessToken: string;
  tokenType: string;
  expiresIn: number;
  user: UserResponse;
  tenant: TenantResponse;
}

export interface CurrentUserResponse {
  user: UserResponse;
  tenant: TenantResponse;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterTenantRequest {
  companyName: string;
  taxId?: string | null;
  firstName: string;
  lastName: string;
  email: string;
  password: string;
}

export interface UpdateTenantRequest {
  name: string;
  taxId?: string | null;
}

export interface UserFilter extends PageQuery {
  search?: string;
  role?: RoleTypeValue;
  isActive?: boolean;
}

export interface CreateUserRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  role: RoleTypeValue;
}

export interface UpdateUserRequest {
  firstName: string;
  lastName: string;
  role?: RoleTypeValue | null;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

// ---------- Inventory ----------
export interface CategoryResponse {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface CategoryRequest {
  name: string;
  description?: string | null;
}

export interface CategoryFilter extends PageQuery {
  name?: string;
}

export interface ProductResponse {
  id: string;
  name: string;
  sku: string;
  description: string | null;
  purchasePrice: number;
  salePrice: number;
  minimumStock: number;
  categoryId: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface ProductRequest {
  name: string;
  sku: string;
  description?: string | null;
  purchasePrice: number;
  salePrice: number;
  minimumStock: number;
  categoryId?: string | null;
}

export interface ProductFilter extends PageQuery {
  name?: string;
  sku?: string;
  categoryId?: string;
}

export interface StockResponse {
  id: string;
  productId: string;
  branchId: string;
  quantity: number;
}

export interface StockRequest {
  productId: string;
  branchId: string;
  quantity: number;
}

export interface StockQuantityRequest {
  newQuantity: number;
}

export interface LowStockResponse {
  stockId: string;
  productId: string;
  productName: string;
  productSku: string;
  branchId: string;
  quantity: number;
  minimumStock: number;
  shortage: number;
}

export type MovementType = 'Receipt' | 'Issue' | 'Adjustment';

export interface StockMovementResponse {
  id: string;
  stockId: string;
  type: MovementType;
  quantity: number;
  previousQuantity: number;
  newQuantity: number;
  date: string;
}

export interface StockMovementListResponse extends StockMovementResponse {
  productId: string;
  productName: string;
  productSku: string;
  branchId: string;
}

// ---------- Orders ----------
export type OrderStatus = 'Draft' | 'Confirming' | 'Confirmed' | 'RequiresReview' | 'Cancelled';
export type PurchaseOrderStatus =
  | 'Draft'
  | 'Ordered'
  | 'Receiving'
  | 'Received'
  | 'RequiresReview'
  | 'Cancelled';

export interface LineResponse {
  id: string;
  lineNumber: number;
  productId: string;
  stockId: string;
  productName: string;
  productSku: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  stockStatus: string;
  stockMovementId: string | null;
  compensationMovementId: string | null;
}

export interface OrderResponse {
  id: string;
  branchId: string;
  status: OrderStatus;
  notes: string | null;
  total: number;
  reviewReason: string | null;
  createdAt: string;
  updatedAt: string | null;
  confirmedAt: string | null;
  cancelledAt: string | null;
  lines: LineResponse[];
}

export interface PurchaseOrderResponse {
  id: string;
  branchId: string;
  supplierName: string;
  status: PurchaseOrderStatus;
  notes: string | null;
  total: number;
  reviewReason: string | null;
  createdAt: string;
  updatedAt: string | null;
  orderedAt: string | null;
  receivedAt: string | null;
  cancelledAt: string | null;
  lines: LineResponse[];
}

export interface OrderRequest {
  branchId: string;
  notes?: string | null;
}

export interface PurchaseOrderRequest {
  branchId: string;
  supplierName: string;
  notes?: string | null;
}

export interface LineRequest {
  productId: string;
  quantity: number;
}

export interface LineQuantityRequest {
  quantity: number;
}

export interface DocumentFilter<TStatus> extends PageQuery {
  status?: TStatus;
  branchId?: string;
  /** ISO date or UTC timestamp, applied to the creation date. */
  from?: string;
  to?: string;
}

// ---------- Reporting ----------
/** Dates are `yyyy-MM-dd` or UTC timestamps; default range is the last 30 days, max 366. */
export interface ReportQuery {
  from?: string;
  to?: string;
  branchId?: string;
}

export interface SalesSummary {
  totalAmount: number;
  salesCount: number;
  averageTicket: number;
  units: number;
}

export interface DailySales {
  day: string;
  amount: number;
  count: number;
  units: number;
}

export interface ProductSales {
  productId: string;
  productSku: string;
  productName: string;
  quantity: number;
  revenue: number;
}

export interface SaleLine {
  orderId: string;
  lineNumber: number;
  stockMovementId: string | null;
  productId: string;
  productSku: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface SaleDetail {
  orderId: string;
  branchId: string;
  confirmedAt: string;
  total: number;
  lines: SaleLine[];
}

export interface PurchaseSummary {
  totalAmount: number;
  receivedPurchases: number;
  unitsReceived: number;
}

export interface DailyPurchases {
  day: string;
  amount: number;
  count: number;
  unitsReceived: number;
}

export interface MovementTypeTotals {
  type: string;
  count: number;
  quantity: number;
  netChange: number;
}

export interface MovementDayTotals {
  day: string;
  count: number;
  quantity: number;
  netChange: number;
}

export interface MovementProductTotals {
  productId: string;
  count: number;
  quantity: number;
  netChange: number;
}

export interface MovementSummary {
  byType: MovementTypeTotals[];
  byDay: MovementDayTotals[];
  byProduct: MovementProductTotals[];
}

export type ReportLowStock = LowStockResponse;

// ---------- Notification (string enums) ----------
export type NotificationChannel = 'Email' | 'WhatsApp';
export type NotificationStatus = 'Pending' | 'Sent' | 'Failed';
export type NotificationEventType = 'LowStock' | 'OrderCreated';

export interface NotificationResponse {
  id: string;
  sourceEventId: string;
  eventType: NotificationEventType;
  channel: NotificationChannel;
  recipient: string;
  subject: string;
  body: string;
  branchId: string;
  resourceId: string;
  status: NotificationStatus;
  createdAt: string;
  sentAt: string | null;
  failureReason: string | null;
}

export interface NotificationFilter extends PageQuery {
  channel?: NotificationChannel;
  eventType?: NotificationEventType;
  status?: NotificationStatus;
  branchId?: string;
  from?: string;
  to?: string;
}

export interface NotificationRecipientResponse {
  id: string;
  name: string;
  channel: NotificationChannel;
  destination: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface NotificationRecipientRequest {
  name: string;
  channel: NotificationChannel;
  destination: string;
}

export interface NotificationRecipientFilter extends PageQuery {
  channel?: NotificationChannel;
}
