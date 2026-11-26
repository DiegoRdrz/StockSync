import { Alert, Button, Skeleton, Stack, Text } from '@mantine/core';

import type { ReactNode } from 'react';
import { ApiError } from '@/api/client';
import { errorMessage } from '@/lib/notify';

export function TableSkeleton({ rows = 6 }: { rows?: number }) {
  return (
    <Stack gap={8} p="sm" aria-busy="true" aria-label="Cargando">
      <Skeleton h={14} w="40%" />
      {Array.from({ length: rows }, (_, i) => (
        <Skeleton key={i} h={28} />
      ))}
    </Stack>
  );
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  if (error instanceof ApiError && error.status === 403) {
    return (
      <EmptyState title="Solo para propietarios" description="Tu rol no tiene acceso a esta información." />
    );
  }
  return (
    <Alert color="red" variant="light" title="No se pudieron cargar los datos" m="sm">
      <Stack gap="xs" align="flex-start">
        <Text size="sm">{errorMessage(error)}</Text>
        {onRetry && (
          <Button size="xs" variant="white" color="red" onClick={onRetry}>
            Reintentar
          </Button>
        )}
      </Stack>
    </Alert>
  );
}

interface EmptyStateProps {
  title: string;
  /** Only when it tells the user what to do next. */
  description?: string;
  action?: ReactNode;
}

export function EmptyState({ title, description, action }: EmptyStateProps) {
  return (
    <Stack align="center" gap={6} py={40} px="md" ta="center">
      <Text fw={600} size="sm">{title}</Text>
      {description && (
        <Text size="sm" c="dimmed" maw={360}>
          {description}
        </Text>
      )}
      {action}
    </Stack>
  );
}

interface QueryStateProps {
  isLoading: boolean;
  error: unknown;
  isEmpty?: boolean;
  onRetry?: () => void;
  empty?: ReactNode;
  skeleton?: ReactNode;
  children: ReactNode;
}

/** Renders loading, error and empty states before the real content. */
export function QueryState({ isLoading, error, isEmpty, onRetry, empty, skeleton, children }: QueryStateProps) {
  if (isLoading) return <>{skeleton ?? <TableSkeleton />}</>;
  if (error) return <ErrorState error={error} onRetry={onRetry} />;
  if (isEmpty) return <>{empty ?? <EmptyState title="Sin resultados" />}</>;
  return <>{children}</>;
}
