import { Tooltip } from '@mantine/core';
import { cloneElement, type ReactElement } from 'react';
import { useAuth } from '@/app/auth';

/**
 * Wraps an Owner-only control. For employees it stays visible but disabled,
 * with a tooltip explaining why (the API would answer 403).
 */
export function OwnerAction({ children }: { children: ReactElement<{ disabled?: boolean }> }) {
  const { isOwner } = useAuth();
  if (isOwner) return children;
  return (
    <Tooltip label="Solo un propietario puede hacer esto">
      <span style={{ display: 'inline-flex' }}>{cloneElement(children, { disabled: true })}</span>
    </Tooltip>
  );
}
