import { Badge } from '@mantine/core';

const STATUS: Record<string, { label: string; color: string }> = {
  Draft: { label: 'Borrador', color: 'gray' },
  Confirming: { label: 'Confirmando', color: 'blue' },
  Confirmed: { label: 'Confirmada', color: 'pine' },
  Ordered: { label: 'Enviada al proveedor', color: 'blue' },
  Receiving: { label: 'Recibiendo', color: 'blue' },
  Received: { label: 'Recibida', color: 'pine' },
  RequiresReview: { label: 'Requiere revisión', color: 'amber' },
  Cancelled: { label: 'Cancelada', color: 'red' },
  Pending: { label: 'Pendiente', color: 'gray' },
  Sent: { label: 'Enviada', color: 'pine' },
  Failed: { label: 'Fallida', color: 'red' },
  Receipt: { label: 'Entrada', color: 'pine' },
  Issue: { label: 'Salida', color: 'red' },
  Adjustment: { label: 'Ajuste', color: 'blue' },
  LowStock: { label: 'Stock bajo', color: 'amber' },
  OrderCreated: { label: 'Venta creada', color: 'blue' },
};

export const statusLabel = (status: string) => STATUS[status]?.label ?? status;

export function StatusBadge({ status, size = 'sm' }: { status: string; size?: 'xs' | 'sm' | 'md' }) {
  const config = STATUS[status] ?? { label: status, color: 'gray' };
  return (
    <Badge color={config.color} size={size} variant="light">
      {config.label}
    </Badge>
  );
}
