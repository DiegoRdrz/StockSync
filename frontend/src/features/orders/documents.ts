// Sales and purchase orders share almost every behaviour. A `DocumentKind` describes the few
// differences (API, labels, price field) so list, detail and line components stay generic.
import { ordersApi, purchaseOrdersApi } from '@/api/endpoints';
import type {
  DocumentFilter,
  LineRequest,
  LineResponse,
  OrderRequest,
  OrderStatus,
  PagedResult,
  PurchaseOrderRequest,
  PurchaseOrderStatus,
} from '@/api/types';

/** Fields common to `OrderResponse` and `PurchaseOrderResponse`. */
export interface DocumentBase {
  id: string;
  branchId: string;
  status: string;
  notes: string | null;
  total: number;
  reviewReason: string | null;
  createdAt: string;
  updatedAt: string | null;
  lines: LineResponse[];
  supplierName?: string;
  confirmedAt?: string | null;
  orderedAt?: string | null;
  receivedAt?: string | null;
  cancelledAt?: string | null;
}

export interface CreateDocumentInput {
  branchId: string;
  notes?: string | null;
  supplierName?: string;
}

export interface DocumentKind {
  key: 'orders' | 'purchase-orders';
  path: '/orders' | '/purchase-orders';
  /** Lowercase singular and plural nouns, used in copy ("Nueva venta", "ventas"). */
  singular: string;
  plural: string;
  /** Title-case noun for headings ("Venta 1a2b3c4d"). */
  title: string;
  priceField: 'salePrice' | 'purchasePrice';
  hasSupplier: boolean;
  statuses: string[];
  /** Statuses where the saga is running and the API may still move the document. */
  transientStatuses: string[];
  api: {
    list: (filter: DocumentFilter<string>) => Promise<PagedResult<DocumentBase>>;
    get: (id: string) => Promise<DocumentBase>;
    create: (input: CreateDocumentInput) => Promise<DocumentBase>;
    addLine: (id: string, body: LineRequest) => Promise<DocumentBase>;
    updateLine: (id: string, lineId: string, quantity: number) => Promise<DocumentBase>;
    removeLine: (id: string, lineId: string) => Promise<DocumentBase>;
  };
}

export const salesKind: DocumentKind = {
  key: 'orders',
  path: '/orders',
  singular: 'venta',
  plural: 'ventas',
  title: 'Venta',
  priceField: 'salePrice',
  hasSupplier: false,
  statuses: ['Draft', 'Confirming', 'Confirmed', 'RequiresReview', 'Cancelled'],
  transientStatuses: ['Confirming'],
  api: {
    list: (filter) => ordersApi.list(filter as DocumentFilter<OrderStatus>),
    get: ordersApi.get,
    create: (input: CreateDocumentInput) => ordersApi.create({ branchId: input.branchId, notes: input.notes } satisfies OrderRequest),
    addLine: ordersApi.addLine,
    updateLine: (id, lineId, quantity) => ordersApi.updateLine(id, lineId, { quantity }),
    removeLine: ordersApi.removeLine,
  },
};

export const purchasesKind: DocumentKind = {
  key: 'purchase-orders',
  path: '/purchase-orders',
  singular: 'compra',
  plural: 'compras',
  title: 'Compra',
  priceField: 'purchasePrice',
  hasSupplier: true,
  statuses: ['Draft', 'Ordered', 'Receiving', 'Received', 'RequiresReview', 'Cancelled'],
  transientStatuses: ['Receiving'],
  api: {
    list: (filter) => purchaseOrdersApi.list(filter as DocumentFilter<PurchaseOrderStatus>),
    get: purchaseOrdersApi.get,
    create: (input: CreateDocumentInput) =>
      purchaseOrdersApi.create({
        branchId: input.branchId,
        supplierName: input.supplierName ?? '',
        notes: input.notes,
      } satisfies PurchaseOrderRequest),
    addLine: purchaseOrdersApi.addLine,
    updateLine: (id, lineId, quantity) => purchaseOrdersApi.updateLine(id, lineId, { quantity }),
    removeLine: purchaseOrdersApi.removeLine,
  },
};
