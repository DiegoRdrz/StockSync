import { Anchor, Button, Divider, Group, PasswordInput, Stack, Text, TextInput, Title, Alert } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useMutation } from '@tanstack/react-query';
import { IconAlertTriangle } from '@tabler/icons-react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { ApiError } from '@/api/client';
import { authApi } from '@/api/endpoints';
import type { LoginRequest } from '@/api/types';
import { useAuth } from '@/app/auth';
import { errorMessage } from '@/lib/notify';
import { AuthFrame } from './AuthFrame';

const DEMO_ACCOUNTS = [
  { label: 'Propietario demo', email: 'owner@stocksync.demo' },
  { label: 'Empleado demo', email: 'employee@stocksync.demo' },
];

export function LoginPage() {
  const { signIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from ?? '/';

  const form = useForm<LoginRequest>({
    initialValues: { email: '', password: '' },
    validateInputOnBlur: true,
    validate: {
      email: (v) => (/^\S+@\S+\.\S+$/.test(v) ? null : 'Escribe un correo válido'),
      password: (v) => (v ? null : 'Escribe tu contraseña'),
    },
  });

  const login = useMutation({
    mutationFn: authApi.login,
    onSuccess: (auth) => {
      signIn(auth);
      navigate(from, { replace: true });
    },
    onError: (error) => {
      if (error instanceof ApiError) form.setErrors(error.formErrors());
    },
  });

  return (
    <AuthFrame>
      <Stack gap="lg">
        <Title order={1} fz={22}>Iniciar sesión</Title>

        {login.isError && (
          <Alert color="red" variant="light" icon={<IconAlertTriangle size={18} />}>
            {login.error instanceof ApiError && login.error.status === 401
              ? 'El correo o la contraseña no coinciden.'
              : errorMessage(login.error)}
          </Alert>
        )}

        <form onSubmit={form.onSubmit((values) => login.mutate(values))} noValidate>
          <Stack gap="md">
            <TextInput label="Correo" type="email" autoComplete="email" placeholder="tu@empresa.com" {...form.getInputProps('email')} />
            <PasswordInput label="Contraseña" autoComplete="current-password" {...form.getInputProps('password')} />
            <Button type="submit" loading={login.isPending} fullWidth mt="xs">
              Iniciar sesión
            </Button>
          </Stack>
        </form>

        <Divider label="Cuentas de demostración" labelPosition="center" />
        <Group grow gap="xs">
          {DEMO_ACCOUNTS.map((account) => (
            <Button
              key={account.email}
              variant="default"
              size="sm"
              onClick={() => form.setValues({ email: account.email, password: 'Password123!' })}
            >
              {account.label}
            </Button>
          ))}
        </Group>

        <Text size="sm" c="dimmed" ta="center">
          <Anchor component={Link} to="/register" fw={600}>Registrar empresa</Anchor>
        </Text>
      </Stack>
    </AuthFrame>
  );
}
