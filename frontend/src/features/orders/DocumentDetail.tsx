import { Alert, Button, Card, Divider, Grid, Group, Loader, Stack, Text, Title } from '@mantine/core';
import { IconArrowLeft, IconRefresh } from '@tabler/icons-react';
import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { ApiError } from '@/api/client';
import { EmptyState, Money, QueryState, StatusBadge } from '@/components';
import { useBranches } from '@/lib/branches';
import { formatDateTime, shortId } from '@/lib/format';
import { confirmAction } from '@/lib/notify';
import { AddLineForm } from './AddLineForm';
import { DocumentLines } from './DocumentLines';
import { DocumentTimeline, type TimelineEvent } from './DocumentTimeline';
import type { DocumentBase, DocumentKind } from './documents';
import { useDocument, useDocumentBusy, useLineMutations } from './useDocument';

interface DocumentDetailProps {
  kind: DocumentKind;
  id: string;
  /** Status-specific buttons; rendered as a component so it can own its mutations. */
  actions: (doc: DocumentBase, busy: boolean) => ReactNode;
  events: (doc: DocumentBase) => TimelineEvent[];
}

function DetailBody({ kind, doc, actions, events }: Omit<DocumentDetailProps, 'id'> & { doc: DocumentBase }) {
  const { nameOf } = useBranches();
  const { update, remove } = useLineMutations(kind, doc.id);
  const busy = useDocumentBusy(kind, doc.id);
  const draft = doc.status === 'Draft';
  const transient = kind.transientStatuses.includes(doc.status);

  return (
    <Grid gap="md">
      <Grid.Col span={{ base: 12, md: 8 }}>
        <Stack gap="md">
          {doc.reviewReason && (
            <Alert color="amber" variant="light" title="Requiere revisión manual">
              <Stack gap={4}>
                <Text size="sm">{doc.reviewReason}</Text>
                <Text size="sm">
                  El resultado en Inventory es incierto y el sistema no reintenta para no duplicar movimientos. Alguien debe conciliarlo
                  con los IDs de movimiento de cada línea y el historial de movimientos.
                </Text>
              </Stack>
            </Alert>
          )}
          {transient && (
            <Alert color="blue" variant="light" icon={<Loader size={16} />} title="Procesando">
              Se está registrando el movimiento en Inventory. Esta pantalla se actualiza sola.
            </Alert>
          )}
          <DocumentLines
            lines={doc.lines}
            editable={draft}
            busy={busy}
            onChangeQuantity={(line, quantity) => update.mutate({ lineId: line.id, quantity })}
            onRemove={(line) =>
              confirmAction({
                title: 'Quitar línea',
                message: `Se quitará ${line.productName} de esta ${kind.singular}. Puedes volver a agregarlo mientras sea borrador.`,
                confirmLabel: 'Quitar línea',
                destructive: true,
                onConfirm: () => remove.mutate(line.id),
              })
            }
          />
          {draft && <AddLineForm kind={kind} doc={doc} busy={busy} />}
        </Stack>
      </Grid.Col>
      <Grid.Col span={{ base: 12, md: 4 }}>
        <Stack gap="md">
          <Card withBorder padding="md">
            <Text size="sm" c="dimmed">Total</Text>
            <Text fz={28} fw={600} className="tabular" lh={1.2}><Money value={doc.total} /></Text>
            <Text size="xs" c="dimmed" mb="sm">{`${doc.lines.length} ${doc.lines.length === 1 ? 'línea' : 'líneas'}`}</Text>
            <Divider mb="sm" />
            <Stack gap={6} mb="sm">
              <Text size="sm">{nameOf(doc.branchId)}</Text>
              {doc.supplierName && <Text size="sm">{`Proveedor: ${doc.supplierName}`}</Text>}
              {doc.notes && (
                <Text size="sm" c="dimmed" style={{ whiteSpace: 'pre-wrap' }}>{doc.notes}</Text>
              )}
            </Stack>
            <Stack gap="xs">{actions(doc, busy)}</Stack>
          </Card>
          <Card withBorder padding="md">
            <Title order={2} size="h5" mb="sm">Historial</Title>
            <DocumentTimeline events={events(doc)} />
          </Card>
        </Stack>
      </Grid.Col>
    </Grid>
  );
}

/** Detail layout shared by sales and purchases: lines on the left, summary, actions and history on the right. */
export function DocumentDetail({ kind, id, actions, events }: DocumentDetailProps) {
  const query = useDocument(kind, id);
  const doc = query.data;
  const notFound = query.error instanceof ApiError && query.error.status === 404;

  const back = (
    <Button component={Link} to={kind.path} variant="subtle" color="gray" size="compact-sm" leftSection={<IconArrowLeft size={16} />}>
      {`Volver a ${kind.plural}`}
    </Button>
  );

  if (notFound) {
    return (
      <EmptyState
        title={`No encontramos esta ${kind.singular}`}
        action={back}
      />
    );
  }

  return (
    <>
      <Group mb="xs">{back}</Group>
      {doc && (
        <Group justify="space-between" align="center" mb="md" wrap="wrap" gap="sm">
          <Group gap="sm" align="center">
            <Title order={1}>{`${kind.title} ${shortId(doc.id)}`}</Title>
            <StatusBadge status={doc.status} size="md" />
          </Group>
          <Group gap="xs">
            <Text size="xs" c="dimmed">{`Creada ${formatDateTime(doc.createdAt)}`}</Text>
            <Button
              variant="subtle"
              size="compact-sm"
              color="gray"
              leftSection={<IconRefresh size={14} />}
              loading={query.isFetching}
              onClick={() => void query.refetch()}
            >
              Actualizar
            </Button>
          </Group>
        </Group>
      )}
      <QueryState isLoading={query.isLoading} error={query.error} onRetry={() => void query.refetch()}>
        {doc && <DetailBody kind={kind} doc={doc} actions={actions} events={events} />}
      </QueryState>
    </>
  );
}
