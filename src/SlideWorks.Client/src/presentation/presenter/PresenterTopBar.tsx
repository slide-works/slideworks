import { isValidElement, type ReactNode } from 'react';
import type { PresenterView } from '../../api/types';
import { formatClock, formatElapsed } from '../format';
import type { PresentationConnection, PresentationState, TimerCommand } from '../types';

type Props = {
  view: PresenterView;
  state: PresentationState;
  elapsedMs: number;
  clockMinutes: number;
  onTimer(cmd: TimerCommand): void;
  onReopenAudience(): void;
  // A chip another feature puts at the start of the right-hand group (P10). Its wrapper renders only when given.
  liveChip?: ReactNode;
  // The Reading in counter (SW-886 K11), drawn just before the live chip. Its wrapper renders only when given.
  readingIn?: ReactNode;
};

const FIVE_MINUTES_MS = 5 * 60 * 1000;

type Tone = 'plain' | 'amber' | 'red';

// The timer's colour from the time left (D5): plain without a talk length, amber at 5 minutes or less, red once over.
function toneOf(leftMs: number | null): Tone {
  if (leftMs === null) return 'plain';
  if (leftMs < 0) return 'red';
  return leftMs <= FIVE_MINUTES_MS ? 'amber' : 'plain';
}

const TONE_CLASS: Record<Tone, string> = {
  plain: 'text-slate-200',
  amber: 'text-amber-400',
  red: 'text-red-400',
};

// True for anything React would draw: not null, undefined, a boolean or an empty string.
function isRenderable(node: ReactNode): boolean {
  if (node === null || node === undefined || typeof node === 'boolean' || node === '') return false;
  if (Array.isArray(node)) return node.some(isRenderable);
  return typeof node === 'object' ? isValidElement(node) || Symbol.iterator in node : true;
}

function Chip({ connection }: { connection: PresentationConnection }) {
  const kind = connection.role === 'audience' ? 'Audience' : 'Phone';
  return (
    <span
      data-testid="presenter-chip"
      data-role={connection.role}
      className="inline-flex items-center gap-1.5 whitespace-nowrap rounded-full bg-white/[.06] px-2.5 py-1 text-xs text-slate-400"
    >
      <i aria-hidden className="size-[7px] rounded-full bg-emerald-400" />
      {kind} <b className="font-medium text-slate-200">{connection.label}</b>
    </span>
  );
}

// The presenter window's top bar: the deck's title and current part on the left; on the right the Reading in counter
// and the live chip (each when given), the timer with its time left, the clock, the pause and reset buttons, and a chip per connected audience
// window and phone.
export function PresenterTopBar({
  view,
  state,
  elapsedMs,
  clockMinutes,
  onTimer,
  onReopenAudience,
  liveChip,
  readingIn,
}: Props) {
  const running = state.timer.state === 'running';
  const partNumber = view.slides[state.slide - 1]?.part ?? null;
  const part = partNumber === null ? null : (view.parts[partNumber - 1]?.name ?? null);
  const leftMs = state.talkMinutes === null ? null : state.talkMinutes * 60 * 1000 - elapsedMs;
  const tone = toneOf(leftMs);
  const audience = state.connected.filter((c) => c.role === 'audience');
  const phones = state.connected.filter((c) => c.role === 'phone');
  const iconButton =
    'inline-grid size-7 place-items-center rounded-lg bg-white/[.07] text-slate-200 hover:bg-white/[.14] focus-visible:outline-2 focus-visible:outline-indigo-300';

  return (
    <header
      data-testid="presenter-top-bar"
      className="flex flex-wrap items-center gap-x-3.5 gap-y-2 border-b border-slate-800 px-4 py-2.5 text-[13px] text-slate-200"
    >
      <span className="whitespace-nowrap font-semibold">{view.title}</span>
      {part !== null && (
        <span
          data-testid="presenter-part"
          className="whitespace-nowrap rounded-[5px] bg-indigo-300/[.12] px-2 py-[5px] text-[11px] font-semibold uppercase leading-none tracking-[.12em] text-indigo-300"
        >
          {part}
        </span>
      )}
      <div className="ml-auto flex flex-wrap items-center gap-3.5">
        {/* ReadingIn draws nothing while translations are off: the empty wrapper then takes no place or gap in the row. */}
        {isRenderable(readingIn) && (
          <div data-testid="presenter-reading-in" className="empty:hidden">
            {readingIn}
          </div>
        )}
        {isRenderable(liveChip) && <div data-testid="presenter-live-chip">{liveChip}</div>}
        <div className="flex items-center gap-2.5">
          <span
            data-testid="presenter-timer"
            data-state={state.timer.state}
            data-tone={tone}
            className={`font-mono text-[22px] font-medium leading-none tabular-nums tracking-[.02em] ${TONE_CLASS[tone]}`}
          >
            {formatElapsed(elapsedMs)}
          </span>
          <div className="text-[11.5px] leading-tight text-slate-400">
            {leftMs !== null && (
              <div data-testid="presenter-time-left">
                {leftMs >= 0 ? `${formatElapsed(leftMs)} left` : `${formatElapsed(-leftMs)} over`}{' '}
                of{' '}
                <b className="font-mono font-medium text-slate-200">
                  {formatElapsed(state.talkMinutes! * 60 * 1000)}
                </b>
              </div>
            )}
            <div>
              clock{' '}
              <b data-testid="presenter-clock" className="font-mono font-medium text-slate-200">
                {formatClock(clockMinutes)}
              </b>
            </div>
          </div>
          <button
            type="button"
            data-testid="presenter-timer-pause"
            className={iconButton}
            aria-label={running ? 'Pause timer' : 'Start timer'}
            title={running ? 'Pause timer' : 'Start timer'}
            onClick={() => onTimer(running ? 'pause' : 'resume')}
          >
            <svg aria-hidden viewBox="0 0 24 24" fill="currentColor" className="size-3.5">
              {running ? (
                <>
                  <rect x="6" y="5" width="4" height="14" rx="1" />
                  <rect x="14" y="5" width="4" height="14" rx="1" />
                </>
              ) : (
                <path d="M7 5l12 7-12 7z" />
              )}
            </svg>
          </button>
          <button
            type="button"
            data-testid="presenter-timer-reset"
            className={iconButton}
            aria-label="Reset timer (T)"
            title="Reset timer (T)"
            onClick={() => onTimer('reset')}
          >
            <svg
              aria-hidden
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2.2"
              className="size-3.5"
            >
              <path d="M4 12a8 8 0 1 0 3-6.2M4 4v4h4" />
            </svg>
          </button>
        </div>
        <span aria-hidden className="h-[26px] w-px bg-slate-800" />
        <div className="flex flex-wrap items-center gap-2">
          {audience.length === 0 ? (
            <span className="inline-flex items-center gap-2">
              <span
                data-testid="presenter-no-audience"
                className="inline-flex items-center gap-1.5 whitespace-nowrap rounded-full bg-white/[.06] px-2.5 py-1 text-xs text-slate-400"
              >
                <i aria-hidden className="size-[7px] rounded-full bg-slate-500" />
                No audience window
              </span>
              <button
                type="button"
                data-testid="presenter-reopen-audience"
                className="rounded-lg bg-white/[.07] px-2.5 py-1 text-xs text-slate-200 hover:bg-white/[.14] focus-visible:outline-2 focus-visible:outline-indigo-300"
                onClick={onReopenAudience}
              >
                Reopen audience window
              </button>
            </span>
          ) : (
            audience.map((c) => <Chip key={c.clientId} connection={c} />)
          )}
          {phones.map((c) => (
            <Chip key={c.clientId} connection={c} />
          ))}
        </div>
      </div>
    </header>
  );
}
