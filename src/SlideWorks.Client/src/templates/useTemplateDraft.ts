import { useCallback, useEffect, useRef, useState } from 'react';
import { templateErrorCode, templateErrorMessage, templatesApi } from './api';
import type { TemplateEditorView, TemplateManifest } from './types';

export type DraftLoad =
  | { kind: 'loading' }
  | { kind: 'loaded'; draft: TemplateEditorView }
  | { kind: 'failed'; message: string };

// Why the last save failed: the refusal's code (null when it was not a refusal) and what to show.
export type DraftSaveError = { code: string | null; message: string };

export type TemplateDraft = {
  load: DraftLoad;
  saving: boolean;
  saveError: DraftSaveError | null;
  // Merges top-level manifest keys into the draft and saves it with the revision last read. A
  // refused or failed save leaves the draft as it was and sets saveError.
  change(patch: Partial<TemplateManifest>): Promise<void>;
  // Reads the editor view again (after an upload or a publish).
  reload(): Promise<void>;
};

// The template editor's one draft: GET /api/templates/{id}, then PUT with the revision on each
// change. Changes are saved one after another, each on top of the answer to the one before, so two
// quick edits never send the same revision.
export function useTemplateDraft(id: string): TemplateDraft {
  const [load, setLoad] = useState<DraftLoad>({ kind: 'loading' });
  const [pending, setPending] = useState(0);
  const [saveError, setSaveError] = useState<DraftSaveError | null>(null);
  // The newest view the server sent for `forId`, and the tail of the queue of saves and reloads.
  const latest = useRef<{ forId: string; draft: TemplateEditorView } | null>(null);
  const queue = useRef<Promise<void>>(Promise.resolve());

  useEffect(() => {
    let active = true;
    latest.current = null;
    templatesApi.get(id).then(
      (draft) => {
        if (!active) return;
        latest.current = { forId: id, draft };
        setLoad({ kind: 'loaded', draft });
      },
      (error: unknown) =>
        active && setLoad({ kind: 'failed', message: templateErrorMessage(error) }),
    );
    return () => {
      active = false;
      setLoad({ kind: 'loading' });
      setSaveError(null);
    };
  }, [id]);

  const accept = useCallback(
    (draft: TemplateEditorView) => {
      if (latest.current?.forId !== id) return;
      latest.current = { forId: id, draft };
      setLoad({ kind: 'loaded', draft });
    },
    [id],
  );

  const enqueue = useCallback((step: () => Promise<void>) => {
    const run = queue.current.then(step);
    queue.current = run;
    return run;
  }, []);

  const change = useCallback(
    (patch: Partial<TemplateManifest>) => {
      // A failure stays on screen until the person changes something again: a queued save that
      // succeeds after it must not take the message away.
      setSaveError(null);
      setPending((count) => count + 1);
      return enqueue(async () => {
        try {
          const current = latest.current;
          if (current?.forId !== id) return;
          const manifest = { ...current.draft.manifest, ...patch };
          accept(await templatesApi.save(id, manifest, current.draft.revision));
        } catch (error) {
          setSaveError({ code: templateErrorCode(error), message: templateErrorMessage(error) });
        } finally {
          setPending((count) => count - 1);
        }
      });
    },
    [id, accept, enqueue],
  );

  const reload = useCallback(
    () =>
      enqueue(async () => {
        try {
          accept(await templatesApi.get(id));
        } catch (error) {
          setSaveError({ code: templateErrorCode(error), message: templateErrorMessage(error) });
        }
      }),
    [id, accept, enqueue],
  );

  return { load, saving: pending > 0, saveError, change, reload };
}
