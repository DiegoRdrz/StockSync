import { DocumentListPage } from './DocumentListPage';
import { purchasesKind } from './documents';

export function PurchaseOrdersPage() {
  return (
    <DocumentListPage
      kind={purchasesKind}
      title="Compras"
    />
  );
}
