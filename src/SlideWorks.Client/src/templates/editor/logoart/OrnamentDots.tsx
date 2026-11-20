// The palette tokens a slide draws an ornament dot in, and the only names the server accepts.
export const DOT_TOKENS = [
  'accent',
  'accent-2',
  'accent-glow',
  'good',
  'warn',
  'bad',
  'extra-1',
  'extra-2',
  'extra-3',
  'extra-4',
] as const;

type Props = {
  id: string;
  dots: string[];
  error: string | null;
  onToggle(token: string): void;
};

// The ornament dots: the ten drawable colours as toggles; the dots are drawn in the order picked.
export function OrnamentDots({ id, dots, error, onToggle }: Props) {
  return (
    <fieldset className="grid gap-2">
      <legend className="text-meta font-semibold text-ink-muted">Ornament dots</legend>
      <div className="flex flex-wrap gap-2">
        {DOT_TOKENS.map((token) => (
          <button
            key={token}
            id={`${id}-dot-${token}`}
            type="button"
            aria-pressed={dots.includes(token)}
            aria-describedby={error === null ? undefined : `${id}-dots-error`}
            className={`rounded-full border px-2.5 py-0.5 text-meta ${
              dots.includes(token)
                ? 'border-accent bg-accent-soft text-ink'
                : 'border-edge text-ink-muted'
            }`}
            onClick={() => onToggle(token)}
          >
            {token}
          </button>
        ))}
      </div>
      <p className="text-meta text-ink-muted">
        {dots.length === 0 ? 'No dots.' : `Drawn in this order: ${dots.join(', ')}.`}
      </p>
      {error === null ? null : (
        <p id={`${id}-dots-error`} className="text-meta text-bad">
          {error}
        </p>
      )}
    </fieldset>
  );
}
