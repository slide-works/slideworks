import type { ReactNode } from 'react';
import type { PresenterView } from '../../api/types';
import type { PresentationState, TimerCommand } from '../types';

type Props = {
  view: PresenterView;
  state: PresentationState;
  elapsedMs: number;
  clockMinutes: number;
  onTimer(cmd: TimerCommand): void;
  onReopenAudience(): void;
  // A chip another feature puts at the start of the right-hand group (P10). Its wrapper renders only when given.
  liveChip?: ReactNode;
};

// The presenter window's top bar: the deck's title on the left; on the right the live chip (when given), the timer
// with its pause and reset buttons, and a chip per connected window and phone.
export function PresenterTopBar({ view, state, onTimer, onReopenAudience, liveChip }: Props) {
  const running = state.timer.state === 'running';
  // TODO: placeholder bar until the top bar is built: the formatted timer and time left (elapsedMs, talkMinutes), the
  // clock (clockMinutes), the part name and the chips' look replace it.
  return (
    <header data-testid="presenter-top-bar" className="flex items-center gap-3 px-4 py-2">
      <span className="text-sm font-bold">{view.title}</span>
      <div className="ml-auto flex items-center gap-2">
        {liveChip !== undefined && <div data-testid="presenter-live-chip">{liveChip}</div>}
        <span data-testid="presenter-timer" data-state={state.timer.state}>
          Timer
        </span>
        <button
          type="button"
          data-testid="presenter-timer-pause"
          className="btn-ghost"
          onClick={() => onTimer(running ? 'pause' : 'resume')}
        >
          {running ? 'Pause timer' : 'Start timer'}
        </button>
        <button
          type="button"
          data-testid="presenter-timer-reset"
          className="btn-ghost"
          onClick={() => onTimer('reset')}
        >
          Reset timer
        </button>
        {state.connected.map((connection) => (
          <span key={connection.clientId} data-testid="presenter-chip" data-role={connection.role}>
            {connection.label}
          </span>
        ))}
        <button type="button" className="btn-ghost" onClick={onReopenAudience}>
          Reopen audience window
        </button>
      </div>
    </header>
  );
}
