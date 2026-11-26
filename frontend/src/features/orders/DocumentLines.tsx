import { ActionIcon, Badge, Group, NumberInput, Paper, Table, Text, Tooltip, VisuallyHidden } from '@mantine/core';
import { IconCheck, IconTrash } from '@tabler/icons-react';
import { useState } from 'react';
import type { LineResponse } from '@/api/types';
import { EmptyState, Money } from '@/components';
import { formatNumber, shortId } from '@/lib/format';

const STOCK_STATUS: Record<string, { label: string; color: string }> = {
  Pending: { label: 'Sin movimiento', color: 'gray' },
  Applied: { label: 'Aplicado', color: 'pine' },
  Compensated: { label: 'Revertido', color: 'amber' },
  '1': { label: 'Sin movimiento', color: 'gray' },
  '2': { label: 'Aplicado', color: 'pine' },
  '3': { label: 'Revertido', color: 'amber' },
};

function MovementId({ label, id }: { label: string; id: string | null }) {
  if (!id) return null;
  return (
    <Tooltip label={`${label}: ${id}`}>
      <Text size="xs" c="dimmed" ff="monospace" tabIndex={0} aria-label={`${label} ${id}`}>
        {`${label} ${shortId(id)}`}
      </Text>
    </Tooltip>
  );
}

interface LineRowProps {
  line: LineResponse;
  editable: boolean;
  busy: boolean;
  onChangeQuantity: (line: LineResponse, quantity: number) => void;
  onRemove: (line: LineResponse) => void;
}

function LineRow({ line, editable, busy, onChangeQuantity, onRemove }: LineRowProps) {
  const [draft, setDraft] = useState<number | string>(line.quantity);
  const draftQty = typeof draft === 'number' ? draft : Number(draft);
  const changed = draftQty !== line.quantity;
  const valid = Number.isInteger(draftQty) && draftQty > 0;
  const stock = STOCK_STATUS[line.stockStatus] ?? { label: line.stockStatus, color: 'gray' };

  return (
    <Table.Tr>
      <Table.Td className="tabular" c="dimmed">{line.lineNumber}</Table.Td>
      <Table.Td miw={160}>
        <Text size="sm" fw={500} lineClamp={2}>{line.productName}</Text>
        <Text size="xs" c="dimmed">{line.productSku}</Text>
      </Table.Td>
      <Table.Td ta="right">
        {editable ? (
          <Group gap={4} justify="flex-end" wrap="nowrap">
            <NumberInput
              size="xs"
              w={84}
              min={1}
              allowDecimal={false}
              allowNegative={false}
              clampBehavior="strict"
              aria-label={`Cantidad de ${line.productName}`}
              value={draft}
              onChange={setDraft}
              error={changed && !valid}
              disabled={busy}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && changed && valid) onChangeQuantity(line, draftQty);
              }}
            />
            <Tooltip label="Guardar cantidad">
              <ActionIcon
                variant="light"
                aria-label={`Guardar cantidad de ${line.productName}`}
                disabled={!changed || !valid || busy}
                onClick={() => onChangeQuantity(line, draftQty)}
              >
                <IconCheck size={16} />
              </ActionIcon>
            </Tooltip>
          </Group>
        ) : (
          <span className="tabular">{formatNumber(line.quantity)}</span>
        )}
      </Table.Td>
      <Table.Td ta="right"><Money value={line.unitPrice} /></Table.Td>
      <Table.Td ta="right"><Money value={line.lineTotal} fw={600} /></Table.Td>
      {editable ? (
        <Table.Td w={48}>
          <Tooltip label="Quitar línea">
            <ActionIcon
              color="red"
              variant="subtle"
              aria-label={`Quitar ${line.productName}`}
              disabled={busy}
              onClick={() => onRemove(line)}
            >
              <IconTrash size={16} />
            </ActionIcon>
          </Tooltip>
        </Table.Td>
      ) : (
        <Table.Td miw={150}>
          <Badge size="sm" variant="light" color={stock.color}>{stock.label}</Badge>
          <MovementId label="Movimiento" id={line.stockMovementId} />
          <MovementId label="Reversión" id={line.compensationMovementId} />
        </Table.Td>
      )}
    </Table.Tr>
  );
}

interface DocumentLinesProps {
  lines: LineResponse[];
  /** Draft: quantities can change and lines can be removed. */
  editable: boolean;
  busy: boolean;
  onChangeQuantity: (line: LineResponse, quantity: number) => void;
  onRemove: (line: LineResponse) => void;
}

export function DocumentLines({ lines, editable, busy, ...handlers }: DocumentLinesProps) {
  if (lines.length === 0) {
    return (
      <Paper withBorder>
        <EmptyState
          title="Aún no hay productos"
        />
      </Paper>
    );
  }
  return (
    <Paper withBorder>
      <Table.ScrollContainer minWidth={560}>
        <Table verticalSpacing={6} horizontalSpacing="sm" stickyHeader aria-label="Líneas del documento">
          <Table.Thead>
            <Table.Tr>
              <Table.Th w={40}>N.º</Table.Th>
              <Table.Th>Producto</Table.Th>
              <Table.Th ta="right">Cantidad</Table.Th>
              <Table.Th ta="right">Precio unitario</Table.Th>
              <Table.Th ta="right">Total línea</Table.Th>
              <Table.Th>{editable ? <VisuallyHidden>Acciones</VisuallyHidden> : 'Stock'}</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {lines.map((line) => (
              // Remount when the server quantity changes so the draft never goes stale.
              <LineRow key={`${line.id}-${line.quantity}`} line={line} editable={editable} busy={busy} {...handlers} />
            ))}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
    </Paper>
  );
}
