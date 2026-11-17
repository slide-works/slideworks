import { useEffect, useMemo, useState, useSyncExternalStore, type ReactNode } from 'react';
import { ApiError } from '../../api/client';
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

// What a call that never reached the server says: the poll's read, and an answer sent while the connection looked live.
const READ_FAILED = 'Couldn’t load the poll. Check your connection.';
const SEND_FAILED = 'Your answer wasn’t sent. Check your connection and try again.';
// A queued pick whose round closed or moved on while the phone was away.
const CLOSED_BEFORE_SENT = 'The poll closed before your answer was sent.';

type Held = Omit<PollAnswer, 'toggle' | 'send' | 'clear'>;

const samePicks = (a: number[], b: number[]) =>
  a.length === b.length && a.every((p, i) => p === b[i]);

const isHubException = (error: unknown) =>
  error instanceof Error && error.message.includes('HubException:');

// The hub's WatchPoll refusal for a guest with no answer in the open round (V5): expected when the answer is not there
// yet, such as a reconnect into a round that moved on.
const isNotAnswered = (error: unknown) =>
  error instanceof Error && error.message.endsWith('HubException: not_answered');

// The code a hub refusal ends with ("…HubException: no_session" is no_session).
const refusalOf = (error: Error) =>
  error.message.slice(error.message.lastIndexOf('HubException:') + 13).trim();

// One guest's answer to one poll card, kept outside React so its rules read top to bottom. PollAnswerLive starts it,
// tells it each activity and status, and renders its snapshot.
//
// - Reads (GET): on start, when the round, phase or roomSeesCounts changes, and after a reconnect, when it first watches
//   the poll if this guest has answered an afterAnswer poll. Only the newest read is applied. 'loading' runs from start
//   until the first read lands, and after a reconnect until the re-read lands.
// - Tallies: only for the activity's round; one with null counts keeps the counts held for that round.
// - Sends (PUT): pick one sends on toggle, pick several on send(). A pick made while not live is 'queued' and sent after
//   the re-read that follows the connection's return, only if the same round is still open. A pick made while a send is
//   in flight goes after it, with the revision it returns. A 409 keeps the picks, takes mine from the current view and
//   is 'conflict'. Anything else is 'failed' with the body's error, or a message of its own when the call never reached
//   the server.
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
  // A PUT is in flight, and the picks to send when it comes back (a pick-one tap made meanwhile).
  private sending = false;
  private next: number[] | null = null;
  // A pick made while the connection was away: its round and picks.
  private queued: { round: string; picks: number[] } | null = null;
  // Whether this connection is in the round's results group. Each change of connection or round starts a new watch.
  private watch: 'none' | 'pending' | 'watching' = 'none';
  private watches = 0;

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
      this.watches += 1;
      this.watch = 'none';
    };
  }

  // The activity and status as the audience connection has them now.
  follow(activity: PollActivity, status: AudienceStatus) {
    const before = this.activity;
    const was = this.status;
    this.activity = activity;
    this.status = status;
    if (!this.active || (before === activity && was === status)) return;

    // The hub forgets the results group when the connection drops, and a new round has its own group.
    if (status !== 'live' || activity.round !== before.round) this.forgetWatch();
    if (activity.round !== before.round) {
      this.next = null;
      this.set({ picks: [], mine: null, tally: null, joinedLate: false });
    }

    const back = status === 'live' && was !== 'live' && (was !== 'connecting' || this.queued);
    if (back) this.read(true);
    else if (
      activity.round !== before.round ||
      activity.phase !== before.phase ||
      activity.roomSeesCounts !== before.roomSeesCounts
    )
      this.read(false);
    this.watchIfAnswered();
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

  readonly clear = () => {
    const { state } = this.held;
    this.set({
      picks: [],
      ...(state === 'conflict' || state === 'failed' ? { state: 'idle', error: null } : {}),
    });
  };

  private readonly onTally = (tally: PollTally) => {
    if (!this.active || tally.round !== this.activity.round) return;
    this.set({ tally: this.merged(tally) });
  };

  // A tally with null counts keeps the counts held for its round.
  private merged(tally: PollTally | null): PollTally | null {
    if (tally === null) return this.held.tally;
    const held = this.held.tally;
    if (tally.counts !== null || held?.round !== tally.round || held.counts === null) return tally;
    return { ...tally, counts: held.counts };
  }

  private read(afterReconnect: boolean) {
    const read = ++this.reads;
    if (afterReconnect && !this.sending) this.set({ state: 'loading', error: null });
    const watched = afterReconnect ? this.watchIfAnswered() : Promise.resolve();
    void watched
      .then(() => getGuestPoll(this.code))
      .then(
        (view) => {
          if (this.active && read === this.reads) this.landed(view);
        },
        (error: unknown) => {
          if (!this.active || read !== this.reads) return;
          if (this.held.state === 'sending' || this.held.state === 'queued') return;
          this.set({
            state: 'failed',
            error: error instanceof ApiError ? error.message : READ_FAILED,
          });
        },
      );
  }

  // Applies a read: mine, the tally and joinedLate when it is about the activity's round. Then sends a queued pick if
  // its round is still the open one, or drops it as failed.
  private landed(view: GuestPollView) {
    const same = (view.activity?.round ?? null) === this.activity.round;
    const mine = same ? view.mine : null;
    const { state, picks } = this.held;
    this.set({
      mine,
      tally: same ? this.merged(view.tally) : this.held.tally,
      joinedLate: same && view.joinedLate,
      picks: picks.length === 0 && !this.queued ? (mine?.picks ?? []) : picks,
      ...(state === 'loading' ? { state: 'idle', error: null } : {}),
    });
    this.watchIfAnswered();

    const queued = this.queued;
    if (queued === null || this.status !== 'live') return;
    this.queued = null;
    if (same && view.activity?.phase === 'open' && view.activity.round === queued.round)
      this.put(queued.picks);
    else this.set({ picks: mine?.picks ?? [], state: 'failed', error: CLOSED_BEFORE_SENT });
  }

  private submit(picks: number[]) {
    const round = this.activity.round;
    if (round === null) return;
    if (this.status !== 'live') {
      this.queued = { round, picks };
      this.set({ state: 'queued', error: null });
    } else if (this.sending) this.next = picks;
    else this.put(picks);
  }

  private put(picks: number[]) {
    const round = this.activity.round!;
    const revision = this.held.mine?.revision ?? 0;
    this.sending = true;
    this.set({ state: 'sending', error: null });
    putPollAnswer(this.code, { round, picks, revision }).then(
      (outcome) => {
        this.sending = false;
        if (!this.active) return;
        const next = this.next;
        this.next = null;
        // The round moved on while the answer was out: the read that followed the change says where the guest is.
        if (round !== this.activity.round) {
          if (this.held.state === 'sending') this.set({ state: 'idle' });
          return;
        }
        if (outcome.type === 'stale') {
          const same = (outcome.current.activity?.round ?? null) === round;
          this.set({
            mine: same ? outcome.current.mine : this.held.mine,
            tally: same ? this.merged(outcome.current.tally) : this.held.tally,
            joinedLate: same && outcome.current.joinedLate,
            picks: next ?? this.held.picks,
            state: 'conflict',
            error: null,
          });
          return;
        }
        this.accepted(outcome.answer, picks, next);
      },
      (error: unknown) => {
        this.sending = false;
        if (!this.active) return;
        const unsent = this.next ?? picks;
        this.next = null;
        if (error instanceof ApiError) this.set({ state: 'failed', error: error.message });
        else if (this.status !== 'live' && round === this.activity.round) {
          // The call never reached the server because the connection went: it waits for the connection to come back.
          this.queued = { round, picks: unsent };
          this.set({ state: 'queued', error: null });
        } else this.set({ state: 'failed', error: SEND_FAILED });
      },
    );
  }

  private accepted(answer: MyAnswer, sent: number[], next: number[] | null) {
    const picks = this.held.picks;
    this.set({ mine: answer, picks: samePicks(picks, sent) ? answer.picks : picks });
    if (next !== null && !samePicks(next, answer.picks)) {
      this.put(next);
      return;
    }
    this.set({ state: 'idle', error: null });
    this.watchIfAnswered();
  }

  // Joins the round's results group once this guest has an answer in an open afterAnswer round. Resolves when the hub
  // has answered, either way: not_answered (the answer isn't in this round) and a dropped connection (the reconnect
  // watches again) leave the card as it is; any other refusal is shown.
  private watchIfAnswered(): Promise<void> {
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
        if (watch === this.watches) this.watch = 'watching';
      },
      (error: unknown) => {
        if (watch !== this.watches) return;
        this.watch = 'none';
        if (this.active && isHubException(error) && !isNotAnswered(error))
          this.set({ state: 'failed', error: refusalOf(error as Error) });
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

  useEffect(() => session.start(), [session]);
  useEffect(() => session.follow(activity, status), [session, activity, status]);

  const held = useSyncExternalStore(session.subscribe, session.snapshot);
  const answer = useMemo<PollAnswer>(
    () => ({ ...held, toggle: session.toggle, send: session.send, clear: session.clear }),
    [held, session],
  );
  return <PollAnswerContext value={answer}>{children}</PollAnswerContext>;
}
