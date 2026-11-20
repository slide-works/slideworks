import { useCallback, useEffect, useState } from 'react';
import { templateErrorMessage, templatesApi } from './api';
import type { TemplatesView } from './types';

export type TemplatesLoad =
  | { kind: 'loading' }
  | { kind: 'loaded'; templates: TemplatesView }
  | { kind: 'failed'; message: string };

// The templates page's list, from one GET /api/templates. `reload` reads it again (after an
// archive, a publish or a new draft) and keeps the rows on screen while it does.
export function useTemplates(): { load: TemplatesLoad; reload(): Promise<void> } {
  const [load, setLoad] = useState<TemplatesLoad>({ kind: 'loading' });

  const reload = useCallback(
    () =>
      templatesApi.list().then(
        (templates) => setLoad({ kind: 'loaded', templates }),
        (error: unknown) => setLoad({ kind: 'failed', message: templateErrorMessage(error) }),
      ),
    [],
  );

  useEffect(() => {
    let active = true;
    templatesApi.list().then(
      (templates) => active && setLoad({ kind: 'loaded', templates }),
      (error: unknown) =>
        active && setLoad({ kind: 'failed', message: templateErrorMessage(error) }),
    );
    return () => {
      active = false;
    };
  }, []);

  return { load, reload };
}
