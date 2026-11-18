import { useEffect, useMemo, useState, useSyncExternalStore, type ReactNode } from 'react';
import { ApiError } from '../../api/client';
import type { AudienceStatus } from '../../guest/useAudience';
import type { QaRoom, ReactionEmoji } from '../types';
import { postReaction } from './questionsApi';
import { PhoneReactionsContext, type PhoneReactions } from './usePhoneReactions';

export type ReactionsLiveProps = {
  // The join code the page is on, for POST /api/join/{code}/reactions.
  code: string;
  // The audience state's room: enabled follows room.reactions.
  room: QaRoom | null;
  status: AudienceStatus;
  children: ReactNode;
};

// The server's limit window for taps (Q7: 10 taps in 5 seconds per guest).
const WINDOW_MS = 5000;

// The text for a call that never reached the server (SW-322 D1).
const NETWORK = 'Your phone couldn’t reach the talk. Check your connection, then try again.';

type Held = { cooling: boolean; error: string | null };

// One guest's taps, kept outside React. The first tap sent in a window starts a 5-second timer; a 429 cools the row
// until that timer ends (a full window when none is running), and a tap while cooling isn't sent (SW-322 D4).
class TapSession {
  private held: Held = { cooling: false, error: null };
  private readonly listeners = new Set<() => void>();
  private active = false;
  private window: ReturnType<typeof setTimeout> | undefined;

  constructor(readonly code: string) {}

  readonly subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => void this.listeners.delete(listener);
  };

  readonly snapshot = () => this.held;

  start() {
    this.active = true;
    return () => {
      this.active = false;
      clearTimeout(this.window);
      this.window = undefined;
    };
  }

  readonly tap = async (emoji: ReactionEmoji): Promise<void> => {
    if (!this.active || this.held.cooling) return;
    this.openWindow();
    try {
      const outcome = await postReaction(this.code, emoji);
      if (!this.active) return;
      if (outcome.type === 'sent') this.set({ error: null });
      else if (outcome.type === 'off') this.set({ error: 'reactions_off' });
      else {
        this.openWindow();
        this.set({ cooling: true });
      }
    } catch (error) {
      if (this.active) this.set({ error: error instanceof ApiError ? error.message : NETWORK });
    }
  };

  private openWindow() {
    if (this.window !== undefined) return;
    this.window = setTimeout(() => {
      this.window = undefined;
      if (this.held.cooling) this.set({ cooling: false });
    }, WINDOW_MS);
  }

  private set(change: Partial<Held>) {
    this.held = { ...this.held, ...change };
    for (const listener of [...this.listeners]) listener();
  }
}

// The phone's reactions (Q7, Q8), given to the row through usePhoneReactions.
export function ReactionsLive({ code, room, children }: ReactionsLiveProps) {
  const [session, setSession] = useState(() => new TapSession(code));
  if (session.code !== code) setSession(new TapSession(code));

  useEffect(() => session.start(), [session]);

  const held = useSyncExternalStore(session.subscribe, session.snapshot);
  const enabled = room?.reactions === true;
  const value = useMemo<PhoneReactions>(
    () => ({ enabled, cooling: held.cooling, error: held.error, tap: session.tap }),
    [enabled, held, session],
  );
  return <PhoneReactionsContext value={value}>{children}</PhoneReactionsContext>;
}
