import { Button, Tooltip, type ButtonProps } from '@mantine/core';
import type { ReactElement, ReactNode } from 'react';
import { OwnerAction } from '@/components';
import { confirmAction } from '@/lib/notify';

interface DocumentActionButtonProps extends Pick<ButtonProps, 'color' | 'variant'> {
  label: string;
  icon?: ReactNode;
  mutation: { mutate: () => void; isPending: boolean };
  /** Any mutation on the document in flight: locks the button. */
  busy: boolean;
  confirm: { title: string; message: string; destructive?: boolean };
  /** When set the button is disabled and the tooltip explains why. */
  disabledReason?: string;
  ownerOnly?: boolean;
}

/** Status-transition button: confirmation with explicit consequence, owner lock, disabled-with-reason. */
export function DocumentActionButton({
  label,
  icon,
  mutation,
  busy,
  confirm,
  disabledReason,
  ownerOnly,
  color,
  variant,
}: DocumentActionButtonProps) {
  const button: ReactElement<{ disabled?: boolean }> = (
    <Button
      fullWidth
      color={color}
      variant={variant}
      leftSection={icon}
      loading={mutation.isPending}
      disabled={busy || !!disabledReason}
      onClick={() =>
        confirmAction({ ...confirm, confirmLabel: label, onConfirm: () => mutation.mutate() })
      }
    >
      {label}
    </Button>
  );

  if (disabledReason) {
    return (
      <Tooltip label={disabledReason} multiline maw={260} events={{ hover: true, focus: true, touch: true }}>
        <span tabIndex={0} style={{ display: 'block' }}>{button}</span>
      </Tooltip>
    );
  }
  return ownerOnly ? <OwnerAction>{button}</OwnerAction> : button;
}
