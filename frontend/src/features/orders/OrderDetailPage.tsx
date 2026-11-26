
import { useParams } from 'react-router-dom';
import { ordersApi } from '@/api/endpoints';
import { DocumentActionButton } from './DocumentActionButton';
import { DocumentDetail } from './DocumentDetail';
import { salesKind, type DocumentBase } from './documents';
import { useDocumentAction } from './useDocument';

function SaleActions({ doc, busy }: { doc: DocumentBase; busy: boolean }) {
  const confirm = useDocumentAction(salesKind, doc.id, {
    run: ordersApi.confirm,
    success: 'Venta confirmada',
    touchesStock: true,
  });
  const cancel = useDocumentAction(salesKind, doc.id, { run: ordersApi.cancel, success: 'Venta cancelada' });

  // Only Draft can move; a Confirmed sale shows a disabled cancel that explains why.
  if (doc.status !== 'Draft' && doc.status !== 'Confirmed') return null;
  const draft = doc.status === 'Draft';

  return (
    <>
      {draft && (
        <DocumentActionButton
          ownerOnly
          label="Confirmar venta"
          mutation={{ mutate: () => confirm.mutate(), isPending: confirm.isPending }}
          busy={busy}
          disabledReason={doc.lines.length === 0 ? 'Agrega al menos un producto para confirmar.' : undefined}
          confirm={{
            title: 'Confirmar venta',
            message:
              'Se descontará del stock de la sucursal la cantidad de cada línea y la venta ya no podrá editarse ni cancelarse. Si no hay stock suficiente, volverá a borrador.',
          }}
        />
      )}
      {/* Only drafts can be cancelled; other states have no available action. */}
      {draft && (
        <DocumentActionButton
          ownerOnly
          color="red"
          variant="light"
          label="Cancelar venta"
          mutation={{ mutate: () => cancel.mutate(), isPending: cancel.isPending }}
          busy={busy}
          confirm={{
            title: 'Cancelar venta',
            message: 'La venta quedará cancelada y no podrá reabrirse. No se ha descontado stock.',
            destructive: true,
          }}
        />
      )}
    </>
  );
}

export function OrderDetailPage() {
  const { id = '' } = useParams();
  return (
    <DocumentDetail
      kind={salesKind}
      id={id}
      actions={(doc, busy) => <SaleActions doc={doc} busy={busy} />}
      events={(doc) => [
        { label: 'Creada', date: doc.createdAt },
        { label: 'Confirmada', date: doc.confirmedAt ?? null },
        { label: 'Cancelada', date: doc.cancelledAt ?? null, color: 'red' },
      ]}
    />
  );
}
