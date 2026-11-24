import { Button, Group, Modal, Paper, SegmentedControl, Select, Stack, Table, Text, TextInput, VisuallyHidden } from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconPencil, IconPlus, IconTrash } from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { recipientsApi } from '@/api/endpoints';
import { ApiError } from '@/api/client';
import type { NotificationChannel, NotificationRecipientResponse } from '@/api/types';
import { EmptyState, OwnerAction, PageHeader, QueryState, RowAction, TablePager, TableSkeleton } from '@/components';
import { confirmAction, notifyError, notifySuccess } from '@/lib/notify';
import { emailError, requiredError } from '@/lib/validation';
import { formatDate } from '@/lib/format';
import { ChannelLabel } from './NotificationsPage';
import { useClampPage } from '@/lib/useClampPage';

const E164 = /^\+[1-9]\d{7,14}$/;

function destinationError(channel: NotificationChannel, value: string): string | null {
  const v = value.trim();
  if (!v) return 'El destino es obligatorio';
  if (channel === 'Email') return emailError(v);
  return E164.test(v) ? null : 'Usa formato internacional, por ejemplo +573001234567';
}

interface FormValues { name: string; channel: NotificationChannel; destination: string }

function RecipientModal({ recipient, opened, onClose }: { recipient: NotificationRecipientResponse | null; opened: boolean; onClose: () => void }) {
  const queryClient = useQueryClient();
  const form = useForm<FormValues>({
    mode: 'controlled',
    validateInputOnBlur: true,
    initialValues: { name: recipient?.name ?? '', channel: recipient?.channel ?? 'Email', destination: recipient?.destination ?? '' },
    validate: {
      name: requiredError('El nombre'),
      destination: (v, values) => destinationError(values.channel, v),
    },
  });

  const save = useMutation({
    mutationFn: (v: FormValues) => {
      const body = { name: v.name.trim(), channel: v.channel, destination: v.destination.trim() };
      return recipient ? recipientsApi.update(recipient.id, body) : recipientsApi.create(body);
    },
    onSuccess: () => {
      notifySuccess(recipient ? 'Destinatario guardado' : 'Destinatario agregado');
      queryClient.invalidateQueries({ queryKey: ['recipients'] });
      onClose();
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 400) form.setErrors(error.formErrors());
      else notifyError(error);
    },
  });

  return (
    <Modal opened={opened} onClose={onClose} title={<Text fw={650}>{recipient ? 'Editar destinatario' : 'Agregar destinatario'}</Text>} closeOnClickOutside={!save.isPending}>
      <form onSubmit={form.onSubmit((v) => save.mutate(v))} noValidate>
        <Stack gap="sm">
          <TextInput label="Nombre" placeholder="Ej. Ana Gómez" data-autofocus {...form.getInputProps('name')} />
          <div>
            <Text size="sm" fw={500} mb={4} id="channel-label">Canal</Text>
            <SegmentedControl
              fullWidth
              aria-labelledby="channel-label"
              data={[{ value: 'Email', label: 'Correo' }, { value: 'WhatsApp', label: 'WhatsApp' }]}
              {...form.getInputProps('channel')}
              onChange={(v) => { form.setFieldValue('channel', v as NotificationChannel); form.clearFieldError('destination'); }}
            />
          </div>
          <TextInput
            label={form.values.channel === 'Email' ? 'Correo electrónico' : 'Número de WhatsApp'}
            placeholder={form.values.channel === 'Email' ? 'ana@mitienda.co' : '+573001234567'}
            description={form.values.channel === 'Email' ? undefined : 'Con indicativo y sin espacios, p. ej. +573001234567'}
            inputMode={form.values.channel === 'Email' ? 'email' : 'tel'}
            {...form.getInputProps('destination')}
          />
          <Group justify="flex-end" mt="xs">
            <Button variant="default" onClick={onClose} disabled={save.isPending}>Cancelar</Button>
            <Button type="submit" loading={save.isPending}>{recipient ? 'Guardar cambios' : 'Agregar destinatario'}</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

export function RecipientsPage() {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [channel, setChannel] = useState('');
  const [editing, setEditing] = useState<NotificationRecipientResponse | null>(null);
  const [creating, setCreating] = useState(false);

  const filter = { page, pageSize: 15, channel: (channel || undefined) as NotificationChannel | undefined };
  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['recipients', filter],
    queryFn: () => recipientsApi.list(filter),
    placeholderData: (prev) => prev,
  });

  const remove = useMutation({
    mutationFn: (id: string) => recipientsApi.remove(id),
    onSuccess: () => {
      notifySuccess('Destinatario eliminado');
      queryClient.invalidateQueries({ queryKey: ['recipients'] });
    },
    onError: (e) => notifyError(e),
  });

  const addButton = (
    <OwnerAction>
      <Button leftSection={<IconPlus size={16} />} onClick={() => setCreating(true)}>Agregar destinatario</Button>
    </OwnerAction>
  );

  useClampPage(page, data?.totalPages, setPage);

  return (
    <>
      <PageHeader
        title="Destinatarios"
        actions={addButton}
      />

      <Group mb="md">
        <Select
          aria-label="Filtrar por canal"
          data={[{ value: '', label: 'Todos los canales' }, { value: 'Email', label: 'Correo' }, { value: 'WhatsApp', label: 'WhatsApp' }]}
          value={channel}
          onChange={(v) => { setChannel(v ?? ''); setPage(1); }}
          w={{ base: '100%', xs: 200 }}
        />
      </Group>

      <Paper>
        <QueryState
          isLoading={isLoading}
          error={error}
          onRetry={refetch}
          isEmpty={(data?.items.length ?? 0) === 0}
          skeleton={<TableSkeleton rows={5} />}
          empty={
            channel ? (
              <EmptyState title="Sin resultados" />
            ) : (
              <EmptyState
                title="Agrega tu primer destinatario"
                description="Recibirán alertas de stock bajo y de ventas nuevas."
                action={addButton}
              />
            )
          }
        >
          <Table.ScrollContainer minWidth={560}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Nombre</Table.Th>
                  <Table.Th>Canal</Table.Th>
                  <Table.Th>Destino</Table.Th>
                  <Table.Th>Agregado</Table.Th>
                  <Table.Th ta="right" w={96}><VisuallyHidden>Acciones</VisuallyHidden></Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {data?.items.map((r) => (
                  <Table.Tr key={r.id}>
                    <Table.Td><Text size="sm" fw={500}>{r.name}</Text></Table.Td>
                    <Table.Td><ChannelLabel channel={r.channel} /></Table.Td>
                    <Table.Td maw={260}><Text size="sm" truncate>{r.destination}</Text></Table.Td>
                    <Table.Td className="tabular">{formatDate(r.createdAt)}</Table.Td>
                    <Table.Td>
                      <Group gap={4} justify="flex-end" wrap="nowrap">
                        <RowAction ownerOnly size="md" label="Editar" ariaLabel={`Editar a ${r.name}`} onClick={() => setEditing(r)}>
                          <IconPencil size={16} />
                        </RowAction>
                        <RowAction
                          ownerOnly
                          size="md"
                          color="red"
                          label="Eliminar"
                          ariaLabel={`Eliminar a ${r.name}`}
                          onClick={() =>
                            confirmAction({
                              title: 'Eliminar destinatario',
                              message: `${r.name} dejará de recibir alertas en ${r.destination}. Esta acción no se puede deshacer.`,
                              confirmLabel: 'Eliminar destinatario',
                              destructive: true,
                              onConfirm: () => remove.mutate(r.id),
                            })
                          }
                        >
                          <IconTrash size={16} />
                        </RowAction>
                      </Group>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          <TablePager page={page} totalPages={data?.totalPages ?? 0} total={data?.total} onChange={setPage} noun="destinatarios" />
        </QueryState>
      </Paper>

      {creating && <RecipientModal recipient={null} opened onClose={() => setCreating(false)} />}
      {editing && <RecipientModal key={editing.id} recipient={editing} opened onClose={() => setEditing(null)} />}
    </>
  );
}
