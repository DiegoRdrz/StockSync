import { useIsMutating, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError } from '@/api/client';
import { notifyError, notifySuccess } from '@/lib/notify';
import type { DocumentBase, DocumentKind } from './documents';

export const detailKey = (kind: DocumentKind, id: string) => [kind.key, 'detail', id] as const;
export const listKey = (kind: DocumentKind) => [kind.key, 'list'] as const;

/** Single document. While a saga is running (Confirming/Receiving) it polls until it settles. */
export function useDocument(kind: DocumentKind, id: string) {
  return useQuery({
    queryKey: detailKey(kind, id),
    queryFn: () => kind.api.get(id),
    enabled: !!id,
    retry: (count, error) => !(error instanceof ApiError && error.status === 404) && count < 2,
    refetchInterval: (query) =>
      query.state.data && kind.transientStatuses.includes(query.state.data.status) ? 3000 : false,
  });
}

/** True while any mutation on this document is in flight, to lock every action at once. */
export function useDocumentBusy(kind: DocumentKind, id: string) {
  return useIsMutating({ mutationKey: [kind.key, id] }) > 0;
}

/** Add, change and remove lines. Each call returns the whole document, which replaces the cache entry. */
export function useLineMutations(kind: DocumentKind, id: string) {
  const queryClient = useQueryClient();
  const store = (doc: DocumentBase) => {
    queryClient.setQueryData(detailKey(kind, id), doc);
    void queryClient.invalidateQueries({ queryKey: listKey(kind) });
  };
  const mutationKey = [kind.key, id, 'line'];

  const add = useMutation({
    mutationKey,
    mutationFn: (body: { productId: string; quantity: number }) => kind.api.addLine(id, body),
    onSuccess: store,
  });
  const update = useMutation({
    mutationKey,
    mutationFn: ({ lineId, quantity }: { lineId: string; quantity: number }) =>
      kind.api.updateLine(id, lineId, quantity),
    onSuccess: (doc) => {
      store(doc);
      notifySuccess('Cantidad actualizada');
    },
    onError: (error) => {
      notifyError(error, 'No se pudo cambiar la cantidad');
      void queryClient.invalidateQueries({ queryKey: detailKey(kind, id) });
    },
  });
  const remove = useMutation({
    mutationKey,
    mutationFn: (lineId: string) => kind.api.removeLine(id, lineId),
    onSuccess: (doc) => {
      store(doc);
      notifySuccess('Línea quitada');
    },
    onError: (error) => {
      notifyError(error, 'No se pudo quitar la línea');
      void queryClient.invalidateQueries({ queryKey: detailKey(kind, id) });
    },
  });
  return { add, update, remove };
}

interface ActionOptions {
  run: (id: string) => Promise<DocumentBase>;
  /** Toast shown on success, same verb as the button. */
  success: string;
  /** Saga actions that move stock also refresh stock, movements and reports. */
  touchesStock?: boolean;
}

/**
 * Status transition (confirm, cancel, place, receive). The detail is ALWAYS refetched afterwards,
 * even on failure: a 409 returns the document to Draft and a 503 may leave it in RequiresReview.
 */
export function useDocumentAction(kind: DocumentKind, id: string, { run, success, touchesStock }: ActionOptions) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [kind.key, id, 'action'],
    mutationFn: () => run(id),
    onSuccess: (doc) => {
      queryClient.setQueryData(detailKey(kind, id), doc);
      notifySuccess(success);
    },
    onError: (error) => notifyError(error),
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: detailKey(kind, id) });
      void queryClient.invalidateQueries({ queryKey: listKey(kind) });
      if (touchesStock) {
        for (const key of ['stock', 'movements', 'reports']) void queryClient.invalidateQueries({ queryKey: [key] });
      }
    },
  });
}
