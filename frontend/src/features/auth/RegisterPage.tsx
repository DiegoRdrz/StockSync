import { Anchor, Button, Group, PasswordInput, SimpleGrid, Stack, Text, TextInput, Title, Alert } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useMutation } from '@tanstack/react-query';
import { IconAlertTriangle } from '@tabler/icons-react';
import { Link, useNavigate } from 'react-router-dom';
import { ApiError } from '@/api/client';
import { authApi } from '@/api/endpoints';
import type { RegisterTenantRequest } from '@/api/types';
import { useAuth } from '@/app/auth';
import { errorMessage, notifySuccess } from '@/lib/notify';
import { passwordError } from '@/lib/validation';
import { AuthFrame } from './AuthFrame';

export function RegisterPage() {
  const { signIn } = useAuth();
  const navigate = useNavigate();

  const form = useForm<RegisterTenantRequest>({
    initialValues: { companyName: '', taxId: '', firstName: '', lastName: '', email: '', password: '' },
    validateInputOnBlur: true,
    validate: {
      companyName: (v) => (v.trim() ? null : 'Escribe el nombre de la empresa'),
      firstName: (v) => (v.trim() ? null : 'Escribe tu nombre'),
      lastName: (v) => (v.trim() ? null : 'Escribe tu apellido'),
      email: (v) => (/^\S+@\S+\.\S+$/.test(v) ? null : 'Escribe un correo válido'),
      password: passwordError,
    },
  });

  const register = useMutation({
    mutationFn: authApi.registerTenant,
    onSuccess: (auth) => {
      signIn(auth);
      notifySuccess(`${auth.tenant.name} quedó registrada. Eres su propietario.`, 'Empresa creada');
      navigate('/', { replace: true });
    },
    onError: (error) => {
      if (error instanceof ApiError) form.setErrors(error.formErrors());
    },
  });

  return (
    <AuthFrame>
      <Stack gap="lg">
        <Title order={1} fz={22}>Registrar empresa</Title>

        {register.isError && Object.keys((register.error as ApiError).fieldErrors ?? {}).length === 0 && (
          <Alert color="red" variant="light" icon={<IconAlertTriangle size={18} />}>
            {errorMessage(register.error)}
          </Alert>
        )}

        <form
          onSubmit={form.onSubmit((values) => register.mutate({ ...values, taxId: values.taxId?.trim() || null }))}
          noValidate
        >
          <Stack gap="md">
            <TextInput label="Empresa" placeholder="Ferretería El Tornillo" {...form.getInputProps('companyName')} />
            <TextInput label="NIT" description="Opcional" placeholder="900123456-7" {...form.getInputProps('taxId')} />
            <SimpleGrid cols={2} spacing="sm">
              <TextInput label="Nombre" autoComplete="given-name" {...form.getInputProps('firstName')} />
              <TextInput label="Apellido" autoComplete="family-name" {...form.getInputProps('lastName')} />
            </SimpleGrid>
            <TextInput label="Correo" type="email" autoComplete="email" {...form.getInputProps('email')} />
            <PasswordInput label="Contraseña" description="Mínimo 8 caracteres, con letras y números" autoComplete="new-password" {...form.getInputProps('password')} />
            <Group justify="flex-end" mt="xs">
              <Button type="submit" loading={register.isPending} fullWidth>Crear empresa</Button>
            </Group>
          </Stack>
        </form>

        <Text size="sm" c="dimmed" ta="center">
          <Anchor component={Link} to="/login" fw={600}>Ya tengo cuenta</Anchor>
        </Text>
      </Stack>
    </AuthFrame>
  );
}
