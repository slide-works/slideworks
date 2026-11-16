import { useId, type KeyboardEvent } from 'react';

type EmailChipsProps = {
  emails: string[];
  pending: string;
  error: string | null;
  onPending(text: string): void;
  onChange(emails: string[]): void;
};

// The named people as chips. Enter or a comma turns what's typed into a chip,
// Backspace in an empty box removes the last one, and × removes any. Emails are
// only checked for being non-empty: the server is the rule, and its 400
// message shows under the box.
export function EmailChips({ emails, pending, error, onPending, onChange }: EmailChipsProps) {
  const errorId = useId();

  const add = (text: string) => {
    const email = text.trim();
    onPending('');
    if (email.length === 0 || emails.includes(email)) return;
    onChange([...emails, email]);
  };

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter' || event.key === ',') {
      event.preventDefault();
      add(pending);
    } else if (event.key === 'Backspace' && pending === '' && emails.length > 0) {
      onChange(emails.slice(0, -1));
    }
  };

  return (
    <div className="mt-2 grid gap-1">
      <div className="flex flex-wrap gap-1.5 rounded-md border border-edge-strong bg-panel p-1.5 focus-within:ring-1 focus-within:ring-accent">
        <ul aria-label="People who can open it" className="contents">
          {emails.map((email) => (
            <li
              key={email}
              data-testid="sharing-chip"
              className="inline-flex items-center gap-1 rounded-full bg-panel-2 py-0.5 pr-1 pl-2.5 text-meta text-ink"
            >
              <span data-testid="sharing-chip-email">{email}</span>
              <button
                type="button"
                data-testid="sharing-chip-remove"
                aria-label={`Remove ${email}`}
                className="grid size-5 place-items-center rounded-full text-ink-muted hover:bg-edge hover:text-ink"
                onClick={() => onChange(emails.filter((other) => other !== email))}
              >
                ×
              </button>
            </li>
          ))}
        </ul>
        <input
          type="text"
          inputMode="email"
          autoComplete="off"
          data-testid="sharing-email-input"
          aria-label="Add email"
          aria-invalid={error !== null}
          aria-describedby={error ? errorId : undefined}
          placeholder="Add email…"
          className="min-w-40 flex-1 bg-transparent px-1 py-0.5 text-meta text-ink outline-none placeholder:text-ink-faint"
          value={pending}
          onChange={(event) => onPending(event.target.value)}
          onKeyDown={onKeyDown}
          onBlur={() => add(pending)}
        />
      </div>
      {error && (
        <p id={errorId} role="alert" data-testid="sharing-error" className="text-meta text-bad">
          {error}
        </p>
      )}
    </div>
  );
}
