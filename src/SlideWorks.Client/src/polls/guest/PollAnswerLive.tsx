import { useEffect, useMemo, useState, useSyncExternalStore, type ReactNode } from 'react';
import { ApiError } from '../../api/client';
import { useGuestLanguageCode } from '../../guest/language/GuestLanguageContext';
import type { AudiencePolls, AudienceStatus } from '../../guest/useAudience';
import type { GuestPollView, MyAnswer, PollActivity, PollTally } from '../types';
import { getGuestPoll, putPollAnswer } from './pollApi';
import { PollAnswerContext, type PollAnswer } from './usePollAnswer';

export type PollAnswerLiveProps = {
  // The join code: GET /api/join/{code}/poll and PUT /api/join/{code}/poll/answer (pollApi.ts).
  code: string;
  // The poll on screen, from the audience state. A new round, phase or roomSeesCounts means a fresh read.
  activity: PollActivity;
  // The audience connection's status: reconnecting or offline with an unsent pick is 'queued'.
  status: AudienceStatus;
  // The audience connection's poll side: every pollTally it receives (polls.onTally), and WatchPoll on that same
  // connection (polls.watchPoll), to call once the guest has answered an afterAnswer poll and again each time status
  // comes back to 'live' after a reconnect. A tally with null counts never clears counts already held for the round.
  polls: AudiencePolls;
  children: ReactNode;
};

// What the card says under "Your answer wasn't sent.", one plain sentence per refusal (SW-271 D1), so no code or
// platform message ever reaches a guest. A picks rule the server names is reworded too; anything not listed is
// UNKNOWN.
const MESSAGES: Record<string, string> = {
  too_many_answers:
    'You’ve changed your answer a lot in the last minute. Wait a moment, then send it again.',
  too_many_attempts: 'Too many tries from this network right now. Wait a moment, then try again.',
  no_session: 'This talk isn’t live any more.',
  no_guest:
    'Your browser isn’t keeping this site’s cookie. Allow cookies for this site, then reload the page.',
  'picks: required': 'Pick an answer first.',
  'picks: pick one': 'Pick just one answer.',
  'picks: duplicate': 'Each answer can be picked only once.',
};
const AT_MOST = /^picks: at most (\d+)$/;
const UNKNOWN_OPTION = /^picks: unknown option \d+$/;
const NETWORK = 'Your phone couldn’t reach the talk. Check your connection, then try again.';
const CLOSED_BEFORE_SENT = 'The poll closed before your answer could be sent.';
const UNKNOWN = 'Something went wrong on our side. Try again in a moment.';

// The hub's own refusal (not_answered or any other code), as opposed to the connection's failure.
const isHubException = (error: unknown) =>
  error instanceof Error && error.message.includes('HubException:');

// A failed call as a sentence for the guest: an ApiError's code or rule (the body's error), or a call that never
// reached the server.
function messageFor(error: unknown): string {
  if (!(error instanceof ApiError)) return NETWORK;
  const code = error.message;
  const max = AT_MOST.exec(code);
  if (max) return `Pick up to ${max[1]} answers.`;
  if (UNKNOWN_OPTION.test(code)) return 'That option isn’t on this poll any more. Pick again.';
  return MESSAGES[code] ?? UNKNOWN;
}

type Held = Omit<PollAnswer, 'toggle' | 'send' | 'clear'>;

const samePicks = (a: number[], b: number[]) =>
  a.length === b.length && a.every((p, i) => p === b[i]);

// One guest's answer to one poll card, kept outside React so its rules read top to bottom. PollAnswerLive starts it,
// tells it each activity and status, and renders its snapshot.
//
// - Reads (GET): on start, when the round, phase or roomSeesCounts changes, after a reconnect (when it first watches
//   the poll if this guest has answered an afterAnswer poll), and once more after a first watch resolves. Only the
//   newest read is applied, and a read never moves mine back within a round. 'loading' runs from start until the first
//   read lands, and after a reconnect until the re-read lands. A failed read shows its sentence; the next activity
//   change or reconnect reads again.
// - Tallies: only for the activity's round; one with null counts keeps the counts held for that round.
// - Sends (PUT), for the current round only. Its invariants: at most one PUT is out (known by its token); the latest
//   unsent pick wins (`pending`, coalescing arrow keys walking the radios); nothing crosses a round (a round change
//   forgets the PUT out, the pending pick and the old round's state); mine only moves forward. Pick one sends on
//   toggle, pick several on send(); the first tap is never delayed, and a pick the server already holds sends nothing.
//   A pick made while not live is 'queued' and goes after the reconnect's re-read if the round is still open. A 409
//   keeps the picks, takes mine from the current view and is 'conflict'. Anything else is 'failed' with one of
//   MESSAGES' sentences, never a code or a platform message.
class AnswerSession {
  private held: Held = {
    picks: [],
    mine: null,
    tally: null,
    joinedLate: false,
    state: 'loading',
    error: null,
  };
  private readonly listeners = new Set<() => void>();
  private active = false;
  private stopTallies: (() => void) | null = null;
  private reads = 0;
  // The PUT out, by its token: a response whose token isn't this one's is from a forgotten PUT and is ignored.
  private out: number | null = null;
  private puts = 0;
  // The latest pick not yet sent in this round: made while a PUT was out, or while the connection was away.
  private pending: number[] | null = null;
  // Whether this connection is in the round's results group. Each change of connection or round starts a new watch;
  // a refused watch is tried again at the next accepted answer or reconnect.
  private watch: 'none' | 'pending' | 'watching' | 'refused' = 'none';
  private watches = 0;
  // The language the phone reads in (SW-883): every read and send carries it. Set on each render.
  private language: string | null = null;

  readonly readIn = (language: string | null) => {
    this.language = language;
  };

  constructor(
    readonly code: string,
    readonly polls: AudiencePolls,
    private activity: PollActivity,
    private status: AudienceStatus,
  ) {}

  readonly subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => void this.listeners.delete(listener);
  };

  readonly snapshot = () => this.held;

  // Subscribes to tallies first, then reads, so no tally falls between the read and the subscription.
  start() {
    this.active = true;
    this.stopTallies = this.polls.onTally(this.onTally);
    this.read(false);
    return () => {
      this.active = false;
      this.stopTallies?.();
      this.stopTallies = null;
      this.reads += 1;
      this.out = null;
      this.pending = null;
      this.forgetWatch();
    };
  }

  // The activity and status as the audience connection has them now.
  follow(activity: PollActivity, status: AudienceStatus) {
    const before = this.activity;
    const was = this.status;
    this.activity = activity;
    this.status = status;
    if (!this.active || (before === activity && was === status)) return;
    const newRound = activity.round !== before.round;

    // The hub forgets the results group when the connection drops, and a new round has its own group.
    if (status !== 'live' || newRound) this.forgetWatch();
    // Nothing crosses a round: the old round's PUT, pending pick, answer and state are forgotten.
    if (newRound) {
      this.out = null;
      this.pending = null;
      this.set({
        picks: [],
        mine: null,
        tally: null,
        joinedLate: false,
        ...(this.held.state === 'loading' ? {} : { state: 'idle', error: null }),
      });
    }

    const back = status === 'live' && was !== 'live' && (was !== 'connecting' || this.pending);
    if (back) this.read(true);
    else if (
      newRound ||
      activity.phase !== before.phase ||
      activity.roomSeesCounts !== before.roomSeesCounts
    )
      this.read(false);
    void this.watchIfAnswered(true);
  }

  readonly toggle = (position: number) => {
    const { activity } = this;
    if (this.held.state === 'loading' || activity.phase !== 'open') return;
    if (activity.pick === 'one') {
      this.set({ picks: [position] });
      this.submit([position]);
      return;
    }
    const picks = this.held.picks;
    if (picks.includes(position)) this.set({ picks: picks.filter((p) => p !== position) });
    else if (activity.max === null || picks.length < activity.max)
      this.set({ picks: [...picks, position].sort((a, b) => a - b) });
  };

  readonly send = () => {
    if (this.held.state === 'loading' || this.activity.phase !== 'open') return;
    this.submit(this.held.picks);
  };

  // Two callers, one rule (SW-271 D3). PollConflict's Clear, or with a failed or queued pick: the unsent pick is
  // dropped and the card shows what the server holds for this guest in the round, mine's picks or nothing, idle.
  // PollFoot's "Change my answer" after a send: the sent picks stay ticked, so nothing changes; the guest edits them
  // and Send comes back once they differ from mine.
  readonly clear = () => {
    const { state, mine } = this.held;
    if (state !== 'conflict' && state !== 'failed' && state !== 'queued') return;
    this.pending = null;
    this.set({ picks: mine?.picks ?? [], state: 'idle', error: null });
  };

  private readonly onTally = (tally: PollTally) => {
    if (!this.active || tally.round !== this.activity.round) return;
    this.set({ tally: this.merged(tally) });
  };

  // A tally with null counts keeps the counts held for its round. (Whether the card may draw them is PollCard's, from
  // the server's fields, not from whether counts are held here.)
  private merged(tally: PollTally | null): PollTally | null {
    if (tally === null) return this.held.tally;
    const held = this.held.tally;
    if (tally.counts !== null || held?.round !== tally.round || held.counts === null) return tally;
    return { ...tally, counts: held.counts };
  }

  // mine only moves forward within the round: a view read before an accepted PUT carries a lower revision.
  private forward(mine: MyAnswer | null): MyAnswer | null {
    const held = this.held.mine;
    return held !== null && (mine?.revision ?? 0) < held.revision ? held : mine;
  }

  private read(afterReconnect: boolean) {
    const read = ++this.reads;
    if (afterReconnect && this.out === null) this.set({ state: 'loading', error: null });
    const watched = afterReconnect ? this.watchIfAnswered(false) : Promise.resolve();
    void watched
      .then(() => getGuestPoll(this.code, this.language))
      .then(
        (view) => {
          if (this.active && read === this.reads) this.landed(view);
        },
        (error: unknown) => {
          if (!this.active || read !== this.reads) return;
          // A failed read never hides a send in progress or a conflict; the next change or reconnect reads again.
          const { state } = this.held;
          if (state === 'sending' || state === 'queued' || state === 'conflict') return;
          this.set({ state: 'failed', error: messageFor(error) });
        },
      );
  }

  // Applies a read: mine (never backwards), the tally and joinedLate when it is about the activity's round. Then a
  // pending pick goes if the round is still open (after the PUT out, if any), or is dropped as failed.
  private landed(view: GuestPollView) {
    const same = (view.activity?.round ?? null) === this.activity.round;
    const mine = same ? this.forward(view.mine) : this.held.mine;
    const { state, picks } = this.held;
    this.set({
      mine,
      tally: same ? this.merged(view.tally) : this.held.tally,
      joinedLate: same && view.joinedLate,
      picks: picks.length === 0 && this.pending === null ? (mine?.picks ?? []) : picks,
      ...(state === 'loading' ? { state: 'idle', error: null } : {}),
    });
    void this.watchIfAnswered(true);

    if (this.pending === null || this.status !== 'live') return;
    if (!same || view.activity?.phase !== 'open') {
      this.pending = null;
      this.set({ picks: mine?.picks ?? [], state: 'failed', error: CLOSED_BEFORE_SENT });
    } else if (this.out !== null) this.set({ state: 'sending', error: null });
    else this.flush();
  }

  private submit(picks: number[]) {
    if (this.activity.round === null) return;
    this.pending = picks;
    if (this.status !== 'live') this.set({ state: 'queued', error: null });
    else this.flush();
  }

  // Sends the pending pick when nothing is out and the connection is live. A pick the server holds sends nothing.
  private flush() {
    if (this.out !== null || this.pending === null || this.status !== 'live') return;
    const picks = this.pending;
    this.pending = null;
    const mine = this.held.mine;
    if (mine !== null && samePicks(picks, mine.picks)) {
      this.set({ state: 'idle', error: null });
      return;
    }
    this.put(picks);
  }

  private put(picks: number[]) {
    const round = this.activity.round!;
    const revision = this.held.mine?.revision ?? 0;
    const token = ++this.puts;
    this.out = token;
    this.set({ state: 'sending', error: null });
    putPollAnswer(this.code, { round, picks, revision }, this.language).then(
      (outcome) => {
        if (!this.active || this.out !== token) return;
        this.out = null;
        if (outcome.type === 'stale') {
          // The card asks again with the latest pick kept; nothing more goes by itself.
          this.pending = null;
          const same = (outcome.current.activity?.round ?? null) === round;
          this.set({
            mine: same ? this.forward(outcome.current.mine) : this.held.mine,
            tally: same ? this.merged(outcome.current.tally) : this.held.tally,
            joinedLate: same && outcome.current.joinedLate,
            state: 'conflict',
            error: null,
          });
        } else this.accepted(outcome.answer, picks);
      },
      (error: unknown) => {
        if (!this.active || this.out !== token) return;
        this.out = null;
        if (error instanceof ApiError) {
          this.pending = null;
          this.set({ state: 'failed', error: messageFor(error) });
        } else if (this.status !== 'live') {
          // The call never reached the server because the connection went: the latest pick waits for it to come back.
          this.pending = this.pending ?? picks;
          this.set({ state: 'queued', error: null });
        } else {
          this.pending = null;
          this.set({ state: 'failed', error: NETWORK });
        }
      },
    );
  }

  private accepted(answer: MyAnswer, sent: number[]) {
    const picks = this.held.picks;
    this.set({
      mine: this.forward(answer),
      picks: this.pending === null && samePicks(picks, sent) ? answer.picks : picks,
    });
    if (this.pending !== null) {
      if (this.status === 'live') this.flush();
      else this.set({ state: 'queued', error: null });
      return;
    }
    this.set({ state: 'idle', error: null });
    // An accepted answer is the moment a refused watch is tried again.
    if (this.watch === 'refused') this.watch = 'none';
    void this.watchIfAnswered(true);
  }

  // Joins the round's results group once this guest has an answer in an open afterAnswer round, and resolves when the
  // hub has answered either way. A refusal shows nothing (the answer was accepted; only live counts are missing) and
  // waits for the next accepted answer or reconnect; a dropped connection is the reconnect's. With reRead, a watch that
  // succeeds reads once more, because WatchPoll brings no tally and a quiet room may not send one soon.
  private watchIfAnswered(reRead: boolean): Promise<void> {
    const { activity } = this;
    if (
      this.watch !== 'none' ||
      this.status !== 'live' ||
      activity.results !== 'afterAnswer' ||
      activity.phase !== 'open' ||
      this.held.mine === null
    )
      return Promise.resolve();
    const watch = ++this.watches;
    this.watch = 'pending';
    return this.polls.watchPoll().then(
      () => {
        if (watch !== this.watches) return;
        this.watch = 'watching';
        if (reRead && this.active) this.read(false);
      },
      (error: unknown) => {
        if (watch === this.watches) this.watch = isHubException(error) ? 'refused' : 'none';
      },
    );
  }

  private forgetWatch() {
    this.watches += 1;
    this.watch = 'none';
  }

  private set(change: Partial<Held>) {
    this.held = { ...this.held, ...change };
    for (const listener of [...this.listeners]) listener();
  }
}

// The guest's answer to the poll on screen, for the card under it (V6): the real provider behind usePollAnswer.
export function PollAnswerLive({ code, activity, status, polls, children }: PollAnswerLiveProps) {
  const [session, setSession] = useState(() => new AnswerSession(code, polls, activity, status));
  if (session.code !== code || session.polls !== polls)
    setSession(new AnswerSession(code, polls, activity, status));

  const language = useGuestLanguageCode();
  useEffect(() => {
    session.readIn(language);
  }, [session, language]);
  useEffect(() => session.start(), [session]);
  useEffect(() => session.follow(activity, status), [session, activity, status]);

  const held = useSyncExternalStore(session.subscribe, session.snapshot);
  const answer = useMemo<PollAnswer>(
    () => ({ ...held, toggle: session.toggle, send: session.send, clear: session.clear }),
    [held, session],
  );
  return <PollAnswerContext value={answer}>{children}</PollAnswerContext>;
}
