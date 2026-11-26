import { Group, Text } from '@mantine/core';

export function Logo({ size = 28, compactOnMobile }: { size?: number; compactOnMobile?: boolean }) {
  return (
    <Group gap={8} wrap="nowrap">
      <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true">
        <rect width="32" height="32" rx="8" fill="var(--mantine-color-pine-7)" />
        <rect x="7" y="18" width="5" height="7" rx="1.5" fill="#fff" />
        <rect x="13.5" y="12" width="5" height="13" rx="1.5" fill="#fff" />
        <rect x="20" y="7" width="5" height="18" rx="1.5" fill="var(--mantine-color-amber-4)" />
      </svg>
      <Text visibleFrom={compactOnMobile ? 'xs' : undefined} ff="heading" fw={700} fz={19} lh={1} style={{ letterSpacing: '-0.02em' }}>
        StockSync
      </Text>
    </Group>
  );
}
