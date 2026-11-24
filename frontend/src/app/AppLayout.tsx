import {
  ActionIcon, AppShell, Avatar, Burger, Group, Menu, NavLink, ScrollArea, Stack, Text, Tooltip,
  UnstyledButton, useComputedColorScheme, useMantineColorScheme, Kbd,
} from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { Spotlight, spotlight, type SpotlightActionData } from '@mantine/spotlight';
import { IconLogout, IconMoon, IconSearch, IconSun, IconSettings } from '@tabler/icons-react';
import { Suspense } from 'react';
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { BranchSelect, TableSkeleton } from '@/components';
import { useActiveBranch } from '@/lib/branches';
import { useAuth } from './auth';
import { ALL_NAV_ITEMS, NAV } from './navigation';
import { Logo } from './Logo';

export function AppLayout() {
  const [opened, { toggle, close }] = useDisclosure();
  const { session, isOwner, signOut, role } = useAuth();
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const [activeBranch, setActiveBranch] = useActiveBranch();
  const { setColorScheme } = useMantineColorScheme();
  const scheme = useComputedColorScheme('light');

  // The most specific nav entry that prefixes the path is the active one.
  const activeTo = ALL_NAV_ITEMS.map((i) => i.to)
    .filter((to) => (to === '/' ? pathname === '/' : pathname === to || pathname.startsWith(to + '/')))
    .sort((a, b) => b.length - a.length)[0];
  const isActive = (to: string) => to === activeTo;

  const actions: SpotlightActionData[] = ALL_NAV_ITEMS.filter((i) => isOwner || !i.ownerOnly).map((item) => ({
    id: item.to,
    label: item.label,
    description: item.description,
    leftSection: <item.icon size={18} stroke={1.6} />,
    onClick: () => navigate(item.to),
  }));

  const user = session!.user;
  const initials = `${user.firstName[0] ?? ''}${user.lastName[0] ?? ''}`.toUpperCase();

  return (
    <AppShell
      header={{ height: 56 }}
      navbar={{ width: 248, breakpoint: 'sm', collapsed: { mobile: !opened } }}
      padding={{ base: 'md', md: 'xl' }}
    >
      <AppShell.Header>
        <Group h="100%" px="md" justify="space-between" wrap="nowrap" gap="sm">
          <Group gap="sm" wrap="nowrap">
            <Burger opened={opened} onClick={toggle} hiddenFrom="sm" size="sm" aria-label="Abrir menú" />
            <Logo compactOnMobile />
          </Group>
          <Group gap="xs" wrap="nowrap">
            <UnstyledButton onClick={spotlight.open} visibleFrom="md" aria-label="Buscar sección">
              <Group gap={8} px="sm" h={34} style={{ border: '1px solid var(--ss-border)', borderRadius: 8 }} c="dimmed">
                <IconSearch size={15} />
                <Text size="sm" w={140}>Ir a…</Text>
                <Kbd size="xs">⌘ K</Kbd>
              </Group>
            </UnstyledButton>
            <BranchSelect
              value={activeBranch}
              onChange={(value) => value && setActiveBranch(value)}
              w={{ base: 150, sm: 210 }}
              aria-label="Sucursal activa"
            />
            <Tooltip label={scheme === 'dark' ? 'Modo claro' : 'Modo oscuro'}>
              <ActionIcon
                visibleFrom="xs"
                variant="default"
                size={34}
                onClick={() => setColorScheme(scheme === 'dark' ? 'light' : 'dark')}
                aria-label="Cambiar tema"
              >
                {scheme === 'dark' ? <IconSun size={17} /> : <IconMoon size={17} />}
              </ActionIcon>
            </Tooltip>
            <Menu position="bottom-end" width={240} shadow="md">
              <Menu.Target>
                <UnstyledButton aria-label="Menú de usuario">
                  <Avatar color="pine" radius="xl" size={34}>{initials}</Avatar>
                </UnstyledButton>
              </Menu.Target>
              <Menu.Dropdown>
                <Menu.Label>
                  <Text size="sm" fw={600} c="var(--mantine-color-text)">{user.firstName} {user.lastName}</Text>
                  <Text size="xs" c="dimmed">{user.email}</Text>
                </Menu.Label>
                <Menu.Item leftSection={<IconSettings size={16} />} onClick={() => navigate('/settings')}>
                  Configuración
                </Menu.Item>
                <Menu.Item
                  hiddenFrom="xs"
                  leftSection={scheme === 'dark' ? <IconSun size={16} /> : <IconMoon size={16} />}
                  onClick={() => setColorScheme(scheme === 'dark' ? 'light' : 'dark')}
                >
                  {scheme === 'dark' ? 'Modo claro' : 'Modo oscuro'}
                </Menu.Item>
                <Menu.Divider />
                <Menu.Item color="red" leftSection={<IconLogout size={16} />} onClick={signOut}>
                  Cerrar sesión
                </Menu.Item>
              </Menu.Dropdown>
            </Menu>
          </Group>
        </Group>
      </AppShell.Header>

      <AppShell.Navbar>
        <AppShell.Section px="md" pt="md" pb="sm">
          <Text fw={600} size="sm" truncate>{session!.tenant.name}</Text>
          <Text size="xs" c="dimmed">{role === 'Owner' ? 'Propietario' : 'Empleado'}</Text>
        </AppShell.Section>
        <AppShell.Section grow component={ScrollArea} px="xs" pb="md">
          <Stack gap="md">
            {NAV.map((section) => (
              <div key={section.label}>
                <Text size="xs" fw={600} c="dimmed" px="sm" mb={4}>{section.label}</Text>
                {section.items.filter((i) => isOwner || !i.ownerOnly).map((item) => (
                  <NavLink
                    key={item.to}
                    component={Link}
                    to={item.to}
                    label={item.label}
                    active={isActive(item.to)}
                    leftSection={<item.icon size={18} stroke={1.6} />}
                    onClick={close}
                    style={{ borderRadius: 8 }}
                    fw={500}
                  />
                ))}
              </div>
            ))}
          </Stack>
        </AppShell.Section>
      </AppShell.Navbar>

      <AppShell.Main>
        <div style={{ maxWidth: 1360, margin: '0 auto' }}>
          <Suspense fallback={<TableSkeleton rows={8} />}>
            <Outlet />
          </Suspense>
        </div>
      </AppShell.Main>

      <Spotlight
        actions={actions}
        shortcut={['mod + K']}
        nothingFound="Ninguna sección coincide"
        searchProps={{ leftSection: <IconSearch size={18} />, placeholder: 'Ir a una sección…' }}
        highlightQuery
      />
    </AppShell>
  );
}
