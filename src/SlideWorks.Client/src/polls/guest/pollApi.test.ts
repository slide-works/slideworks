import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import {
  answerAccepted,
  answerInvalid,
  answerRequest,
  answerStale,
  answerTooMany,
  guestPoll,
} from '../../test-support/fixtures/pollStates';
import { stubFetch } from '../../test-support/stubFetch';
import { getGuestPoll, putPollAnswer } from './pollApi';

afterEach(() => vi.unstubAllGlobals());

describe('getGuestPoll', () => {
  it('reads the guest poll view for the code', async () => {
    const fetchMock = stubFetch(200, guestPoll('hidden'));

    const view = await getGuestPoll('482913');

    expect(view).toEqual(guestPoll('hidden'));
    expect(String(fetchMock.mock.calls[0][0])).toBe('/api/join/482913/poll');
  });
});

describe('putPollAnswer', () => {
  it('puts the request as JSON and returns the accepted answer', async () => {
    const fetchMock = stubFetch(200, answerAccepted());

    const outcome = await putPollAnswer('482913', answerRequest());

    expect(outcome).toEqual({ type: 'accepted', answer: answerAccepted() });
    const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(path).toBe('/api/join/482913/poll/answer');
    expect(init.method).toBe('PUT');
    expect(JSON.parse(String(init.body))).toEqual(answerRequest());
  });

  it('returns a 409 stale as an outcome with the current view', async () => {
    stubFetch(409, answerStale());

    const outcome = await putPollAnswer('482913', answerRequest());

    expect(outcome).toEqual({ type: 'stale', current: answerStale().current });
  });

  it('throws a 400 with the rule it names', async () => {
    const rule = answerInvalid()[1];
    stubFetch(400, rule);

    const failure = await putPollAnswer('482913', answerRequest()).catch((error: unknown) => error);

    expect(failure).toBeInstanceOf(ApiError);
    expect(failure).toMatchObject({ message: rule.error, status: 400 });
  });

  it('throws a 429 as too_many_answers', async () => {
    stubFetch(429, answerTooMany());

    const failure = await putPollAnswer('482913', answerRequest()).catch((error: unknown) => error);

    expect(failure).toMatchObject({ message: 'too_many_answers', status: 429 });
  });

  it('throws a 409 that is not stale instead of reading it as stale', async () => {
    stubFetch(409, { error: 'something_else' });

    const failure = await putPollAnswer('482913', answerRequest()).catch((error: unknown) => error);

    expect(failure).toMatchObject({ message: 'something_else', status: 409 });
  });
});
