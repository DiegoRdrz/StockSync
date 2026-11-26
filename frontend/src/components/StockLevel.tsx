import { Box, Group, Text, Tooltip } from '@mantine/core';
import { formatNumber } from '@/lib/format';
import classes from './StockLevel.module.css';

interface StockLevelProps {
  quantity: number;
  minimum: number;
  /** Compact renders only the bar and the number (for tables). */
  compact?: boolean;
}

/**
 * Shelf-level meter: the bar fills against twice the minimum, with a tick at the
 * minimum. Colour and the text label both encode the state (never colour alone).
 */
export function StockLevel({ quantity, minimum, compact }: StockLevelProps) {
  const scale = Math.max(minimum * 2, quantity, 1);
  const fill = Math.min(100, (quantity / scale) * 100);
  const tick = minimum > 0 ? (minimum / scale) * 100 : null;
  const state = quantity <= 0 ? 'empty' : minimum > 0 && quantity < minimum ? 'low' : 'ok';
  // Only exceptions get a label; a healthy level needs no words.
  const label = state === 'empty' ? 'Agotado' : state === 'low' ? `Faltan ${formatNumber(minimum - quantity)}` : '';

  return (
    <Tooltip label={`${formatNumber(quantity)} en estantería, mínimo ${formatNumber(minimum)}`}>
      <Group gap="xs" wrap="nowrap" miw={compact ? 120 : 180}>
        <Box className={classes.track} data-state={state} role="meter" aria-valuenow={quantity} aria-valuemin={0} aria-valuemax={scale} aria-label="Nivel de stock">
          <Box className={classes.fill} style={{ width: `${fill}%` }} />
          {tick !== null && <Box className={classes.tick} style={{ left: `${tick}%` }} />}
        </Box>
        <Text size="sm" fw={600} className="tabular" w={compact ? 36 : 44} ta="right">
          {formatNumber(quantity)}
        </Text>
        {!compact && (
          <Text size="xs" c={state === 'low' ? 'amber.8' : 'red.7'} fw={600} miw={78}>
            {label}
          </Text>
        )}
      </Group>
    </Tooltip>
  );
}
