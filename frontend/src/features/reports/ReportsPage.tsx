import { Box, Group, Stack, Tabs } from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import { IconCalendar } from '@tabler/icons-react';
import { useSearchParams } from 'react-router-dom';
import { useMemo, useState } from 'react';
import { BranchSelect, PageHeader } from '@/components';
import type { ReportQuery } from '@/api/types';
import { dayEndUtc, dayjs, dayStartUtc, toIsoDay } from '@/lib/format';
import { InventoryTab } from './InventoryTab';
import { PurchasesTab } from './PurchasesTab';
import { SalesTab } from './SalesTab';
import { EventualNote } from './widgets';

type Range = [string | null, string | null];

const MAX_DAYS = 366;

const TABS = ['sales', 'purchases', 'inventory'] as const;

function rangeError([from, to]: Range): string | null {
  // A half-picked range is not an error yet: the user is still choosing the end date.
  if (!from || !to) return null;
  const days = dayjs(to).diff(dayjs(from), 'day') + 1;
  if (days > MAX_DAYS) return `El rango máximo es de ${MAX_DAYS} días; elegiste ${days}.`;
  return null;
}

export function ReportsPage() {
  const [range, setRange] = useState<Range>(() => [
    toIsoDay(dayjs().subtract(29, 'day').toDate()) ?? null,
    toIsoDay(new Date()) ?? null,
  ]);
  const [branchId, setBranchId] = useState('');
  const [params, setParams] = useSearchParams();
  const tab = TABS.find((t) => t === params.get('tab')) ?? 'sales';
  const error = rangeError(range);

  // Reports keep showing the last complete, valid range while the picker is being edited,
  // so the active tab, page and expanded rows survive a range change.
  const [applied, setApplied] = useState<[string, string]>([range[0]!, range[1]!]);
  const onRangeChange = (value: Range) => {
    setRange(value);
    if (value[0] && value[1] && !rangeError(value)) setApplied([value[0], value[1]]);
  };

  const query = useMemo<ReportQuery>(
    () => ({ from: dayStartUtc(applied[0]), to: dayEndUtc(applied[1]), branchId: branchId || undefined }),
    [applied, branchId],
  );

  return (
    <>
      <PageHeader title="Reportes" />

      <Box
        py="xs"
        mb="sm"
        bg="var(--ss-canvas)"
        style={{ position: 'sticky', top: 'var(--app-shell-header-height, 56px)', zIndex: 20 }}
      >
        <Group align="flex-start" gap="sm" wrap="wrap">
          <DatePickerInput
            type="range"
            label="Período"
            aria-label="Período del reporte"
            valueFormat="D MMM YYYY"
            leftSection={<IconCalendar size={16} />}
            value={range}
            onChange={(value) => onRangeChange(value as Range)}
            maxDate={toIsoDay(new Date())}
            allowSingleDateInRange
            miw={{ base: '100%', sm: 260 }}
            error={error ?? undefined}
            description={!range[1] ? 'Elige la fecha final' : undefined}
          />
          <BranchSelect
            withAll
            label="Sucursal"
            value={branchId}
            onChange={(v) => setBranchId(v ?? '')}
            miw={{ base: '100%', sm: 220 }}
          />
        </Group>
      </Box>

      <Stack gap="sm">
        <EventualNote />
        <Tabs
          value={tab}
          onChange={(value) => value && setParams((p) => { p.set('tab', value); return p; }, { replace: true })}
          keepMounted={false}
        >
          <Tabs.List mb="md">
            <Tabs.Tab value="sales">Ventas</Tabs.Tab>
            <Tabs.Tab value="purchases">Compras</Tabs.Tab>
            <Tabs.Tab value="inventory">Inventario</Tabs.Tab>
          </Tabs.List>
          <Tabs.Panel value="sales"><SalesTab query={query} /></Tabs.Panel>
          <Tabs.Panel value="purchases"><PurchasesTab query={query} /></Tabs.Panel>
          <Tabs.Panel value="inventory"><InventoryTab query={query} /></Tabs.Panel>
        </Tabs>
      </Stack>
    </>
  );
}
