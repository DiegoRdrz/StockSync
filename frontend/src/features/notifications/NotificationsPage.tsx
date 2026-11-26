import { Alert, Anchor, Badge, Box, Drawer, Group, Paper, Select, Stack, Table, Text, Tooltip } from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import { IconBrandWhatsapp, IconCalendar, IconMail } from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { notificationsApi } from '@/api/endpoints';
import type { NotificationChannel, NotificationEventType, NotificationResponse, NotificationStatus } from '@/api/types';
import { BranchSelect, EmptyState, PageHeader, QueryState, StatusBadge, TablePager, TableSkeleton } from '@/components';
import { useBranches } from '@/lib/branches';
import { dayEndUtc, dayStartUtc, formatDateTime, toIsoDay } from '@/lib/format';

export function ChannelLabel({ channel }: { channel: NotificationChannel }) {
  const Icon = channel === 'Email' ? IconMail : IconBrandWhatsapp;
  return (
    <Group gap={6} wrap="nowrap">
      <Icon size={16} aria-hidden />
      <Text size="sm" component="span">{channel === 'Email' ? 'Correo' : 'WhatsApp'}</Text>
    </Group>
  );
}

const CHANNELS = [{ value: '', label: 'Todos los canales' }, { value: 'Email', label: 'Correo' }, { value: 'WhatsApp', label: 'WhatsApp' }];
const EVENTS = [{ value: '', label: 'Todos los eventos' }, { value: 'LowStock', label: 'Stock bajo' }, { value: 'OrderCreated', label: 'Venta creada' }];
const STATUSES = [
  { value: '', label: 'Todos los estados' },
  { value: 'Pending', label: 'Pendiente' },
  { value: 'Sent', label: 'Enviada' },
  { value: 'Failed', label: 'Fallida' },
];

export function NotificationsPage() {
  const { nameOf } = useBranches();
  const [page, setPage] = useState(1);
  const [channel, setChannel] = useState('');
  const [eventType, setEventType] = useState('');
  const [status, setStatus] = useState('');
  const [branchId, setBranchId] = useState('');
  const [range, setRange] = useState<[string | null, string | null]>([null, null]);
  const [selected, setSelected] = useState<NotificationResponse | null>(null);

  const filter = {
    page,
    pageSize: 15,
    channel: (channel || undefined) as NotificationChannel | undefined,
    eventType: (eventType || undefined) as NotificationEventType | undefined,
    status: (status || undefined) as NotificationStatus | undefined,
    branchId: branchId || undefined,
    from: dayStartUtc(range[0]),
    to: dayEndUtc(range[1]),
  };
  const { data, isLoading, error, refetch, isPlaceholderData } = useQuery({
    queryKey: ['notifications', filter],
    queryFn: () => notificationsApi.list(filter),
    placeholderData: (prev) => prev,
  });

  const reset = <T,>(set: (v: T) => void) => (v: T) => {
    set(v);
    setPage(1);
  };
  const hasFilters = !!(channel || eventType || status || branchId || range[0]);

  return (
    <>
      <PageHeader
        title="Notificaciones"
        actions={<Anchor component={Link} to="/notifications/recipients" size="sm">Administrar destinatarios</Anchor>}
      />

      <Paper p="sm" mb="md">
        <Group gap="sm" align="flex-end" wrap="wrap">
          <Select label="Canal" data={CHANNELS} value={channel} onChange={(v) => reset(setChannel)(v ?? '')} w={{ base: '100%', xs: 170 }} />
          <Select label="Evento" data={EVENTS} value={eventType} onChange={(v) => reset(setEventType)(v ?? '')} w={{ base: '100%', xs: 170 }} />
          <Select label="Estado" data={STATUSES} value={status} onChange={(v) => reset(setStatus)(v ?? '')} w={{ base: '100%', xs: 170 }} />
          <BranchSelect withAll label="Sucursal" value={branchId} onChange={(v) => reset(setBranchId)(v ?? '')} w={{ base: '100%', xs: 200 }} />
          <DatePickerInput
            type="range"
            label="Fechas"
            valueFormat="D MMM YYYY"
            placeholder="Todas las fechas"
            clearable
            leftSection={<IconCalendar size={16} />}
            value={range}
            onChange={(v) => reset(setRange)(v as [string | null, string | null])}
            maxDate={toIsoDay(new Date())}
            w={{ base: '100%', xs: 240 }}
          />
        </Group>
      </Paper>

      <Paper style={{ opacity: isPlaceholderData ? 0.6 : 1 }}>
        <QueryState
          isLoading={isLoading}
          error={error}
          onRetry={refetch}
          isEmpty={(data?.items.length ?? 0) === 0}
          skeleton={<TableSkeleton rows={8} />}
          empty={
            <EmptyState
              title={hasFilters ? 'Ninguna notificación coincide' : 'Aún no se han enviado notificaciones'}
              description={hasFilters ? undefined : 'Se generan cuando un producto baja de su mínimo o se crea una venta.'}
            />
          }
        >
          <Table.ScrollContainer minWidth={760}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Fecha</Table.Th>
                  <Table.Th>Evento</Table.Th>
                  <Table.Th>Canal</Table.Th>
                  <Table.Th>Destinatario</Table.Th>
                  <Table.Th>Asunto</Table.Th>
                  <Table.Th>Estado</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {data?.items.map((n) => (
                  <Table.Tr key={n.id} onClick={() => setSelected(n)} style={{ cursor: 'pointer' }}>
                    <Table.Td className="tabular" style={{ whiteSpace: 'nowrap' }}>{formatDateTime(n.createdAt)}</Table.Td>
                    <Table.Td><StatusBadge status={n.eventType} size="xs" /></Table.Td>
                    <Table.Td><ChannelLabel channel={n.channel} /></Table.Td>
                    <Table.Td maw={200}><Text size="sm" truncate>{n.recipient}</Text></Table.Td>
                    <Table.Td maw={260}>
                      <Tooltip label={n.subject} multiline maw={360}>
                        <Anchor
                          component="button"
                          type="button"
                          size="sm"
                          c="inherit"
                          onClick={(e: React.MouseEvent) => { e.stopPropagation(); setSelected(n); }}
                          style={{ maxWidth: '100%', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', display: 'block', textAlign: 'left' }}
                        >
                          {n.subject}
                        </Anchor>
                      </Tooltip>
                    </Table.Td>
                    <Table.Td><StatusBadge status={n.status} size="xs" /></Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          <TablePager page={page} totalPages={data?.totalPages ?? 0} total={data?.total} onChange={setPage} noun="notificaciones" singular="notificación" />
        </QueryState>
      </Paper>

      <Drawer opened={!!selected} onClose={() => setSelected(null)} position="right" size="md" title={<Text fw={650}>Detalle de la notificación</Text>}>
        {selected && (
          <Stack gap="md">
            <Group gap="xs">
              <StatusBadge status={selected.eventType} />
              <StatusBadge status={selected.status} />
              <Badge color="gray" leftSection={selected.channel === 'Email' ? <IconMail size={12} /> : <IconBrandWhatsapp size={12} />}>
                {selected.channel === 'Email' ? 'Correo' : 'WhatsApp'}
              </Badge>
            </Group>
            {selected.status === 'Failed' && (
              <Alert color="red" variant="light" title="No se pudo enviar">
                {selected.failureReason ?? 'El proveedor no informó el motivo.'}
              </Alert>
            )}
            <Box>
              <Text size="xs" c="dimmed">Asunto</Text>
              <Text fw={600}>{selected.subject}</Text>
            </Box>
            <Box>
              <Text size="xs" c="dimmed">Mensaje</Text>
              <Paper p="sm" bg="var(--mantine-color-gray-0)">
                <Text size="sm" style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>{selected.body}</Text>
              </Paper>
            </Box>
            <Group grow align="flex-start">
              <Box><Text size="xs" c="dimmed">Destinatario</Text><Text size="sm" style={{ wordBreak: 'break-all' }}>{selected.recipient}</Text></Box>
              <Box><Text size="xs" c="dimmed">Sucursal</Text><Text size="sm">{nameOf(selected.branchId)}</Text></Box>
            </Group>
            <Group grow align="flex-start">
              <Box><Text size="xs" c="dimmed">Creada</Text><Text size="sm" className="tabular">{formatDateTime(selected.createdAt)}</Text></Box>
              <Box><Text size="xs" c="dimmed">Enviada</Text><Text size="sm" className="tabular">{selected.sentAt ? formatDateTime(selected.sentAt) : 'Aún no enviada'}</Text></Box>
            </Group>
          </Stack>
        )}
      </Drawer>
    </>
  );
}
