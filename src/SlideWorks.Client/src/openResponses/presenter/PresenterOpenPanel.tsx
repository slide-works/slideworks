import { usePresenterOpenKeys } from '../../presentation/presenter/usePresenterKeys';
import type { OpenResponseControlView, OpenResponsePhase } from '../types';
import { OPEN_PRESENTER_IDS as IDS } from './testIds';
import { useKeptFocus } from './useKeptFocus';
import { useOpenResponsePanel } from './useOpenResponseControl';

type Props = {
  deckId: string;
  // Something is open on top of the presenter window: the open-response keys do nothing.
  overlayOpen?: boolean;
};

const PHASE: Readonly<Record<OpenResponsePhase, string>> = {
  draft: 'Draft',
  open: 'Open',
  closed: 'Closed',
};

const PHASE_DOT: Readonly<Record<OpenResponsePhase, string>> = {
  draft: 'bg-[#94a3b8]',
  open: 'bg-[#4ade80]',
  closed: 'bg-[#8ea5ff]',
};

const RESULTS = {
  live: 'Live',
  afterAnswer: 'After answering',
  afterClose: 'After close',
} as const;
const OPENS = { arrival: 'Opens on arrival', manual: 'Opens when you open it' } as const;

const BUTTON =
  'min-h-9 rounded-md px-3 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-[#8ea5ff] aria-disabled:opacity-60 forced-colors:border-2 forced-colors:border-[ButtonText]';

function kindOf(kind: OpenResponseControlView['kind']): string {
  return kind === 'word-cloud' ? 'Word cloud' : 'Open text';
}

function focusedEntry(root: HTMLElement | null): number | null {
  const active = document.activeElement;
  if (!(active instanceof HTMLElement) || root === null || !root.contains(active)) return null;
  const row = active.closest('[data-entry-number]');
  if (!(row instanceof HTMLElement)) return null;
  const number = Number(row.dataset.entryNumber);
  return Number.isInteger(number) && number > 0 ? number : null;
}

// The open-response panel beside the poll panel. P opens a draft or closes an open round. H hides the focused entry.
// R does nothing (D14). New round is a button with no key. There is no reveal control.
export function PresenterOpenPanel({ deckId, overlayOpen = false }: Props) {
  const value = useOpenResponsePanel(deckId);
  const { control, error } = value;
  const { root, busy, run } = useKeptFocus();

  const used = () =>
    document.activeElement instanceof HTMLElement ? document.activeElement : null;

  const hideFocused = () => {
    if (control === null) return;
    const number = focusedEntry(root.current);
    if (number === null) return;
    const entry = control.entries.find((item) => item.number === number);
    if (entry === undefined || entry.hidden) return;
    void run(() => value.hide(number), used());
  };

  usePresenterOpenKeys(
    control === null
      ? null
      : {
          toggle:
            control.phase === 'open'
              ? () => void run(() => value.close(), used())
              : control.phase === 'draft'
                ? () => void run(() => value.open(), used())
                : undefined,
          hide: hideFocused,
        },
    overlayOpen,
  );

  if (control === null && error === null) return null;

  const name = control === null ? 'Open response' : kindOf(control.kind);

  return (
    <section
      ref={root}
      data-testid={IDS.panel}
      aria-label={name}
      tabIndex={-1}
      className="grid min-w-0 content-start gap-2 outline-none"
    >
      <h2 className="text-[11px] font-semibold tracking-[0.12em] text-[#94a3b8] uppercase">
        {name}
      </h2>
      <div className="grid gap-3 rounded-lg border border-[#1f2937] bg-[#111827] px-4 py-3">
        {control !== null && (
          <>
            <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
              <span
                data-testid={IDS.phase}
                className="inline-flex items-center gap-1.5 rounded-full bg-white/5 px-2 py-0.5 text-[11px] font-semibold tracking-[0.08em] uppercase"
              >
                <span
                  className={`size-1.5 rounded-full ${PHASE_DOT[control.phase]}`}
                  aria-hidden="true"
                />
                {PHASE[control.phase]}
              </span>
              <span className="font-mono text-xs text-[#94a3b8]">round {control.number}</span>
              <span className="ml-auto text-xs text-[#94a3b8]">
                {RESULTS[control.results]} · {OPENS[control.opens]}
              </span>
            </div>
            <p data-testid={IDS.guests}>
              <span className="text-2xl font-bold tabular-nums">{control.guests}</span>{' '}
              {control.guests === 1 ? 'guest submitted' : 'guests submitted'}
            </p>
            <ul aria-label="Entries" className="grid gap-1">
              {control.entries.map((entry) => (
                <li
                  key={entry.number}
                  data-testid={IDS.entry(entry.number)}
                  data-entry-number={entry.number}
                  className="flex items-center gap-2 rounded-md bg-white/5 px-2 py-1"
                >
                  <span className="min-w-0 flex-1 truncate">
                    {entry.text}
                    {entry.hidden ? ' · Hidden' : ''}
                  </span>
                  {entry.count !== undefined && (
                    <span className="font-mono text-xs text-[#94a3b8]">{entry.count}</span>
                  )}
                  <button
                    type="button"
                    data-testid={entry.hidden ? IDS.restore(entry.number) : IDS.hide(entry.number)}
                    aria-label={entry.hidden ? `Restore ${entry.text}` : `Hide ${entry.text}`}
                    aria-disabled={busy || undefined}
                    className={`${BUTTON} bg-white/10 text-[#e2e8f0]`}
                    onClick={(event) =>
                      void run(
                        () =>
                          entry.hidden ? value.restore(entry.number) : value.hide(entry.number),
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
                  className={`${BUTTON} bg-[#2f5bea] text-white`}
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
                  className={`${BUTTON} bg-[#4a1d24] text-[#fecaca]`}
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
                  className={`${BUTTON} bg-white/10 text-[#e2e8f0]`}
                  onClick={(event) => void run(() => value.newRound(), event.currentTarget)}
                >
                  New round
                </button>
              )}
            </div>
            <p className="text-[11px] text-[#94a3b8]">
              <kbd className="rounded border border-[#334155] px-1 font-mono">P</kbd> open or close
              · <kbd className="rounded border border-[#334155] px-1 font-mono">H</kbd> hide the
              focused entry
            </p>
          </>
        )}
        {error !== null && (
          <p
            data-testid={IDS.error}
            role="alert"
            className="rounded-md bg-[#c0362c]/25 px-3 py-2 text-sm text-[#fca5a5]"
          >
            {error}
          </p>
        )}
      </div>
    </section>
  );
}
