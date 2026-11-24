// Branches live in another (not yet built) service, so the UI keeps a local
// catalogue of branch IDs with friendly names. The demo seed uses the first two.
import { useLocalStorage } from '@mantine/hooks';

export interface Branch {
  id: string;
  name: string;
}

export const DEMO_BRANCHES: Branch[] = [
  { id: '11111111-1111-1111-1111-111111111111', name: 'Sucursal Centro' },
  { id: '22222222-2222-2222-2222-222222222222', name: 'Sucursal Norte' },
];

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export const isGuid = (value: string) => GUID.test(value.trim());

export function useBranches() {
  const [branches, setBranches] = useLocalStorage<Branch[]>({
    key: 'stocksync.branches',
    defaultValue: DEMO_BRANCHES,
    getInitialValueInEffect: false,
  });

  const nameOf = (id: string | null | undefined) =>
    (id && branches.find((b) => b.id.toLowerCase() === id.toLowerCase())?.name) ||
    (id ? `Sucursal ${id.slice(0, 8)}` : '—');

  const add = (branch: Branch) =>
    setBranches((current) => [...current.filter((b) => b.id !== branch.id), branch]);
  const remove = (id: string) => setBranches((current) => current.filter((b) => b.id !== id));

  return { branches, nameOf, add, remove };
}

/** Branch currently selected in the header; shared by inventory, orders and reports. */
export function useActiveBranch() {
  const { branches } = useBranches();
  const [stored, setStored] = useLocalStorage<string>({
    key: 'stocksync.activeBranch',
    defaultValue: DEMO_BRANCHES[0].id,
    getInitialValueInEffect: false,
  });
  // If the stored branch was removed from the catalogue, fall back to the first one.
  const active = branches.some((b) => b.id === stored) ? stored : branches[0]?.id ?? '';
  return [active, setStored] as const;
}
