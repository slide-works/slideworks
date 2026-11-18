import { useCallback, useEffect, useId, useRef, useState } from 'react';
import type { SharingLevel } from '../api/types';
import { EmailChips } from '../components/sharing/EmailChips';
import { LevelOption } from '../components/sharing/LevelOption';
import { ModalDialog } from '../components/sharing/ModalDialog';
import { useCopyText } from '../components/useCopyText';
import { ApiError } from '../api/client';
import { templatesApi } from './api';
import { TEMPLATES_TEST_IDS } from './testIds';
import type { TemplateSharingUpdate, TemplateSharingView } from './types';

// Frozen: the templates page and the editor both open this dialog. It is rendered only while
// open, and whoever opens it puts focus back on the opener in onClose, as SharingDialog's does.
export type TemplateShareDialogProps = { templateId: string; name: string; onClose(): void };

const LEVELS: { level: SharingLevel; title: string; hint: string }[] = [
  { level: 'private', title: 'Private', hint: 'Only you can make decks with it.' },
  { level: 'named', title: 'Named people', hint: 'People you list by email can use it.' },
  {
    level: 'link',
    title: 'Anyone with the link',
    hint: 'Anyone signed in who opens the link can add it to their templates.',
  },
];

const CHANGE_FAILED = 'Could not change sharing. Try again.';
const COPY_FAILED = 'Could not copy. Select the link and copy it.';

type Load = { kind: 'loading' } | { kind: 'loaded' } | { kind: 'failed' };

// What the dialog shows: the level and the people, which run ahead of the server while a change is
// on its way and go back to the server's last answer when it fails.
type Shown = { level: SharingLevel; viewers: string[] };

// Who can use a template (PNG 06): private, people you name, or anyone with the link. Every
// change saves at once. Changing only the level sends no viewer list, so the server keeps it:
// once more than 50 people have used a link that list would be refused, and the owner could never
// switch the level.
export function TemplateShareDialog({ templateId, name, onClose }: TemplateShareDialogProps) {
  const titleId = useId();
  const noteId = useId();
  const bodyRef = useRef<HTMLDivElement>(null);
  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [link, setLink] = useState('');
  // The level the server named when the dialog loaded: the radio that takes focus.
  const [opened, setOpened] = useState<SharingLevel | null>(null);
  const [shown, setShown] = useState<Shown>({ level: 'private', viewers: [] });
  const [pending, setPending] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [failures, setFailures] = useState(0);
  const [attempt, setAttempt] = useState(0);
  // The server's last answer, and the saves in order (a second change waits for the first).
  const server = useRef<TemplateSharingView | null>(null);
  const queue = useRef<Promise<boolean>>(Promise.resolve(true));
  const mounted = useRef(true);

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  useEffect(() => {
    let active = true;
    templatesApi.sharing(templateId).then(
      (view) => {
        if (!active) return;
        server.current = view;
        setLink(view.link);
        setOpened(view.level);
        setShown({ level: view.level, viewers: view.viewers });
        setLoad({ kind: 'loaded' });
      },
      () => active && setLoad({ kind: 'failed' }),
    );
    return () => {
      active = false;
    };
  }, [templateId, attempt]);

  // After a failed save, focus stays where the person was; when that control is gone (a removed
  // chip), it goes to the people box or the chosen level.
  useEffect(() => {
    const body = bodyRef.current;
    if (failures === 0 || !body || body.contains(document.activeElement)) return;
    body
      .querySelector<HTMLElement>(
        '[data-testid="sharing-email-input"], input[type="radio"]:checked',
      )
      ?.focus();
  }, [failures]);

  const save = useCallback(
    (update: TemplateSharingUpdate, next: Shown) => {
      setShown(next);
      setError(null);
      const run = async () => {
        try {
          const saved = await templatesApi.setSharing(templateId, update);
          server.current = saved;
          if (mounted.current) {
            setLink(saved.link);
            setShown({ level: saved.level, viewers: saved.viewers });
          }
          return true;
        } catch (failure) {
          const last = server.current;
          if (mounted.current) {
            if (last) setShown({ level: last.level, viewers: last.viewers });
            setError(failureMessage(failure));
            setFailures((count) => count + 1);
          }
          return false;
        }
      };
      queue.current = queue.current.then(run);
    },
    [templateId],
  );

  const done = async () => {
    // A change still on its way finishes first; if it fails the dialog stays to say so.
    if (await queue.current) onClose();
  };

  const pick = (level: SharingLevel) => {
    if (level === shown.level) return;
    // Only the level: no viewers, so the server keeps its list whatever its size.
    save({ level }, { level, viewers: shown.viewers });
  };

  const editViewers = (viewers: string[]) =>
    save({ level: shown.level, viewers }, { ...shown, viewers });

  return (
    <ModalDialog
      labelledBy={titleId}
      describedBy={noteId}
      testId={TEMPLATES_TEST_IDS.shareDialog}
      onClose={() => void done()}
    >
      <header className="px-[18px] pt-4 pb-1.5">
        <h2 id={titleId} className="text-base font-semibold text-ink">
          Share &quot;{name}&quot;
        </h2>
      </header>

      <div ref={bodyRef}>
        {load.kind === 'loaded' ? (
          <>
            <fieldset className="grid gap-2 px-[18px] pt-2.5 pb-4">
              <legend className="sr-only">Who can use it</legend>
              {LEVELS.map((option) => (
                <LevelOption
                  key={option.level}
                  {...option}
                  checked={shown.level === option.level}
                  autoFocus={opened === option.level}
                  onPick={pick}
                >
                  {option.level === 'named' && (
                    <EmailChips
                      emails={shown.viewers}
                      pending={pending}
                      error={null}
                      onPending={setPending}
                      onChange={editViewers}
                    />
                  )}
                </LevelOption>
              ))}
              {error !== null && (
                <p role="alert" data-testid="template-share-error" className="text-meta text-bad">
                  {error}
                </p>
              )}
            </fieldset>
            {shown.level === 'link' && <LinkRow link={link} />}
          </>
        ) : (
          <div className="grid gap-3 px-[18px] pt-2.5 pb-4">
            {load.kind === 'failed' ? (
              <>
                <p
                  role="alert"
                  data-testid="template-share-load-error"
                  className="text-meta text-bad"
                >
                  Could not load sharing for {name}.
                </p>
                <button
                  type="button"
                  data-testid="template-share-retry"
                  className="btn-ghost justify-self-start"
                  onClick={() => {
                    setLoad({ kind: 'loading' });
                    setAttempt((count) => count + 1);
                  }}
                >
                  Retry
                </button>
              </>
            ) : (
              <p role="status" className="text-meta text-ink-muted">
                Loading…
              </p>
            )}
          </div>
        )}
      </div>

      <footer className="flex items-center gap-3 border-t border-edge px-[18px] py-3">
        <p
          id={noteId}
          data-testid="template-share-note"
          className="flex-1 text-[12.5px] text-ink-muted"
        >
          People you share with can use the template, not edit it. Anyone who can see a deck sees it
          in this template, shared or not.
        </p>
        <button
          type="button"
          data-testid="template-share-done"
          className="btn-primary py-1.5"
          onClick={() => void done()}
        >
          Done
        </button>
      </footer>
    </ModalDialog>
  );
}

// The server's sentence when it sent one, else a fixed one. Never a code.
function failureMessage(failure: unknown): string {
  return failure instanceof ApiError && failure.detail ? failure.detail : CHANGE_FAILED;
}

// The link on the grey band, with a copy button; a failed copy selects the link and says so.
function LinkRow({ link }: { link: string }) {
  const linkRef = useRef<HTMLElement>(null);
  const copy = useCopyText(linkRef);
  return (
    <div className="border-t border-edge bg-panel-2 px-[18px] py-2.5">
      <div className="flex items-center gap-2">
        <code
          ref={linkRef}
          data-testid="sharing-link"
          className="min-w-0 flex-1 truncate font-mono text-[12.5px] text-ink select-all"
        >
          {link}
        </code>
        <button
          type="button"
          data-testid="sharing-copy-link"
          className="btn-ghost bg-panel text-ink"
          onClick={() => copy.copy(link)}
        >
          {copy.state === 'copied' ? 'Copied' : 'Copy link'}
        </button>
      </div>
      <p
        role="status"
        data-testid="template-share-copy-status"
        className="text-meta text-ink-muted empty:hidden"
      >
        {copy.state === 'copied' ? 'Copied' : copy.state === 'failed' ? COPY_FAILED : ''}
      </p>
    </div>
  );
}
