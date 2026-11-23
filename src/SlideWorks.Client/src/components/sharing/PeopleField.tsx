import { useId, type KeyboardEvent, type RefObject } from 'react';

export const FULL_EMAIL = /^[^\s@,;]+@[^\s@,;]+\.[^\s@,;]+$/;

type PeopleFieldProps = {
  value: string;
  inputRef: RefObject<HTMLInputElement | null>;
  onChange(text: string): void;
  onAdd(): void;
};

// One input and an Add button. It never suggests anyone (D32): anything that is not a full address only shows
// the same hint, whether or not an account matches it.
export function PeopleField({ value, inputRef, onChange, onAdd }: PeopleFieldProps) {
  const hintId = useId();
  const partial = value.trim() !== '' && !FULL_EMAIL.test(value.trim());

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key !== 'Enter') return;
    event.preventDefault();
    onAdd();
  };

  return (
    <div className="grid gap-1">
      <div className="flex gap-2">
        <input
          ref={inputRef}
          type="text"
          inputMode="email"
          autoComplete="off"
          data-testid="sharing-email-input"
          aria-label="Add someone by their full email"
          aria-describedby={partial ? hintId : undefined}
          placeholder="Add someone by their full email"
          className="min-w-0 flex-1 rounded-md border border-edge-strong bg-panel px-2.5 py-1.5 text-meta text-ink placeholder:text-ink-faint"
          value={value}
          onChange={(event) => onChange(event.target.value)}
          onKeyDown={onKeyDown}
        />
        <button
          type="button"
          data-testid="sharing-add-person"
          className="btn-ghost"
          onClick={onAdd}
        >
          Add
        </button>
      </div>
      {partial && (
        <p
          id={hintId}
          role="status"
          data-testid="sharing-email-hint"
          className="text-meta text-ink-muted"
        >
          Type their full email to share this deck with them.
        </p>
      )}
    </div>
  );
}
