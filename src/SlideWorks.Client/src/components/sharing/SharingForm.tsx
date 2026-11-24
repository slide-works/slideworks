import { useEffect, useRef, useState, type FormEvent } from 'react';
import { ApiError, api } from '../../api/client';
import type { SharingLevel, SharingPeopleRequest, SharingView } from '../../api/types';
import { CopyLinkRow } from './CopyLinkRow';
import { LevelOption } from './LevelOption';
import { FULL_EMAIL } from './PeopleField';
import { PeopleSection } from './PeopleSection';
import type { PersonEntry } from './PersonRow';

const LEVELS: { level: SharingLevel; title: string; hint: string }[] = [
  {
    level: 'private',
    title: 'Private',
    hint: "Only you, and agents signed in as you. People below can't open it until you switch back.",
  },
  {
    level: 'link',
    title: 'Anyone with the link',
    hint: 'Anyone can view and present. People below with Can edit can still edit.',
  },
  { level: 'named', title: 'Only people below', hint: 'They sign in with these emails.' },
];

// The server's 400 messages start with the field they're about.
const PEOPLE_ERROR_PREFIX = 'people:';

const LIMIT_NOTE =
  'Someone added to several of your decks counts once. Nothing was saved and no emails went out.';

type SharingFormProps = {
  deckId: string;
  view: SharingView;
  onCancel(): void;
  onSaved(view: SharingView): void;
};

// The loaded dialog: the three levels, the People section, the agents note, the link and Save. Save PUTs
// { level, people }, adding a full email still typed; a partial one stops Save. A 400's message shows under the
// people field and a 429 shows the server's text; either way nothing moves and focus goes back to a control.
export function SharingForm({ deckId, view, onCancel, onSaved }: SharingFormProps) {
  const [level, setLevel] = useState(view.level);
  const [people, setPeople] = useState<PersonEntry[]>(() =>
    view.people.map((person) => ({ ...person, isNew: false })),
  );
  const [pending, setPending] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [limited, setLimited] = useState(false);
  const [failure, setFailure] = useState<{ target: 'input' | 'save' } | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const saveRef = useRef<HTMLButtonElement>(null);

  // Save is disabled while the call runs, so after a failure focus is put back once it is enabled again.
  useEffect(() => {
    if (failure) (failure.target === 'input' ? inputRef : saveRef).current?.focus();
  }, [failure]);

  const save = async (event: FormEvent) => {
    event.preventDefault();
    if (pending.trim() !== '' && !FULL_EMAIL.test(pending.trim())) {
      inputRef.current?.focus();
      return;
    }
    const typed = pending.trim().toLowerCase();
    const typedIsNew =
      FULL_EMAIL.test(typed) &&
      typed !== view.owner.email.toLowerCase() &&
      !people.some((person) => person.email === typed);
    const all = typedIsNew
      ? [
          ...people,
          { email: typed, name: null, avatar: null, access: 'read' as const, isNew: true },
        ]
      : people;
    const request: SharingPeopleRequest = {
      level,
      people: all.map(({ email, access }) => ({ email, access })),
    };
    setSaving(true);
    setError(null);
    setLimited(false);
    try {
      const saved = await api.put<SharingView>(
        `/api/decks/${encodeURIComponent(deckId)}/sharing`,
        request,
      );
      onSaved(saved);
    } catch (cause) {
      setSaving(false);
      // Nothing moves on a failure: the typed email stays in the field and the list is as it was.
      let target: 'input' | 'save' = 'save';
      if (cause instanceof ApiError) {
        setError(cause.message);
        setLimited(cause.status === 429);
        if (cause.message.startsWith(PEOPLE_ERROR_PREFIX)) target = 'input';
      } else {
        setError("Can't reach SlideWorks.");
      }
      setFailure({ target });
    }
  };

  const peopleError = error?.startsWith(PEOPLE_ERROR_PREFIX) ? error : null;
  const levelError = error !== null && peopleError === null && !limited ? error : null;

  return (
    <form onSubmit={save}>
      <fieldset className="grid gap-2 px-[18px] pt-2.5 pb-3">
        <legend className="sr-only">Who can open it</legend>
        {LEVELS.map((option) => (
          <LevelOption
            key={option.level}
            {...option}
            checked={level === option.level}
            autoFocus={view.level === option.level}
            onPick={setLevel}
          />
        ))}
        {levelError && (
          <p role="alert" data-testid="sharing-error" className="text-meta text-bad">
            {levelError}
          </p>
        )}
      </fieldset>

      <PeopleSection
        owner={view.owner}
        people={people}
        level={level}
        error={peopleError}
        pending={pending}
        inputRef={inputRef}
        onPending={setPending}
        onChange={setPeople}
      />

      {limited && error && (
        <p
          role="alert"
          data-testid="sharing-limit-error"
          className="mx-[18px] mb-3 grid gap-0.5 text-meta text-bad"
        >
          <b>Not shared.</b>
          <span data-testid="sharing-limit-message">{error}</span>
          <span>{LIMIT_NOTE}</span>
        </p>
      )}

      <p
        data-testid="sharing-agents-note"
        className="mx-[18px] mb-3 rounded-md bg-violet-50 px-3 py-2 text-[12.5px] text-violet-800"
      >
        Agents signed in as you can change this too, with <code>set_sharing</code>. People with Can
        edit can use their own agents on this deck. Every change appears in the deck's activity list
        with the agent's name.
        {view.lastChange && (
          <>
            {' '}
            <span data-testid="sharing-last-change">
              <b>Last change:</b> {view.lastChange.actor}, {view.lastChange.atLabel}.
            </span>
          </>
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
          ref={saveRef}
        >
          Save
        </button>
      </footer>
    </form>
  );
}
