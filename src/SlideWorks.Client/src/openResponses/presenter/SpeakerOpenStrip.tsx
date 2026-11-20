import type { OpenResponsePhase } from '../types';
import { OPEN_PRESENTER_IDS as IDS } from './testIds';
import { useKeptFocus } from './useKeptFocus';
import { useOpenResponsePanel } from './useOpenResponseControl';

const PHASE: Readonly<Record<OpenResponsePhase, string>> = {
  draft: 'Draft',
  open: 'Open',
  closed: 'Closed',
};

const BUTTON =
  'grid min-h-12 min-w-11 place-items-center rounded-[9px] border px-2 text-center text-xs leading-tight font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-300 aria-disabled:opacity-60 forced-colors:border-2';

// The open-response strip on the presenter's phone, beside the poll strip. The same controls as the panel, with no keys.
// Nothing, and no space, on a slide that is not a word cloud or open text.
export function SpeakerOpenStrip({ deckId }: { deckId: string }) {
  const value = useOpenResponsePanel(deckId);
  const { control, error } = value;
  const { root, busy, run } = useKeptFocus();

  if (control === null && error === null) return null;

  const name = control?.kind === 'open-text' ? 'Open text' : 'Word cloud';

  return (
    <section
      ref={root}
      tabIndex={-1}
      data-testid={IDS.strip}
      aria-label={control ? `${name}, ${control.phase}` : name}
      className="mx-4 mt-2 grid gap-2 rounded-xl border border-white/10 bg-white/5 px-3 py-2 outline-none"
    >
      {control !== null && (
        <>
          <p className="flex items-baseline gap-2 text-sm">
            <span data-testid={IDS.phase}>{PHASE[control.phase]}</span>
            <span data-testid={IDS.guests} className="text-slate-400">
              {control.guests === 1 ? '1 guest submitted' : `${control.guests} guests submitted`}
            </span>
          </p>
          <ul aria-label="Entries" className="grid gap-1">
            {control.entries.map((entry) => (
              <li key={entry.number} data-entry-number={entry.number} className="flex gap-2">
                <span className="min-w-0 flex-1 truncate text-sm">
                  {entry.text}
                  {entry.hidden ? ' · Hidden' : ''}
                </span>
                <button
                  type="button"
                  data-testid={entry.hidden ? IDS.restore(entry.number) : IDS.hide(entry.number)}
                  aria-label={entry.hidden ? `Restore ${entry.text}` : `Hide ${entry.text}`}
                  aria-disabled={busy || undefined}
                  className={`${BUTTON} border-white/10 bg-white/10 text-slate-100`}
                  onClick={(event) =>
                    void run(
                      () => (entry.hidden ? value.restore(entry.number) : value.hide(entry.number)),
                      event.currentTarget,
                    )
                  }
                >
                  {entry.hidden ? 'Restore' : 'Hide'}
                </button>
              </li>
            ))}
          </ul>
          <div className="flex flex-wrap gap-2">
            {control.phase === 'draft' && (
              <button
                type="button"
                data-testid={IDS.open}
                aria-label={`Open ${name}`}
                aria-disabled={busy || undefined}
                className={`${BUTTON} border-transparent bg-[#3b5bdb] text-white`}
                onClick={(event) => void run(() => value.open(), event.currentTarget)}
              >
                Open
              </button>
            )}
            {control.phase === 'open' && (
              <button
                type="button"
                data-testid={IDS.close}
                aria-label={`Close ${name}`}
                aria-disabled={busy || undefined}
                className={`${BUTTON} border-red-300/40 bg-red-600/25 text-red-200`}
                onClick={(event) => void run(() => value.close(), event.currentTarget)}
              >
                Close
              </button>
            )}
            {control.phase === 'closed' && (
              <button
                type="button"
                data-testid={IDS.newRound}
                aria-label={`New round: ${name}`}
                aria-disabled={busy || undefined}
                className={`${BUTTON} border-white/10 bg-white/10 text-slate-100`}
                onClick={(event) => void run(() => value.newRound(), event.currentTarget)}
              >
                New round
              </button>
            )}
          </div>
        </>
      )}
      {error !== null && (
        <p data-testid={IDS.error} role="alert" className="text-sm text-red-300">
          {error}
        </p>
      )}
    </section>
  );
}
