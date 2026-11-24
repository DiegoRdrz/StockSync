import { Group, Stack, Text, Title } from '@mantine/core';
import type { ReactNode } from 'react';

interface PageHeaderProps {
  title: string;
  description?: ReactNode;
  actions?: ReactNode;
}

export function PageHeader({ title, description, actions }: PageHeaderProps) {
  return (
    <Group justify="space-between" align="flex-end" wrap="wrap" gap="sm" mb="lg">
      <Stack gap={4} style={{ minWidth: 0 }}>
        <Title order={1}>{title}</Title>
        {description && (
          <Text size="sm" c="dimmed" maw={640}>
            {description}
          </Text>
        )}
      </Stack>
      {actions && <Group gap="xs">{actions}</Group>}
    </Group>
  );
}
