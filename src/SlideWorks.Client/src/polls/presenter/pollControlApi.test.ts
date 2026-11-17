import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import { pollControlView } from '../../test-support/fixtures/pollStates';
import {
  closePoll,
  openPoll,
  readPollControl,
  revealPoll,
  showPollAnswer,
  startPollRound,
} from './pollControlApi';

const control = pollControlView('open');

function answerWith(status: number, body: unknown) {
  const fetch = vi.fn<typeof globalThis.fetch>(
    async () => new Response(JSON.stringify(body), { status }),
  );
  vi.stubGlobal('fetch', fetch);
  return fetch;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('pollControlApi', () => {
  it('reads the poll on a slide with GET /api/decks/{id}/polls/{slide}', async () => {
    const fetch = answerWith(200, control);
    expect(await readPollControl('deck 1', 4)).toEqual(control);
    expect(fetch).toHaveBeenCalledWith('/api/decks/deck%201/polls/4', expect.anything());
    expect(fetch.mock.calls[0][1]).toMatchObject({ method: 'GET' });
  });

  const actions = [
    [openPoll, 'open'],
    [closePoll, 'close'],
    [revealPoll, 'reveal'],
    [showPollAnswer, 'answer'],
    [startPollRound, 'round'],
  ] as const;

  for (const [send, path] of actions) {
    it(`sends POST …/polls/{slide}/${path} and returns the poll view`, async () => {
      const fetch = answerWith(200, control);
      expect(await send('deck-1', 4)).toEqual(control);
      expect(fetch).toHaveBeenCalledWith(`/api/decks/deck-1/polls/4/${path}`, expect.anything());
      expect(fetch.mock.calls[0][1]).toMatchObject({ method: 'POST' });
    });
  }

  it('rejects a refusal with its code as an ApiError with status 409', async () => {
    answerWith(409, { error: 'wrong_phase' });
    const refused = await closePoll('deck-1', 4).catch((error: unknown) => error);
    expect(refused).toBeInstanceOf(ApiError);
    expect(refused).toMatchObject({ status: 409, message: 'wrong_phase' });
  });
});
