import { DocumentListPage } from './DocumentListPage';
import { salesKind } from './documents';

export function OrdersPage() {
  return (
    <DocumentListPage
      kind={salesKind}
      title="Ventas"
    />
  );
}
