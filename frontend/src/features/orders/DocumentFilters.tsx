import { Box, Button, Group, Select } from '@mantine/core';
import { DatePickerInput, type DatesRangeValue } from '@mantine/dates';
import { IconFilterOff } from '@tabler/icons-react';
import { BranchSelect, statusLabel } from '@/components';

export interface DocumentFilterValues {
  status: string | null;
  branchId: string;
  range: DatesRangeValue<string>;
}

export const emptyFilters: DocumentFilterValues = { status: null, branchId: '', range: [null, null] };

interface DocumentFiltersProps {
  statuses: string[];
  value: DocumentFilterValues;
  onChange: (value: DocumentFilterValues) => void;
}

export function DocumentFilters({ statuses, value, onChange }: DocumentFiltersProps) {
  const dirty = !!value.status || !!value.branchId || !!value.range[0] || !!value.range[1];
  return (
    <Box component="form" role="search" aria-label="Filtros" mb="md" onSubmit={(e) => e.preventDefault()}>
      <Group gap="sm" align="flex-end" wrap="wrap">
        <Select
          label="Estado"
          placeholder="Todos los estados"
          clearable
          w={{ base: '100%', xs: 200 }}
          data={statuses.map((s) => ({ value: s, label: statusLabel(s) }))}
          value={value.status}
          onChange={(status) => onChange({ ...value, status })}
        />
        <BranchSelect
          withAll
          label="Sucursal"
          w={{ base: '100%', xs: 220 }}
          value={value.branchId}
          onChange={(branchId) => onChange({ ...value, branchId: branchId ?? '' })}
        />
        <DatePickerInput
          type="range"
          label="Fecha de creación"
          placeholder="Desde - hasta"
          clearable
          w={{ base: '100%', xs: 260 }}
          valueFormat="D MMM YYYY"
          value={value.range}
          onChange={(range) => onChange({ ...value, range })}
        />
        <Button variant="subtle" color="gray" leftSection={<IconFilterOff size={16} />} disabled={!dirty} onClick={() => onChange(emptyFilters)}>
          Limpiar filtros
        </Button>
      </Group>
    </Box>
  );
}
