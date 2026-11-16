import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Note } from '../api/types';

// The sample page: one request for what the screen shows, one per action.
// Replace it with the real thing.
export function NotesPage() {
  const [notes, setNotes] = useState<Note[] | null>(null);
  const [text, setText] = useState('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api
      .get<Note[]>('/api/notes')
      .then(setNotes)
      .catch((e: Error) => setError(e.message));
  }, []);

  const add = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!text.trim()) return;
    try {
      const note = await api.post<Note>('/api/notes', { text });
      setNotes((current) => [note, ...(current ?? [])]);
      setText('');
      setError(null);
    } catch (e) {
      setError((e as Error).message);
    }
  };

  const remove = async (id: number) => {
    try {
      await api.del(`/api/notes/${id}`);
      setNotes((current) => (current ?? []).filter((n) => n.id !== id));
    } catch (e) {
      setError((e as Error).message);
    }
  };

  return (
    <section className="flex flex-col gap-6">
      <h2 className="page-title" data-testid="notes-heading">
        Notes
      </h2>
      <form className="flex gap-2" onSubmit={add}>
        <label className="sr-only" htmlFor="note-text">
          New note
        </label>
        <input
          className="field flex-1"
          id="note-text"
          data-testid="note-input"
          placeholder="Write a note"
          value={text}
          onChange={(e) => setText(e.target.value)}
        />
        <button className="btn-signal" type="submit" data-testid="note-add">
          Add
        </button>
      </form>
      {error && (
        <p role="alert" className="text-meta text-signal" data-testid="notes-error">
          {error}
        </p>
      )}
      {notes === null ? (
        <p className="text-meta text-ink-muted">Loading…</p>
      ) : notes.length === 0 ? (
        <p className="text-meta text-ink-muted">No notes yet.</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {notes.map((note) => (
            <li
              key={note.id}
              className="panel flex items-baseline gap-3 px-4 py-3"
              data-testid="note-item"
            >
              <span className="flex-1 text-ink">{note.text}</span>
              <span className="font-mono text-meta text-ink-faint">{note.created}</span>
              <button
                className="btn-ghost"
                data-testid="note-delete"
                type="button"
                aria-label={`Delete ${note.text}`}
                onClick={() => remove(note.id)}
              >
                Delete
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
