import { Badge, Button, Group, Modal, Paper, PasswordInput, Select, Stack, Table, Text, TextInput, VisuallyHidden } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useDebouncedValue } from '@mantine/hooks';
import { IconPencil, IconPlus, IconSearch, IconUserOff } from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { usersApi } from '@/api/endpoints';
import { ApiError } from '@/api/client';
import { RoleType, type RoleTypeValue, type UserResponse } from '@/api/types';
import { useAuth } from '@/app/auth';
import { EmptyState, OwnerAction, PageHeader, QueryState, RowAction, TablePager, TableSkeleton } from '@/components';
import { formatDate } from '@/lib/format';
import { confirmAction, notifyError, notifySuccess } from '@/lib/notify';
import { emailError, passwordError, requiredError } from '@/lib/validation';
import { useClampPage } from '@/lib/useClampPage';

const ROLE_OPTIONS = [
  { value: String(RoleType.Owner), label: 'Propietario' },
  { value: String(RoleType.Employee), label: 'Empleado' },
];

const roleLabel = (role: RoleTypeValue) => (role === RoleType.Owner ? 'Propietario' : 'Empleado');

interface FormValues { firstName: string; lastName: string; email: string; password: string; role: string }

function UserModal({ user, onClose }: { user: UserResponse | null; onClose: () => void }) {
  const queryClient = useQueryClient();
  const editing = !!user;
  const form = useForm<FormValues>({
    validateInputOnBlur: true,
    initialValues: {
      firstName: user?.firstName ?? '',
      lastName: user?.lastName ?? '',
      email: user?.email ?? '',
      password: '',
      role: String(user?.role ?? RoleType.Employee),
    },
    validate: {
      firstName: requiredError('El nombre'),
      lastName: requiredError('El apellido'),
      email: (v) => (editing ? null : emailError(v)),
      password: (v) => (editing ? null : passwordError(v)),
    },
  });

  const save = useMutation({
    mutationFn: (v: FormValues) => {
      const role = Number(v.role) as RoleTypeValue;
      return user
        ? usersApi.update(user.id, { firstName: v.firstName.trim(), lastName: v.lastName.trim(), role })
        : usersApi.create({ firstName: v.firstName.trim(), lastName: v.lastName.trim(), email: v.email.trim(), password: v.password, role });
    },
    onSuccess: () => {
      notifySuccess(editing ? 'Usuario guardado' : 'Usuario creado');
      queryClient.invalidateQueries({ queryKey: ['users'] });
      onClose();
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 400) form.setErrors(error.formErrors());
      else notifyError(error);
    },
  });

  return (
    <Modal opened onClose={onClose} closeOnClickOutside={!save.isPending} title={<Text fw={650}>{editing ? 'Editar usuario' : 'Crear usuario'}</Text>}>
      <form onSubmit={form.onSubmit((v) => save.mutate(v))} noValidate>
        <Stack gap="sm">
          <Group grow align="flex-start">
            <TextInput label="Nombre" data-autofocus {...form.getInputProps('firstName')} />
            <TextInput label="Apellido" {...form.getInputProps('lastName')} />
          </Group>
          {!editing && (
            <>
              <TextInput label="Correo electrónico" inputMode="email" autoComplete="off" {...form.getInputProps('email')} />
              <PasswordInput
                label="Contraseña inicial"
                description="Mínimo 8 caracteres, con letras y al menos un número."
                autoComplete="new-password"
                {...form.getInputProps('password')}
              />
            </>
          )}
          <Select label="Rol" data={ROLE_OPTIONS} description="Los empleados no ven reportes de compras ni administran usuarios." {...form.getInputProps('role')} />
          <Group justify="flex-end" mt="xs">
            <Button variant="default" onClick={onClose} disabled={save.isPending}>Cancelar</Button>
            <Button type="submit" loading={save.isPending}>{editing ? 'Guardar cambios' : 'Crear usuario'}</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

export function UsersPage() {
  const queryClient = useQueryClient();
  const { session } = useAuth();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [debounced] = useDebouncedValue(search.trim(), 300);
  const [role, setRole] = useState('');
  const [active, setActive] = useState('');
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<UserResponse | null>(null);

  const filter = {
    page,
    pageSize: 15,
    search: debounced || undefined,
    role: role ? (Number(role) as RoleTypeValue) : undefined,
    isActive: active === '' ? undefined : active === 'true',
  };
  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['users', filter],
    queryFn: () => usersApi.list(filter),
    placeholderData: (prev) => prev,
  });

  const deactivate = useMutation({
    mutationFn: (id: string) => usersApi.deactivate(id),
    onSuccess: () => {
      notifySuccess('Usuario desactivado');
      queryClient.invalidateQueries({ queryKey: ['users'] });
    },
    onError: (e) => notifyError(e),
  });

  const hasFilters = !!(debounced || role || active);

  useClampPage(page, data?.totalPages, setPage);

  return (
    <>
      <PageHeader
        title="Usuarios"
        actions={
          <OwnerAction>
            <Button leftSection={<IconPlus size={16} />} onClick={() => setCreating(true)}>Crear usuario</Button>
          </OwnerAction>
        }
      />

      <Paper p="sm" mb="md">
        <Group gap="sm" wrap="wrap">
          <TextInput
            aria-label="Buscar usuarios"
            placeholder="Buscar por nombre o correo"
            leftSection={<IconSearch size={16} />}
            value={search}
            onChange={(e) => { setSearch(e.currentTarget.value); setPage(1); }}
            w={{ base: '100%', xs: 280 }}
          />
          <Select aria-label="Filtrar por rol" data={[{ value: '', label: 'Todos los roles' }, ...ROLE_OPTIONS]} value={role} onChange={(v) => { setRole(v ?? ''); setPage(1); }} w={{ base: '48%', xs: 170 }} />
          <Select
            aria-label="Filtrar por estado"
            data={[{ value: '', label: 'Todos los estados' }, { value: 'true', label: 'Activos' }, { value: 'false', label: 'Inactivos' }]}
            value={active}
            onChange={(v) => { setActive(v ?? ''); setPage(1); }}
            w={{ base: '48%', xs: 170 }}
          />
        </Group>
      </Paper>

      <Paper>
        <QueryState
          isLoading={isLoading}
          error={error}
          onRetry={refetch}
          isEmpty={(data?.items.length ?? 0) === 0}
          skeleton={<TableSkeleton rows={6} />}
          empty={<EmptyState title={hasFilters ? 'Ningún usuario coincide' : 'Aún no hay usuarios'}  />}
        >
          <Table.ScrollContainer minWidth={620}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Usuario</Table.Th>
                  <Table.Th>Rol</Table.Th>
                  <Table.Th>Estado</Table.Th>
                  <Table.Th>Creado</Table.Th>
                  <Table.Th ta="right" w={96}><VisuallyHidden>Acciones</VisuallyHidden></Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {data?.items.map((u) => {
                  const isSelf = u.id === session?.user.id;
                  const fullName = `${u.firstName} ${u.lastName}`.trim();
                  return (
                    <Table.Tr key={u.id}>
                      <Table.Td maw={280}>
                        <Text size="sm" fw={500} truncate>{fullName}{isSelf && <Text component="span" size="xs" c="dimmed"> (tú)</Text>}</Text>
                        <Text size="xs" c="dimmed" truncate>{u.email}</Text>
                      </Table.Td>
                      <Table.Td><Badge color={u.role === RoleType.Owner ? 'pine' : 'gray'} size="sm">{roleLabel(u.role)}</Badge></Table.Td>
                      <Table.Td><Badge color={u.isActive ? 'pine' : 'gray'} size="sm">{u.isActive ? 'Activo' : 'Inactivo'}</Badge></Table.Td>
                      <Table.Td className="tabular">{formatDate(u.createdAt)}</Table.Td>
                      <Table.Td>
                        <Group gap={4} justify="flex-end" wrap="nowrap">
                          <RowAction ownerOnly size="md" label="Editar" ariaLabel={`Editar a ${fullName}`} onClick={() => setEditing(u)}>
                            <IconPencil size={16} />
                          </RowAction>
                          <RowAction
                            ownerOnly
                            size="md"
                            color="red"
                            label={isSelf ? 'No puedes desactivar tu propia cuenta' : u.isActive ? 'Desactivar' : 'Ya está inactivo'}
                            ariaLabel={`Desactivar a ${fullName}`}
                            disabled={isSelf || !u.isActive}
                            onClick={() =>
                              confirmAction({
                                title: 'Desactivar usuario',
                                message: `${fullName} ya no podrá iniciar sesión. Sus ventas y movimientos se conservan.`,
                                confirmLabel: 'Desactivar usuario',
                                destructive: true,
                                onConfirm: () => deactivate.mutate(u.id),
                              })
                            }
                          >
                            <IconUserOff size={16} />
                          </RowAction>
                        </Group>
                      </Table.Td>
                    </Table.Tr>
                  );
                })}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          <TablePager page={page} totalPages={data?.totalPages ?? 0} total={data?.totalCount} onChange={setPage} noun="usuarios" />
        </QueryState>
      </Paper>

      {creating && <UserModal user={null} onClose={() => setCreating(false)} />}
      {editing && <UserModal key={editing.id} user={editing} onClose={() => setEditing(null)} />}
    </>
  );
}
