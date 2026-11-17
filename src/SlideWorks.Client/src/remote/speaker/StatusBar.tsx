import type { PresentationStatus } from '../../presentation/usePresentation';
import { formatElapsed } from '../../presentation/format';
import type { TimerState } from '../../presentation/types';

type Props = {
  status: PresentationStatus;
  timerState: TimerState;
  elapsedMs: number;
  talkMinutes: number | null;
  slide: number;
  total: number;
  part: string | null;
  onTimerTap(): void;
};

const AMBER_MS = 5 * 60_000;

// Time left of a set talk length: amber from 5 minutes left, red once over (D5), the same as the presenter window.
type TimerTone = 'normal' | 'amber' | 'red';
function toneOf(leftMs: number | null): TimerTone {
  if (leftMs === null) return 'normal';
  if (leftMs < 0) return 'red';
  return leftMs <= AMBER_MS ? 'amber' : 'normal';
}

const TONE_CLASS: Record<TimerTone, string> = {
  normal: 'text-slate-100',
  amber: 'text-amber-400',
  red: 'text-red-400',
};

// The top of speaker mode: Live or Reconnecting, the timer (a tap pauses or resumes it) with the time left when the
// deck has a talk length, and under them the count with the slide's part.
export function StatusBar({
  status,
  timerState,
  elapsedMs,
  talkMinutes,
  slide,
  total,
  part,
  onTimerTap,
}: Props) {
  const live = status === 'live';
  const leftMs = talkMinutes === null ? null : talkMinutes * 60_000 - elapsedMs;
  const tone = toneOf(leftMs);
  const elapsed = formatElapsed(elapsedMs);
  const left =
    leftMs === null
      ? null
      : leftMs < 0
        ? `${formatElapsed(-leftMs)} over`
        : `${formatElapsed(leftMs)} left`;
  const running = timerState === 'running';
  const action = running
    ? 'Pause the timer'
    : timerState === 'paused'
      ? 'Resume the timer'
      : 'Start the timer';

  return (
    <div className="grid gap-2 px-4 pt-4">
      <div className="flex items-center gap-2">
        <span
          data-testid="speaker-status"
          data-status={status}
          className={`inline-flex items-center gap-1.5 text-[11px] font-semibold tracking-widest uppercase ${
            live ? 'text-emerald-300' : 'text-red-300'
          }`}
        >
          <i
            aria-hidden
            className={`size-2 rounded-full ${live ? 'bg-emerald-400' : 'bg-red-400'}`}
          />
          {live ? 'Live' : status === 'connecting' ? 'Connecting' : 'Reconnecting'}
        </span>
        <span className="flex-1" />
        <button
          type="button"
          data-testid="speaker-timer"
          data-state={timerState}
          data-tone={tone}
          aria-label={`${action} (${elapsed} elapsed${left ? `, ${left}` : ''})`}
          disabled={!live}
          onClick={onTimerTap}
          className="flex items-baseline gap-2 rounded-md px-1 disabled:cursor-default"
        >
          <span className={`font-mono text-lg tabular-nums ${TONE_CLASS[tone]}`}>{elapsed}</span>
          {left && (
            <span
              data-testid="speaker-time-left"
              className={`font-mono text-xs ${tone === 'normal' ? 'text-slate-400' : TONE_CLASS[tone]}`}
            >
              {left}
            </span>
          )}
          {!running && (
            <span className="text-[10px] font-semibold tracking-widest text-slate-400 uppercase">
              {timerState === 'paused' ? 'Paused' : 'Not started'}
            </span>
          )}
        </button>
      </div>
      <div className="flex items-center gap-2 text-[10px] font-semibold tracking-widest text-slate-400 uppercase">
        <span>Now</span>
        <b
          data-testid="speaker-count"
          className="font-mono font-medium tracking-normal text-slate-100"
        >
          {slide} / {total}
        </b>
        <span className="flex-1" />
        {part && <span data-testid="speaker-part">{part}</span>}
      </div>
    </div>
  );
}
