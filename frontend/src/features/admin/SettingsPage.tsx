import { ActionIcon, Button, CopyButton, Group, Paper, PasswordInput, Skeleton, Stack, Table, Text, TextInput, Title, Tooltip } from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconCheck, IconCopy, IconTrash } from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState, type ReactNode } from 'react';
import { ApiError } from '@/api/client';
import { tenantsApi, usersApi } from '@/api/endpoints';
import { useAuth } from '@/app/auth';
import { EmptyState, ErrorState, OwnerAction, PageHeader } from '@/components';
import { isGuid, useBranches } from '@/lib/branches';
import { confirmAction, notifyError, notifySuccess } from '@/lib/notify';
import { passwordError, requiredError } from '@/lib/validation';

function Section({ title, description, children }: { title: string; description?: string; children: ReactNode }) {
  return (
    <Paper p="md" component="section" aria-label={title}>
      <Title order={2} mb={2}>{title}</Title>
      {description && <Text size="sm" c="dimmed" mb="md" maw={640}>{description}</Text>}
      {!description && <div style={{ height: 12 }} />}
      {children}
    </Paper>
  );
}

function CompanySection() {
  const { isOwner, session, updateSession } = useAuth();
  const queryClient = useQueryClient();
  const tenant = useQuery({ queryKey: ['tenant'], queryFn: tenantsApi.current, enabled: isOwner });
  const form = useForm({
    validateInputOnBlur: true,
    initialValues: { name: '', taxId: '' },
    validate: { name: requiredError('El nombre de la empresa') },
  });
  const { setValues, resetDirty } = form;

  useEffect(() => {
    if (tenant.data) {
      const values = { name: tenant.data.name, taxId: tenant.data.taxId ?? '' };
      setValues(values);
      resetDirty(values);
    }
  }, [tenant.data, setValues, resetDirty]);

  const save = useMutation({
    mutationFn: (v: { name: string; taxId: string }) => tenantsApi.update({ name: v.name.trim(), taxId: v.taxId.trim() || null }),
    onSuccess: (updated) => {
      notifySuccess('Datos de la empresa guardados');
      updateSession({ tenant: updated });
      queryClient.setQueryData(['tenant'], updated);
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 400) form.setErrors(error.formErrors());
      else notifyError(error);
    },
  });

  const tenantId = session?.tenant.id ?? '';

  return (
    <Section title="Empresa">
      {isOwner && tenant.isLoading ? (
        <Stack gap="sm"><Skeleton h={36} /><Skeleton h={36} /></Stack>
      ) : isOwner && tenant.error ? (
        <ErrorState error={tenant.error} onRetry={tenant.refetch} />
      ) : (
        <form onSubmit={form.onSubmit((v) => save.mutate(v))} noValidate>
          <Stack gap="sm" maw={460}>
            {isOwner ? (
              <>
                <TextInput label="Nombre de la empresa" {...form.getInputProps('name')} />
                <TextInput label="NIT" description="Opcional" {...form.getInputProps('taxId')} />
              </>
            ) : (
              <>
                <TextInput label="Nombre de la empresa" value={session?.tenant.name ?? ''} readOnly />
                <TextInput label="NIT" value={session?.tenant.taxId ?? 'Sin NIT'} readOnly />
              </>
            )}
            <TextInput
              label="ID de la empresa"
              value={tenantId}
              readOnly
              rightSection={
                <CopyButton value={tenantId}>
                  {({ copied, copy }) => (
                    <Tooltip label={copied ? 'Copiado' : 'Copiar ID'}>
                      <ActionIcon variant="subtle" color={copied ? 'pine' : 'gray'} onClick={copy} aria-label="Copiar ID de la empresa">
                        {copied ? <IconCheck size={16} /> : <IconCopy size={16} />}
                      </ActionIcon>
                    </Tooltip>
                  )}
                </CopyButton>
              }
            />
            <Group justify="flex-end">
              <OwnerAction>
                <Button type="submit" loading={save.isPending} disabled={!form.isDirty()}>Guardar cambios</Button>
              </OwnerAction>
            </Group>
          </Stack>
        </form>
      )}
    </Section>
  );
}

function BranchesSection() {
  const { branches, add, remove } = useBranches();
  const form = useForm({
    validateInputOnBlur: true,
    initialValues: { name: '', id: '' },
    validate: {
      name: requiredError('El nombre'),
      id: (v) => (!v.trim() ? 'El ID es obligatorio' : !isGuid(v) ? 'Debe ser un GUID, por ejemplo 33333333-3333-3333-3333-333333333333' : null),
    },
  });

  return (
    <Section
      title="Sucursales"
      description="Aún no existe un servicio de sucursales: aquí das nombre a cada ID. Se guarda solo en este navegador."
    >
      <Stack gap="md">
        {branches.length === 0 ? (
          <EmptyState title="Sin sucursales" description="Agrega una con su nombre y su ID." />
        ) : (
          <Table.ScrollContainer minWidth={480}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Nombre</Table.Th>
                  <Table.Th>ID</Table.Th>
                  <Table.Th w={56} />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {branches.map((b) => (
                  <Table.Tr key={b.id}>
                    <Table.Td><Text size="sm" fw={500}>{b.name}</Text></Table.Td>
                    <Table.Td><Text size="xs" c="dimmed" ff="monospace" className="tabular">{b.id}</Text></Table.Td>
                    <Table.Td>
                      <Tooltip label="Quitar de la lista">
                        <ActionIcon
                          variant="subtle"
                          color="red"
                          aria-label={`Quitar ${b.name}`}
                          onClick={() =>
                            confirmAction({
                              title: 'Quitar sucursal',
                              message: `${b.name} dejará de mostrarse con su nombre en este navegador. Los datos de la sucursal no se borran.`,
                              confirmLabel: 'Quitar sucursal',
                              destructive: true,
                              onConfirm: () => remove(b.id),
                            })
                          }
                        >
                          <IconTrash size={16} />
                        </ActionIcon>
                      </Tooltip>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}

        <form
          noValidate
          onSubmit={form.onSubmit((v) => {
            add({ id: v.id.trim().toLowerCase(), name: v.name.trim() });
            notifySuccess('Sucursal agregada');
            form.reset();
          })}
        >
          <Group align="flex-start" gap="sm" wrap="wrap">
            <TextInput label="Nombre" placeholder="Sucursal Sur" w={{ base: '100%', sm: 200 }} {...form.getInputProps('name')} />
            <TextInput label="ID (GUID)" placeholder="33333333-3333-3333-3333-333333333333" style={{ flex: 1, minWidth: 260 }} {...form.getInputProps('id')} />
            <Button type="submit" mt={{ base: 0, sm: 25 }}>Agregar sucursal</Button>
          </Group>
        </form>
      </Stack>
    </Section>
  );
}

function PasswordSection() {
  const { session } = useAuth();
  const form = useForm({
    validateInputOnBlur: true,
    initialValues: { currentPassword: '', newPassword: '', confirm: '' },
    validate: {
      currentPassword: requiredError('La contraseña actual'),
      newPassword: (v, values) => passwordError(v) ?? (v === values.currentPassword ? 'La nueva contraseña debe ser distinta' : null),
      confirm: (v, values) => (v === values.newPassword ? null : 'Las contraseñas no coinciden'),
    },
  });
  const [done, setDone] = useState(false);

  const change = useMutation({
    mutationFn: (v: { currentPassword: string; newPassword: string }) =>
      usersApi.changePassword(session!.user.id, { currentPassword: v.currentPassword, newPassword: v.newPassword }),
    onSuccess: () => {
      notifySuccess('Contraseña actualizada');
      form.reset();
      setDone(true);
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 400) form.setErrors(error.formErrors());
      else notifyError(error);
    },
  });

  return (
    <Section title="Tu contraseña" description="Mínimo 8 caracteres, con letras y números.">
      <form onSubmit={form.onSubmit((v) => { setDone(false); change.mutate(v); })} noValidate>
        <Stack gap="sm" maw={460}>
          <PasswordInput label="Contraseña actual" autoComplete="current-password" {...form.getInputProps('currentPassword')} />
          <PasswordInput label="Nueva contraseña" autoComplete="new-password" {...form.getInputProps('newPassword')} />
          <PasswordInput label="Confirmar nueva contraseña" autoComplete="new-password" {...form.getInputProps('confirm')} />
          <Group justify="flex-end" align="center">
            {done && <Text size="sm" c="pine.8" role="status">Contraseña actualizada</Text>}
            <Button type="submit" loading={change.isPending}>Cambiar contraseña</Button>
          </Group>
        </Stack>
      </form>
    </Section>
  );
}

export function SettingsPage() {
  return (
    <>
      <PageHeader title="Configuración" />
      <Stack gap="md">
        <CompanySection />
        <BranchesSection />
        <PasswordSection />
      </Stack>
    </>
  );
}
