import { useId, type ReactNode } from 'react';
import type { SharingLevel } from '../../api/types';

type LevelOptionProps = {
  level: SharingLevel;
  title: string;
  hint: string;
  checked: boolean;
  autoFocus: boolean;
  onPick(level: SharingLevel): void;
  children?: ReactNode;
};

// One sharing level as a card with its radio. The checked card is outlined in
// the accent colour; its children (the email chips) show under it.
export function LevelOption({
  level,
  title,
  hint,
  checked,
  autoFocus,
  onPick,
  children,
}: LevelOptionProps) {
  const hintId = useId();
  return (
    <div
      data-testid={`sharing-level-${level}`}
      className={`grid grid-cols-[18px_minmax(0,1fr)] gap-2.5 rounded-lg border px-3 py-2.5 ${checked ? 'border-accent bg-accent-soft/50' : 'border-edge'}`}
    >
      <input
        type="radio"
        data-testid="sharing-radio"
        name="sharing-level"
        value={level}
        aria-labelledby={`${hintId}-title`}
        aria-describedby={hintId}
        checked={checked}
        autoFocus={autoFocus}
        onChange={() => onPick(level)}
        className="mt-0.5 size-4 accent-accent"
        id={`${hintId}-radio`}
      />
      <div className="min-w-0">
        <label htmlFor={`${hintId}-radio`} className="block cursor-pointer text-sm">
          <b id={`${hintId}-title`} className="block font-semibold text-ink">
            {title}
          </b>
          <span id={hintId} className="text-meta text-ink-muted">
            {hint}
          </span>
        </label>
        {checked && children}
      </div>
    </div>
  );
}
