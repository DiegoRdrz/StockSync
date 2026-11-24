import { Group, Pagination, Text } from '@mantine/core';
import { formatNumber } from '@/lib/format';

interface TablePagerProps {
  page: number;
  totalPages: number;
  total?: number;
  onChange: (page: number) => void;
  /** Plural noun; pass `singular` when it is not the plural minus the final -s. */
  noun?: string;
  singular?: string;
}

export function TablePager({ page, totalPages, total, onChange, noun = 'registros', singular }: TablePagerProps) {
  const word = total === 1 ? singular ?? noun.replace(/s$/, '') : noun;
  if (!totalPages && !total) return null;
  return (
    <Group justify="space-between" px="sm" py="xs" wrap="wrap" gap="xs">
      <Text size="xs" c="dimmed" className="tabular">
        {total !== undefined ? `${formatNumber(total)} ${word}` : `Página ${page}`}
      </Text>
      {totalPages > 1 && <Pagination size="sm" value={page} total={totalPages} onChange={onChange} withEdges={totalPages > 5} />}
    </Group>
  );
}
