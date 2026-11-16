import { useRef, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiError, api } from '../../api/client';
import type { CreateDeckRequest, CreateDeckResult } from '../../api/types';

type Form = { kind: 'closed' } | { kind: 'open'; saving: boolean; error: string | null };

// "+ New deck": asks for a title, POSTs it to /api/decks and opens the new
// deck's page. Cancel puts focus back on "+ New deck", and a failed create puts
// it back on the title.
export function NewDeckForm() {
  const navigate = useNavigate();
  const [form, setForm] = useState<Form>({ kind: 'closed' });
  const [title, setTitle] = useState('');
  const titleRef = useRef<HTMLInputElement>(null);
  const focusNewDeck = useRef(false);

  if (form.kind === 'closed') {
    return (
      <button
        type="button"
        ref={(button) => {
          if (button && focusNewDeck.current) {
            focusNewDeck.current = false;
            button.focus();
          }
        }}
        data-testid="new-deck"
        className="btn-primary py-1.5"
        onClick={() => setForm({ kind: 'open', saving: false, error: null })}
      >
        + New deck
      </button>
    );
  }

  const create = async (event: FormEvent) => {
    event.preventDefault();
    setForm({ kind: 'open', saving: true, error: null });
    try {
      const request: CreateDeckRequest = { title };
      const created = await api.post<CreateDeckResult>('/api/decks', request);
      navigate(`/decks/${created.id}`);
    } catch (error) {
      const message = error instanceof ApiError ? error.message : "Can't reach SlideWorks.";
      setForm({ kind: 'open', saving: false, error: message });
      titleRef.current?.focus();
    }
  };

  return (
    <form
      data-testid="new-deck-form"
      className="flex w-full flex-wrap items-center gap-2 sm:w-auto"
      onSubmit={create}
    >
      <input
        ref={titleRef}
        autoFocus
        required
        aria-label="Deck title"
        aria-invalid={form.error !== null}
        aria-describedby={form.error ? 'new-deck-error' : undefined}
        placeholder="Deck title"
        data-testid="new-deck-title"
        className="field min-w-0 flex-1 sm:w-56"
        value={title}
        onChange={(event) => setTitle(event.target.value)}
      />
      <button
        type="submit"
        data-testid="new-deck-create"
        className="btn-primary py-1.5"
        disabled={form.saving}
      >
        Create
      </button>
      <button
        type="button"
        data-testid="new-deck-cancel"
        className="btn-ghost"
        onClick={() => {
          focusNewDeck.current = true;
          setForm({ kind: 'closed' });
        }}
      >
        Cancel
      </button>
      {form.error && (
        <p
          id="new-deck-error"
          role="alert"
          data-testid="new-deck-error"
          className="w-full text-meta text-bad"
        >
          {form.error}
        </p>
      )}
    </form>
  );
}
