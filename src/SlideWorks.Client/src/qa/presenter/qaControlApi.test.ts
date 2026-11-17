import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import { qaControlView } from '../../test-support/fixtures/qaStates';
import { QA_ACTIONS } from '../types';
import { readQaControl, sendQaAction, updateQaSettings, updateQaSlide } from './qaControlApi';

const control = qaControlView('live');

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

describe('qaControlApi', () => {
  it('reads the view with GET /api/decks/{id}/qa', async () => {
    const fetch = answerWith(200, control);
    expect(await readQaControl('deck 1')).toEqual(control);
    expect(fetch).toHaveBeenCalledWith('/api/decks/deck%201/qa', expect.anything());
    expect(fetch.mock.calls[0][1]).toMatchObject({ method: 'GET' });
  });

  it.each(QA_ACTIONS)('sends %s with POST …/qa/questions/{number}/%s', async (action) => {
    const fetch = answerWith(200, control);
    expect(await sendQaAction('deck-1', action, 4)).toEqual(control);
    expect(fetch).toHaveBeenCalledWith(
      `/api/decks/deck-1/qa/questions/4/${action}`,
      expect.objectContaining({ method: 'POST' }),
    );
  });

  it('changes settings with PUT …/qa/settings and a slide with PUT …/qa/slides/{slide}', async () => {
    const fetch = answerWith(200, control);
    await updateQaSettings('deck-1', { review: false });
    await updateQaSlide('deck-1', 3, { questions: true });
    expect(fetch.mock.calls.map(([url, init]) => [url, init?.method, init?.body])).toEqual([
      ['/api/decks/deck-1/qa/settings', 'PUT', JSON.stringify({ review: false })],
      ['/api/decks/deck-1/qa/slides/3', 'PUT', JSON.stringify({ questions: true })],
    ]);
  });

  it('rejects a refusal with its code and status', async () => {
    answerWith(409, { error: 'wrong_state' });
    const failure = await sendQaAction('deck-1', 'approve', 1).catch((e: unknown) => e);
    expect(failure).toBeInstanceOf(ApiError);
    expect(failure).toMatchObject({ message: 'wrong_state', status: 409 });
  });
});
