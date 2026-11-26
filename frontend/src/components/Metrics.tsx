import { SimpleGrid, Skeleton, Text } from '@mantine/core';
import type { ReactNode } from 'react';
import classes from './Metrics.module.css';

export interface MetricProps {
  label: string;
  value?: ReactNode;
  loading?: boolean;
  /** Semantic colour only (e.g. 'amber.8' for low stock, 'red.7' for depleted). */
  color?: string;
}

export function Metric({ label, value, loading, color }: MetricProps) {
  return (
    <div className={classes.metric} role="group" aria-label={label}>
      <Text size="sm" c="dimmed">{label}</Text>
      {loading ? (
        <Skeleton h={28} w="50%" mt={4} />
      ) : (
        <Text fz={26} fw={600} lh={1.25} className="tabular" c={color}>{value ?? '—'}</Text>
      )}
    </div>
  );
}

/** Open row of headline figures: no cards, separated by hairlines on wide screens. */
export function Metrics({ children, cols = 4 }: { children: ReactNode; cols?: number }) {
  return (
    <SimpleGrid cols={{ base: 2, md: cols }} spacing={0} verticalSpacing="md" className={classes.row}>
      {children}
    </SimpleGrid>
  );
}
