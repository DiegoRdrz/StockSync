import { Text } from '@mantine/core';
import type { MovementType } from '@/api/types';
import { formatNumber } from '@/lib/format';

/** Signed change of a movement: + receipt, − issue, adjustment = newQuantity − previousQuantity. */
export function movementDelta(m: { type: MovementType; quantity: number; previousQuantity: number; newQuantity: number }) {
  if (m.type === 'Receipt') return m.quantity;
  if (m.type === 'Issue') return -m.quantity;
  return m.newQuantity - m.previousQuantity;
}

export function MovementAmount({ delta }: { delta: number }) {
  const sign = delta > 0 ? '+' : delta < 0 ? '−' : '';
  return (
    <Text size="sm" fw={600} className="tabular" c={delta > 0 ? 'pine.7' : delta < 0 ? 'red.7' : 'dimmed'}>
      {sign}
      {formatNumber(Math.abs(delta))}
    </Text>
  );
}
