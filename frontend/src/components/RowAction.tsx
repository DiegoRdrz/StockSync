import { ActionIcon, Tooltip, type ActionIconProps } from '@mantine/core';
import type { ReactNode } from 'react';
import { useAuth } from '@/app/auth';

interface RowActionProps extends ActionIconProps {
  label: string;
  /** Accessible name; defaults to `label`. */
  ariaLabel?: string;
  ownerOnly?: boolean;
  onClick: () => void;
  children: ReactNode;
}

/** Icon button with tooltip. Owner-only actions are disabled for employees, with the reason as tooltip. */
export function RowAction({ label, ariaLabel, ownerOnly, onClick, children, disabled, ...props }: RowActionProps) {
  const { isOwner } = useAuth();
  const locked = ownerOnly && !isOwner;
  return (
    <Tooltip label={locked ? 'Solo un propietario puede hacer esto' : label}>
      <ActionIcon
        variant="subtle"
        color="gray"
        size="lg"
        aria-label={ariaLabel ?? label}
        disabled={disabled || locked}
        onClick={onClick}
        {...props}
      >
        {children}
      </ActionIcon>
    </Tooltip>
  );
}
