import { notifications } from '@mantine/notifications';
import { modals } from '@mantine/modals';
import { Text } from '@mantine/core';
import { IconCheck, IconX } from '@tabler/icons-react';
import { ApiError } from '@/api/client';

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.fullMessage();
  if (error instanceof Error) return error.message;
  return 'Ocurrió un error inesperado.';
}

export function notifySuccess(message: string, title?: string) {
  notifications.show({ title, message, color: 'pine', icon: <IconCheck size={18} />, autoClose: 3500 });
}

export function notifyError(error: unknown, title = 'No se pudo completar la acción') {
  notifications.show({ title, message: errorMessage(error), color: 'red', icon: <IconX size={18} />, autoClose: 7000 });
}

interface ConfirmOptions {
  title: string;
  /** State the consequence explicitly. */
  message: string;
  confirmLabel: string;
  destructive?: boolean;
  onConfirm: () => void;
}

export function confirmAction({ title, message, confirmLabel, destructive, onConfirm }: ConfirmOptions) {
  modals.openConfirmModal({
    title: <Text fw={650}>{title}</Text>,
    children: <Text size="sm">{message}</Text>,
    labels: { confirm: confirmLabel, cancel: 'Volver' },
    confirmProps: { color: destructive ? 'red' : 'pine' },
    onConfirm,
  });
}
