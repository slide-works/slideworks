import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '../api/client';
import { templateErrorCode, templateErrorMessage, templateErrorPath, templatesApi } from './api';
import type { TemplateEditorView, TemplateManifest } from './types';

export type DraftLoad =
  | { kind: 'loading' }
  | { kind: 'loaded'; draft: TemplateEditorView }
  | { kind: 'failed'; message: string; notFound: boolean };

// Why a save failed: the refusal's code (null when it was not a refusal), the sentence to show,
// and the manifest field the refusal is about when it names one.
export type DraftSaveError = { code: string | null; message: string; path?: string };

// How one change went, for the tab that asked for it: saved, or why not.
export type ChangeOutcome = { ok: true } | ({ ok: false } & DraftSaveError);

const STALE_REVISION = 'stale_revision';
const RELOADED =
  'This template was changed somewhere else, so it has been read again. Check it, then make your change again.';
const NOT_RELOADED =
  'This template was changed somewhere else and could not be read again. Reload the page, then make your change again.';
const NOT_LOADED = 'This template is not open yet. Try again in a moment.';

function saveErrorOf(error: unknown): DraftSaveError {
  const path = templateErrorPath(error);
  return {
    code: templateErrorCode(error),
    message: templateErrorMessage(error),
    ...(path === null ? {} : { path }),
  };
}

export type TemplateDraft = {
  load: DraftLoad;
  saving: boolean;
  saveError: DraftSaveError | null;
  // Merges top-level manifest keys into the draft and saves it with the revision last read, and
  // resolves to how that went. A refused or failed save leaves the draft as it was, sets saveError
  // and resolves `ok: false` with the code, the sentence and the field. After a stale_revision
  // refusal the draft has already been read again when this resolves.
  change(patch: Partial<TemplateManifest>): Promise<ChangeOutcome>;
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
  // The template this hook is open on now, and how many stale_revision refusals it has seen: a
  // change made before a refusal must not save on top of the draft that was read again after it.
  const openId = useRef(id);
  const epoch = useRef(0);

  useEffect(() => {
    let active = true;
    openId.current = id;
    latest.current = null;
    templatesApi.get(id).then(
      (draft) => {
        if (!active) return;
        latest.current = { forId: id, draft };
        setLoad({ kind: 'loaded', draft });
      },
      (error: unknown) =>
        active &&
        setLoad({
          kind: 'failed',
          message: templateErrorMessage(error),
          notFound: error instanceof ApiError && error.status === 404,
        }),
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

  const enqueue = useCallback(<T>(step: () => Promise<T>): Promise<T> => {
    const run = queue.current.then(step);
    queue.current = run.then(
      () => undefined,
      () => undefined,
    );
    return run;
  }, []);

  const change = useCallback(
    (patch: Partial<TemplateManifest>) => {
      // A failure stays on screen until the person changes something again: a queued save that
      // succeeds after it must not take the message away.
      setSaveError(null);
      setPending((count) => count + 1);
      const madeAt = epoch.current;
      return enqueue(async (): Promise<ChangeOutcome> => {
        // A save still in flight for another template must never put its error on this one.
        const report = (failed: DraftSaveError) => {
          if (openId.current === id) setSaveError(failed);
        };
        try {
          if (madeAt !== epoch.current) {
            // Queued behind a stale_revision refusal: the draft was read again, so this change
            // is dropped, not saved on top of it.
            return { ok: false, code: STALE_REVISION, message: RELOADED };
          }
          const current = latest.current;
          if (current?.forId !== id) return { ok: false, code: null, message: NOT_LOADED };
          const manifest = { ...current.draft.manifest, ...patch };
          accept(await templatesApi.save(id, manifest, current.draft.revision));
          return { ok: true };
        } catch (error) {
          let failed = saveErrorOf(error);
          if (failed.code === STALE_REVISION) {
            epoch.current += 1;
            // Every later save would be refused too, so read the draft again here, inside the
            // queue, before the next queued change saves on top of it.
            const reloaded = await templatesApi.get(id).then(
              (draft) => (accept(draft), true),
              () => false,
            );
            failed = { code: STALE_REVISION, message: reloaded ? RELOADED : NOT_RELOADED };
          }
          report(failed);
          return { ok: false, ...failed };
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
          if (openId.current === id) setSaveError(saveErrorOf(error));
        }
      }),
    [id, accept, enqueue],
  );

  return { load, saving: pending > 0, saveError, change, reload };
}
