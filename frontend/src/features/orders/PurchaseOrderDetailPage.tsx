
import { useParams } from 'react-router-dom';
import { purchaseOrdersApi } from '@/api/endpoints';
import { DocumentActionButton } from './DocumentActionButton';
import { DocumentDetail } from './DocumentDetail';
import { purchasesKind, type DocumentBase } from './documents';
import { useDocumentAction } from './useDocument';

function PurchaseActions({ doc, busy }: { doc: DocumentBase; busy: boolean }) {
  const place = useDocumentAction(purchasesKind, doc.id, { run: purchaseOrdersApi.place, success: 'Compra enviada al proveedor' });
  const receive = useDocumentAction(purchasesKind, doc.id, {
    run: purchaseOrdersApi.receive,
    success: 'Mercancía recibida',
    touchesStock: true,
  });
  const cancel = useDocumentAction(purchasesKind, doc.id, { run: purchaseOrdersApi.cancel, success: 'Compra cancelada' });

  const draft = doc.status === 'Draft';
  const ordered = doc.status === 'Ordered';
  if (!draft && !ordered) return null;

  return (
    <>
      {draft && (
        <DocumentActionButton
          label="Enviar al proveedor"
          mutation={{ mutate: () => place.mutate(), isPending: place.isPending }}
          busy={busy}
          disabledReason={doc.lines.length === 0 ? 'Agrega al menos un producto para enviar.' : undefined}
          confirm={{
            title: 'Enviar al proveedor',
            message: 'La compra pasará a "Enviada al proveedor" y sus líneas ya no podrán editarse.',
          }}
        />
      )}
      {ordered && (
        <DocumentActionButton
          label="Recibir mercancía"
          mutation={{ mutate: () => receive.mutate(), isPending: receive.isPending }}
          busy={busy}
          confirm={{
            title: 'Recibir mercancía',
            message: 'Se registrará una entrada de stock por cada línea en la sucursal. No se puede deshacer desde aquí.',
          }}
        />
      )}
      <DocumentActionButton
        color="red"
        variant="light"
        label="Cancelar compra"
        mutation={{ mutate: () => cancel.mutate(), isPending: cancel.isPending }}
        busy={busy}
        confirm={{
          title: 'Cancelar compra',
          message: 'La compra quedará cancelada y no podrá reabrirse. No se ha ingresado stock.',
          destructive: true,
        }}
      />
    </>
  );
}

export function PurchaseOrderDetailPage() {
  const { id = '' } = useParams();
  return (
    <DocumentDetail
      kind={purchasesKind}
      id={id}
      actions={(doc, busy) => <PurchaseActions doc={doc} busy={busy} />}
      events={(doc) => [
        { label: 'Creada', date: doc.createdAt },
        { label: 'Enviada al proveedor', date: doc.orderedAt ?? null },
        { label: 'Recibida', date: doc.receivedAt ?? null },
        { label: 'Cancelada', date: doc.cancelledAt ?? null, color: 'red' },
      ]}
    />
  );
}
