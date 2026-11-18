import { useEffect, useMemo, useState, useSyncExternalStore, type ReactNode } from 'react';
import { ApiError } from '../../api/client';
import type { AudienceQuestions, AudienceStatus } from '../../guest/useAudience';
import type {
  AskRefusalCode,
  AskRequest,
  AskedQuestion,
  GuestQuestion,
  GuestQuestionsView,
  MyVotes,
  QaRoom,
  QuestionTally,
} from '../types';
import { useAudienceQuestions } from './AudienceQuestionsContext';
import { getQuestions, postQuestion, putVotes } from './questionsApi';
import {
  QuestionsContext,
  type AskingState,
  type QuestionSort,
  type Questions,
  type QuestionsState,
} from './useQuestions';

export type QuestionsLiveProps = {
  // The join code the page is on, for the questions' GET, POST and PUT.
  code: string;
  // The audience state's room: a new round or a higher listRevision means the list is read again.
  room: QaRoom | null;
  // The audience connection's status, so a question or an upvote waits while it is reconnecting or offline, and the list
  // is read again after a reconnect.
  status: AudienceStatus;
  // Whether the Questions tab is showing: while it is, the list is read again once a minute so the ages move.
  showing: boolean;
  children: ReactNode;
};

// How often the list is read again while the Questions tab is showing, so the server's age labels move (D22).
const AGE_REREAD_MS = 60_000;

// The error text for a question, vote or reaction call that never reached the server (SW-322 D1), shared with
// ReactionsLive. Every other failure is the server's code or rule; questionErrorText passes this one through as is.
export const QA_NETWORK_ERROR =
  'Your phone couldn’t reach the talk. Check your connection, then try again.';

// The error of a question queued in one round when the connection comes back to a new round: it is not sent (SW-322 D2).
const NEW_ROUND = 'new_round';

// Why a queued question isn't sent when the connection comes back: the room's questions as the server's 409 would say.
const REFUSAL_OF: Record<Exclude<QaRoom['questions'], 'open'>, AskRefusalCode> = {
  closed: 'questions_closed',
  offHere: 'questions_off_here',
  off: 'questions_off',
};

type ErrorFrom = 'read' | 'ask' | 'vote';

type Revision = { round: string | null; revision: number };

type Held = {
  // The room the list was read with; null before the first read.
  room: QaRoom | null;
  // The questions as the server last said, each one's votes consistent with `stored`.
  base: GuestQuestion[];
  // The guest's upvotes as the server holds them.
  stored: MyVotes;
  // The questions whose upvote the guest has toggled and the server doesn't hold yet: mine is stored's picks with these
  // flipped.
  toggles: number[];
  state: QuestionsState;
  asking: AskingState;
  retryIn: number;
  error: string | null;
  errorFrom: ErrorFrom | null;
  sort: QuestionSort;
};

const NO_VOTES: MyVotes = { picks: [], revision: 0 };

const roomOn = (room: QaRoom | null): room is QaRoom & { round: string } =>
  room !== null && room.questions !== 'off' && room.round !== null;

const flip = (picks: number[], toggles: number[]) =>
  [...picks.filter((n) => !toggles.includes(n)), ...toggles.filter((n) => !picks.includes(n))].sort(
    (a, b) => a - b,
  );

// base's votes moved from the server's picks `from` to `to`: one more for each pick added, one fewer for each removed.
const moved = (base: GuestQuestion[], from: number[], to: number[]) =>
  base.map((q) => {
    const change = Number(to.includes(q.number)) - Number(from.includes(q.number));
    return change === 0 ? q : { ...q, votes: q.votes + change };
  });

const messageOf = (error: unknown) =>
  error instanceof ApiError ? error.message : QA_NETWORK_ERROR;

// One guest's questions in one round, kept outside React so its rules read top to bottom. QuestionsLive starts it,
// tells it each room, status and whether the tab is showing, and renders its snapshot.
//
// - Reads (GET): on start, when room.round changes, when the room or a questionTally carries a listRevision higher than
//   the one a read has landed or is in flight for, after a reconnect, once a minute while the tab is showing, and on
//   retry(). Never while the room is null or its questions are off. Only the newest read is applied; within a round
//   mine never moves back. A failed read records nothing, so the next tally or room at that revision reads again.
// - Tallies: only for the held list's round; one with a higher listRevision reads again instead of applying its votes.
//   While a PUT is out, a tally leaves the questions that PUT changes at their held count (it may already count the
//   vote), and the list is read again once the PUT has answered.
// - Asks (POST): one at a time. While not live the question is queued, and after the reconnect's read it is sent if the
//   room still allows asking (else failed with the room's refusal); a new round drops it (failed, new_round). A 201
//   for a round the phone no longer holds is not added.
// - Votes (PUT): at most one out; toggles made meanwhile wait and the picks the guest wants go when it returns, a
//   take-back of what the PUT out sent included. While not live they wait for the reconnect's read. A stale 409 takes
//   `current` and applies the toggles again once.
class QuestionsSession {
  private held: Held = {
    room: null,
    base: [],
    stored: NO_VOTES,
    toggles: [],
    state: 'loading',
    asking: 'idle',
    retryIn: 0,
    error: null,
    errorFrom: null,
    sort: 'top',
  };
  private readonly listeners = new Set<() => void>();
  private active = false;
  private stopTallies: (() => void) | null = null;
  private reads = 0;
  // The round and listRevision the last read that landed covers, and the read in flight's, so the same change doesn't
  // read twice. A failed read leaves `landedFor` as it was.
  private landedFor: Revision = { round: null, revision: 0 };
  private inFlight: Revision | null = null;
  private queuedAsk: AskRequest | null = null;
  // The PUT out, by its token: a response whose token isn't this one's is ignored. `sent` is its picks.
  private voteOut: number | null = null;
  private sent: number[] | null = null;
  // Whether a tally came, while the PUT was out, for a question that PUT changes.
  private tallyTouched = false;
  private puts = 0;
  private staleRetried = false;
  private countdown: ReturnType<typeof setInterval> | undefined;
  private ageTimer: ReturnType<typeof setInterval> | undefined;

  constructor(
    readonly code: string,
    readonly channel: AudienceQuestions | null,
    private room: QaRoom | null,
    private status: AudienceStatus,
    private showing: boolean,
  ) {}

  readonly subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => void this.listeners.delete(listener);
  };

  readonly snapshot = () => this.held;

  // Subscribes to tallies first, then reads, so no tally falls between the read and the subscription.
  start() {
    this.active = true;
    this.stopTallies = this.channel?.onTally(this.onTally) ?? null;
    if (roomOn(this.room)) this.read();
    this.keepAgesMoving();
    return () => {
      this.active = false;
      this.stopTallies?.();
      this.stopTallies = null;
      this.reads += 1;
      this.inFlight = null;
      clearInterval(this.countdown);
      clearInterval(this.ageTimer);
      this.ageTimer = undefined;
      // The countdown is gone, so limited would never end if the effects run again (StrictMode, <Activity>).
      if (this.held.asking === 'limited')
        this.set({ asking: 'idle', retryIn: 0, ...this.cleared('ask') });
    };
  }

  follow(room: QaRoom | null, status: AudienceStatus, showing: boolean) {
    const was = this.status;
    this.room = room;
    this.status = status;
    this.showing = showing;
    if (!this.active) return;
    this.keepAgesMoving();
    if (!roomOn(room)) return;
    const waiting = this.queuedAsk !== null || this.held.toggles.length > 0;
    const back = status === 'live' && was !== 'live' && (was !== 'connecting' || waiting);
    if (back || !this.known(room.round, room.listRevision)) this.read();
  }

  readonly retry = () => {
    if (!this.active || !roomOn(this.room)) return;
    if (this.held.state === 'failed') this.set({ state: 'loading' });
    this.read();
  };

  readonly setSort = (sort: QuestionSort) => this.set({ sort });

  readonly ask = async (text: string, name: string | null): Promise<void> => {
    const { asking } = this.held;
    if (!this.active || asking === 'sending' || asking === 'limited') return;
    const request = { text, name };
    if (this.status !== 'live') {
      this.queuedAsk = request;
      this.set({ asking: 'queued', ...this.cleared('ask') });
      return;
    }
    await this.post(request);
  };

  readonly toggleVote = (number: number) => {
    const { state, room, base, toggles } = this.held;
    if (!this.active || state !== 'ready' || room?.round == null) return;
    if (base.find((q) => q.number === number)?.state !== 'open') return;
    this.set({ toggles: flip(toggles, [number]) });
    this.flushVotes();
  };

  private readonly onTally = (tally: QuestionTally) => {
    const { room, base } = this.held;
    if (!this.active || room === null || tally.round !== room.round) return;
    if (tally.listRevision > room.listRevision) {
      if (!this.known(tally.round, tally.listRevision)) this.read(tally.listRevision);
      return;
    }
    const votes = new Map(tally.votes.map((v) => [v.number, v.votes]));
    const changing = this.changing();
    this.set({
      base: base.map((q) => {
        const count = votes.get(q.number);
        if (count === undefined || count === q.votes) return q;
        if (changing.includes(q.number)) {
          this.tallyTouched = true;
          return q;
        }
        return { ...q, votes: count };
      }),
    });
  };

  // Whether a read has landed, or is in flight, for this round at this listRevision or later.
  private known(round: string, revision: number): boolean {
    const covers = (r: Revision | null) =>
      r !== null && r.round === round && revision <= r.revision;
    return covers(this.landedFor) || covers(this.inFlight);
  }

  // The questions the PUT out changes: where its picks differ from the stored ones.
  private changing(): number[] {
    return this.sent === null ? [] : flip(this.sent, this.held.stored.picks);
  }

  // mine as the phone shows it: the stored picks with the unsent toggles applied.
  private mine(): number[] {
    return flip(this.held.stored.picks, this.held.toggles);
  }

  // The toggles still needed for the picks the guest wants, against picks the server now holds, on open questions only.
  // The candidates are the unsent toggles and what the PUT out changes, so a take-back made while it was out survives.
  private stillToggled(wanted: number[], stored: number[], base: GuestQuestion[]): number[] {
    const open = (n: number) => base.find((q) => q.number === n)?.state === 'open';
    const candidates = [...new Set([...this.held.toggles, ...this.changing()])];
    return candidates.filter((n) => open(n) && wanted.includes(n) !== stored.includes(n));
  }

  // Clears the error when it came from the same kind of call that just succeeded.
  private cleared(from: ErrorFrom): Partial<Held> {
    return this.held.errorFrom === from ? { error: null, errorFrom: null } : {};
  }

  private read(revision?: number) {
    const room = this.room;
    if (!roomOn(room)) return;
    const read = ++this.reads;
    const asked = this.inFlight?.round === room.round ? this.inFlight.revision : 0;
    const wanted = {
      round: room.round,
      revision: Math.max(asked, room.listRevision, revision ?? 0),
    };
    this.inFlight = wanted;
    getQuestions(this.code).then(
      (view) => {
        if (!this.active || read !== this.reads) return;
        this.inFlight = null;
        const revision = view.room.listRevision;
        this.landedFor = {
          round: view.room.round,
          revision:
            view.room.round === wanted.round ? Math.max(revision, wanted.revision) : revision,
        };
        this.landed(view);
      },
      (error: unknown) => {
        if (!this.active || read !== this.reads) return;
        // A failed re-read keeps the list it holds and records no revision; the next trigger reads again.
        this.inFlight = null;
        const state = this.held.state === 'ready' ? 'ready' : 'failed';
        this.set({ state, error: messageOf(error), errorFrom: 'read' });
      },
    );
  }

  // Applies a read. Within the round mine never moves back, and the toggles not yet sent stay; a new round starts from
  // the view alone. Then whatever waited for the connection goes.
  private landed(view: GuestQuestionsView) {
    const held = this.held;
    const sameRound = held.room !== null && held.room.round === view.room.round;
    if (sameRound) {
      const stored = view.mine.revision < held.stored.revision ? held.stored : view.mine;
      const base = moved(view.questions, view.mine.picks, stored.picks);
      const toggles = this.stillToggled(this.mine(), stored.picks, base);
      this.set({ room: view.room, base, stored, toggles });
    } else {
      // Nothing crosses a round: the PUT out, the toggles and a queued question belong to the old one.
      this.voteOut = null;
      this.sent = null;
      this.tallyTouched = false;
      this.staleRetried = false;
      this.set({ room: view.room, base: view.questions, stored: view.mine, toggles: [] });
      if (held.room !== null && this.queuedAsk !== null) {
        this.queuedAsk = null;
        this.set({ asking: 'failed', error: NEW_ROUND, errorFrom: 'ask' });
      }
    }
    this.set({ state: 'ready', ...this.cleared('read') });

    if (this.status !== 'live') return;
    this.flushAsk(view.room);
    this.flushVotes();
  }

  private flushAsk(room: QaRoom) {
    const request = this.queuedAsk;
    if (request === null) return;
    this.queuedAsk = null;
    if (room.questions === 'open') void this.post(request);
    else this.set({ asking: 'failed', error: REFUSAL_OF[room.questions], errorFrom: 'ask' });
  }

  private async post(request: AskRequest) {
    const round = this.held.room?.round ?? null;
    this.set({ asking: 'sending', ...this.cleared('ask') });
    try {
      const outcome = await postQuestion(this.code, request);
      if (!this.active) return;
      // A 201 for a round the phone no longer holds belongs to that round's list, not this one.
      if (outcome.type === 'asked' && this.held.room?.round !== round)
        this.set({ asking: 'idle', ...this.cleared('ask') });
      else if (outcome.type === 'asked') this.asked(outcome.asked);
      else if (outcome.type === 'refused')
        this.set({ asking: 'failed', error: outcome.error, errorFrom: 'ask' });
      else this.limit(outcome.retryAfter);
    } catch (error) {
      if (!this.active) return;
      if (!(error instanceof ApiError) && this.status !== 'live') {
        // The call never reached the server because the connection went: it waits for the connection to come back.
        this.queuedAsk = request;
        this.set({ asking: 'queued' });
      } else this.set({ asking: 'failed', error: messageOf(error), errorFrom: 'ask' });
    }
  }

  // The new question joins the list with the asker's upvote (unless the writer refused it), and mine follows.
  private asked({ question, mine }: AskedQuestion) {
    const held = this.held;
    const wanted = mine.picks.includes(question.number)
      ? [...this.mine(), question.number]
      : this.mine();
    const stored = mine.revision < held.stored.revision ? held.stored : mine;
    const base = [...held.base.filter((q) => q.number !== question.number), question].sort(
      (a, b) => a.number - b.number,
    );
    const toggles = this.stillToggled(wanted, stored.picks, base);
    this.set({ base, stored, toggles, asking: 'idle', ...this.cleared('ask') });
  }

  private limit(retryAfter: number) {
    clearInterval(this.countdown);
    this.set({
      asking: 'limited',
      retryIn: Math.max(0, retryAfter),
      error: 'too_many_questions',
      errorFrom: 'ask',
    });
    if (retryAfter <= 0) {
      this.set({ asking: 'idle', retryIn: 0, ...this.cleared('ask') });
      return;
    }
    this.countdown = setInterval(() => {
      const retryIn = this.held.retryIn - 1;
      if (retryIn > 0) {
        this.set({ retryIn });
        return;
      }
      clearInterval(this.countdown);
      this.set({ asking: 'idle', retryIn: 0, ...this.cleared('ask') });
    }, 1000);
  }

  // Sends the picks the guest wants when nothing is out, something is toggled and the connection is live.
  private flushVotes() {
    const { room, toggles, stored } = this.held;
    if (this.voteOut !== null || toggles.length === 0 || this.status !== 'live') return;
    if (room?.round == null) return;
    const round = room.round;
    const token = ++this.puts;
    const picks = this.mine();
    this.voteOut = token;
    this.sent = picks;
    this.tallyTouched = false;
    putVotes(this.code, { round, picks, revision: stored.revision }).then(
      (outcome) => {
        if (!this.active || this.voteOut !== token) return;
        this.voteOut = null;
        const touched = this.tallyTouched;
        if (outcome.type === 'accepted') this.accepted(outcome.mine);
        else if (outcome.type === 'stale') this.stale(outcome.current);
        else this.dropToggles('too_many_votes');
        // A tally held back for this PUT's questions may have counted it: read the list, whose counts and mine agree.
        if (touched && outcome.type !== 'stale') this.read();
      },
      (error: unknown) => {
        if (!this.active || this.voteOut !== token) return;
        this.voteOut = null;
        const touched = this.tallyTouched;
        // The call never reached the server because the connection went: the toggles wait for the reconnect's read.
        if (!(error instanceof ApiError) && this.status !== 'live') this.sent = null;
        else this.dropToggles(messageOf(error));
        if (touched) this.read();
      },
    );
  }

  private dropToggles(error: string) {
    this.sent = null;
    this.set({ toggles: [], error, errorFrom: 'vote' });
  }

  private accepted(answer: MyVotes) {
    const held = this.held;
    const wanted = this.mine();
    const stored = answer.revision < held.stored.revision ? held.stored : answer;
    const base = moved(held.base, held.stored.picks, stored.picks);
    const toggles = this.stillToggled(wanted, stored.picks, base);
    this.sent = null;
    this.staleRetried = false;
    this.set({ base, stored, toggles, ...this.cleared('vote') });
    this.flushVotes();
  }

  // The server's view is current; the toggles are applied to it again once, then dropped with the error.
  private stale(current: GuestQuestionsView) {
    const held = this.held;
    const wanted = this.mine();
    const sameRound = held.room?.round === current.room.round;
    const retry = sameRound && !this.staleRetried;
    this.staleRetried = retry;
    const { round, listRevision } = current.room;
    this.landedFor = {
      round,
      revision:
        round === this.landedFor.round
          ? Math.max(listRevision, this.landedFor.revision)
          : listRevision,
    };
    const toggles = retry ? this.stillToggled(wanted, current.mine.picks, current.questions) : [];
    this.sent = null;
    this.set({
      room: current.room,
      base: current.questions,
      stored: current.mine,
      toggles,
      ...(retry ? {} : { error: 'stale', errorFrom: 'vote' as const }),
    });
    this.flushVotes();
  }

  // While the Questions tab is showing in a room with questions, the list is read once a minute.
  private keepAgesMoving() {
    const wanted = this.active && this.showing && roomOn(this.room);
    if (wanted && this.ageTimer === undefined)
      this.ageTimer = setInterval(() => this.read(), AGE_REREAD_MS);
    else if (!wanted && this.ageTimer !== undefined) {
      clearInterval(this.ageTimer);
      this.ageTimer = undefined;
    }
  }

  private set(change: Partial<Held>) {
    this.held = { ...this.held, ...change };
    for (const listener of [...this.listeners]) listener();
  }
}

// The room useQuestions gives: null before the first read, then the audience state's room unless the read's is a newer
// revision of the same round (SW-322 D6).
function roomShown(read: QaRoom | null, live: QaRoom | null): QaRoom | null {
  if (read === null || live === null) return read;
  return read.round === live.round && read.listRevision > live.listRevision ? read : live;
}

// The phone's questions from the server (Q8), given to the parts through useQuestions. The questions channel is
// useAudienceQuestions(): subscribed to before the first GET.
export function QuestionsLive({ code, room, status, showing, children }: QuestionsLiveProps) {
  const channel = useAudienceQuestions();
  const [session, setSession] = useState(
    () => new QuestionsSession(code, channel, room, status, showing),
  );
  if (session.code !== code || session.channel !== channel)
    setSession(new QuestionsSession(code, channel, room, status, showing));

  useEffect(() => session.start(), [session]);
  useEffect(() => session.follow(room, status, showing), [session, room, status, showing]);

  const held = useSyncExternalStore(session.subscribe, session.snapshot);
  const value = useMemo<Questions>(() => {
    const picks = flip(held.stored.picks, held.toggles);
    return {
      room: roomShown(held.room, room),
      // Each question's votes with this guest's unsent toggles applied (Q8).
      questions: moved(held.base, held.stored.picks, picks),
      mine: { picks, revision: held.stored.revision },
      state: held.state,
      ask: session.ask,
      asking: held.asking,
      retryIn: held.retryIn,
      error: held.error,
      toggleVote: session.toggleVote,
      retry: session.retry,
      sort: held.sort,
      setSort: session.setSort,
    };
  }, [held, room, session]);
  return <QuestionsContext value={value}>{children}</QuestionsContext>;
}
