import { useState, type FormEvent } from 'react';
import { ApiError, api } from '../../api/client';
import type { SharingLevel, SharingRequest, SharingView } from '../../api/types';
import { CopyLinkRow } from './CopyLinkRow';
import { EmailChips } from './EmailChips';
import { LevelOption } from './LevelOption';

const LEVELS: { level: SharingLevel; title: string; hint: string }[] = [
  { level: 'private', title: 'Private', hint: 'Only you, and agents signed in as you.' },
  {
    level: 'link',
    title: 'Anyone with the link',
    hint: 'Read and present only. The link is hard to guess, but anyone you forward it to can open it.',
  },
  { level: 'named', title: 'Named people', hint: 'They sign in with these emails.' },
];

// The server's 400 messages start with the field they're about.
const VIEWERS_ERROR_PREFIX = 'viewers:';

type SharingFormProps = {
  deckId: string;
  view: SharingView;
  onCancel(): void;
  onSaved(view: SharingView): void;
};

// The loaded dialog: the three levels, the named people, the agents note, the
// link and Save. Save PUTs { level, viewers }, counting an email still being
// typed, and a 400's message shows under the field it's about.
export function SharingForm({ deckId, view, onCancel, onSaved }: SharingFormProps) {
  const [level, setLevel] = useState(view.level);
  const [viewers, setViewers] = useState(view.viewers);
  const [pending, setPending] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const save = async (event: FormEvent) => {
    event.preventDefault();
    const typed = pending.trim();
    const request: SharingRequest = {
      level,
      viewers: typed && !viewers.includes(typed) ? [...viewers, typed] : viewers,
    };
    setViewers(request.viewers);
    setPending('');
    setSaving(true);
    setError(null);
    try {
      const saved = await api.put<SharingView>(
        `/api/decks/${encodeURIComponent(deckId)}/sharing`,
        request,
      );
      onSaved(saved);
    } catch (failure) {
      setSaving(false);
      setError(failure instanceof ApiError ? failure.message : "Can't reach SlideWorks.");
    }
  };

  const viewersError = level === 'named' && error?.startsWith(VIEWERS_ERROR_PREFIX) ? error : null;
  const levelError = error !== null && viewersError === null ? error : null;

  return (
    <form onSubmit={save}>
      <fieldset className="grid gap-2 px-[18px] pt-2.5 pb-4">
        <legend className="sr-only">Who can open it</legend>
        {LEVELS.map((option) => (
          <LevelOption
            key={option.level}
            {...option}
            checked={level === option.level}
            autoFocus={view.level === option.level}
            onPick={setLevel}
          >
            {option.level === 'named' && (
              <EmailChips
                emails={viewers}
                pending={pending}
                error={viewersError}
                onPending={setPending}
                onChange={setViewers}
              />
            )}
          </LevelOption>
        ))}
        {levelError && (
          <p role="alert" data-testid="sharing-error" className="text-meta text-bad">
            {levelError}
          </p>
        )}
      </fieldset>

      <p
        data-testid="sharing-agents-note"
        className="mx-[18px] mb-3 rounded-md bg-violet-50 px-3 py-2 text-[12.5px] text-violet-800"
      >
        Agents signed in as you can change this too, with <code>set_sharing</code>. Every change
        appears in the deck's activity list with the agent's name.
        {view.lastChange && (
          <span data-testid="sharing-last-change">
            {' '}
            <b>Last change:</b> {view.lastChange.actor}, {view.lastChange.atLabel}.
          </span>
        )}
      </p>

      <CopyLinkRow link={view.link} />

      <footer className="flex flex-wrap items-center justify-end gap-2 border-t border-edge px-[18px] py-3">
        <button
          type="button"
          disabled
          data-testid="sharing-qr-later"
          title="Coming later (SW-5)"
          className="mr-auto cursor-not-allowed rounded-full bg-violet-50 px-3 py-1 text-xs font-medium text-violet-700"
        >
          QR join for an audience · SW-5
        </button>
        <button type="button" data-testid="sharing-cancel" className="btn-ghost" onClick={onCancel}>
          Cancel
        </button>
        <button
          type="submit"
          data-testid="sharing-save"
          className="btn-primary py-1.5"
          disabled={saving}
        >
          Save
        </button>
      </footer>
    </form>
  );
}
