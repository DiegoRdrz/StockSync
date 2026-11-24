# StockSync Web: frontend conventions

Stack: React 19 + TypeScript (strict) + Vite 8, **Mantine 9** (`@mantine/core`, `form`, `dates`,
`charts`, `modals`, `notifications`, `spotlight`), TanStack Query 5, React Router 7,
`@tabler/icons-react`, dayjs. No Shadcn, Tailwind or Radix.

## Where things live
- `src/api/types.ts`: DTOs mirrored from the .NET services. `src/api/endpoints.ts`: one function per gateway route.
- `src/api/client.ts`: fetch wrapper, JWT, `ApiError` (`status`, `formErrors()`, `fullMessage()`).
- `src/app/`: auth (`useAuth()` gives `session`, `isOwner`, `role`), router, layout, navigation.
- `src/components/`: `PageHeader`, `QueryState`/`ErrorState`/`EmptyState`/`TableSkeleton`, `TablePager`,
  `StatusBadge`/`statusLabel`, `StockLevel`, `BranchSelect`, `ProductSelect`, `OwnerAction`, `Money`.
- `src/lib/`: `format.ts` (es-CO money/number/date), `branches.ts` (`useBranches`, `useActiveBranch`),
  `notify.tsx` (`notifySuccess`, `notifyError`, `confirmAction`), `validation.ts`.
- `src/features/<area>/`: pages and their private components/hooks.

## Rules
- UI copy in Spanish (Colombia), sentence case, concrete verbs ("Registrar entrada", not "Aceptar").
  No all-caps labels, no `A · B · C` meta strings, no `→` appended to buttons.
- Every data view handles loading (skeleton), error (retry), empty (next action) and 403 via `QueryState`.
- Owner-only actions are wrapped in `<OwnerAction>` (disabled with tooltip for employees). Do not call
  Owner-only endpoints for employees; show a locked state instead.
- Forms: `@mantine/form` inside a `Modal`, label on top, `validateInputOnBlur`, map 400 responses with
  `form.setErrors(error.formErrors())`, primary button on the right with `loading`.
- Mutations: `notifySuccess` on success with the same verb as the button, `notifyError` on failure,
  then invalidate the related query keys. Destructive actions go through `confirmAction({ destructive: true })`.
- Tables: Mantine `Table` inside `Paper` with `Table.ScrollContainer`; numbers right-aligned with `className="tabular"`;
  row actions at the end as `ActionIcon` with `aria-label` + `Tooltip`, or a `Menu`.
- Colour: `pine` = primary action, `amber` = low stock only, `red` = errors/destructive. Use theme colours, never raw hex.
- Query keys start with the resource: `['products', ...]`, `['stock', ...]`, `['orders', ...]`, `['purchase-orders', ...]`,
  `['reports', ...]`, `['notifications', ...]`, `['recipients', ...]`, `['users', ...]`, `['tenant']`.
