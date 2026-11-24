import { Box, Center } from '@mantine/core';
import type { ReactNode } from 'react';
import { Logo } from '@/app/Logo';

export function AuthFrame({ children }: { children: ReactNode }) {
  return (
    <Center mih="100vh" px="md" py={48} bg="var(--ss-canvas)">
      <Box w="100%" maw={380}>
        <Box mb={32}><Logo /></Box>
        {children}
      </Box>
    </Center>
  );
}
