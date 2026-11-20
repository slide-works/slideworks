import { useEffect, useState } from 'react';
import { ApiError, api } from '../api/client';
import type { LooksView } from '../api/types';

export type LooksLoad =
  { kind: 'loading' } | { kind: 'loaded'; looks: LooksView } | { kind: 'failed'; message: string };

// GET /api/looks: the templates the signed-in person may pick (the built-ins,
// their own and the ones shared with them) and the palettes. It is per person and
// the same for every deck, so it is read on its own (D20, SW-50 T4).
export function useLooks(): LooksLoad {
  const [load, setLoad] = useState<LooksLoad>({ kind: 'loading' });
  useEffect(() => {
    let active = true;
    api.get<LooksView>('/api/looks').then(
      (looks) => active && setLoad({ kind: 'loaded', looks }),
      (error: unknown) =>
        active &&
        setLoad({
          kind: 'failed',
          message: error instanceof ApiError ? error.message : "Can't reach SlideWorks.",
        }),
    );
    return () => {
      active = false;
    };
  }, []);
  return load;
}
