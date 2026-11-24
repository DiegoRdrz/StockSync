import { useEffect } from 'react';

/**
 * Moves back to the last page when the current one becomes out of range,
 * e.g. after deleting the only row of the last page (the API answers 200 with no items).
 */
export function useClampPage(page: number, totalPages: number | undefined, setPage: (page: number) => void) {
  useEffect(() => {
    if (totalPages !== undefined && page > Math.max(1, totalPages)) setPage(Math.max(1, totalPages));
  }, [page, totalPages, setPage]);
}
