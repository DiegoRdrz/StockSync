import { Select, type SelectProps } from '@mantine/core';
import { IconBuildingStore } from '@tabler/icons-react';
import { useBranches } from '@/lib/branches';

type BranchSelectProps = Omit<SelectProps, 'data'> & { withAll?: boolean };

/** Picks a branch from the local catalogue. `withAll` adds an empty "all branches" option. */
export function BranchSelect({ withAll, placeholder, ...props }: BranchSelectProps) {
  const { branches } = useBranches();
  const data = [
    ...(withAll ? [{ value: '', label: 'Todas las sucursales' }] : []),
    ...branches.map((b) => ({ value: b.id, label: b.name })),
  ];
  return (
    <Select
      leftSection={<IconBuildingStore size={16} />}
      data={data}
      placeholder={placeholder ?? 'Selecciona sucursal'}
      comboboxProps={{ withinPortal: true }}
      {...props}
    />
  );
}
