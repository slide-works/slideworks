import { useId } from 'react';

type Props = {
  checked: boolean;
  confirming: boolean;
  error: string | null;
  onConfirm(): void;
};

// "I have the right to serve these fonts on the web". Once on it stays on: the right is kept on the
// files, and nothing here takes it back.
export function FontRightsTick({ checked, confirming, error, onConfirm }: Props) {
  const id = useId();
  return (
    <div className="grid gap-1 text-meta">
      <label className="flex items-start gap-2 text-ink">
        <input
          type="checkbox"
          className="mt-0.5"
          checked={checked}
          disabled={checked || confirming}
          aria-describedby={`${id}-hint${error === null ? '' : ` ${id}-error`}`}
          aria-invalid={error === null ? undefined : true}
          onChange={() => {
            if (!checked) onConfirm();
          }}
        />
        <span>I have the right to serve these fonts on the web</span>
      </label>
      <p id={`${id}-hint`} className="text-ink-muted">
        {checked
          ? 'Confirmed for every font in this template. This stays on.'
          : 'Fonts are sent to everyone who sees a deck made with this template.'}
      </p>
      {error === null ? null : (
        <p id={`${id}-error`} role="alert" className="text-bad">
          {error}
        </p>
      )}
    </div>
  );
}
