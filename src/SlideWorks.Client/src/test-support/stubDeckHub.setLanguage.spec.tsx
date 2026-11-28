import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { audienceState } from './fixtures/guestStates';
import { stubDeckHub } from './stubDeckHub';

// Calls the stubbed hub straight over a WebSocket, as SignalR's JSON protocol does, and returns each completion's
// result or error, in order. No component is mounted: this checks the stub itself.
type Call = { target: string; arguments: unknown[] };

async function invokeAll(page: Page, calls: Call[]) {
  return page.evaluate(async (invocations: Call[]) => {
    const RS = '\u001e';
    const ws = new WebSocket(`${location.origin.replace(/^http/, 'ws')}/hubs/deck?id=self-test`);
    const frames: string[] = [];
    let wake: (() => void) | undefined;
    ws.onmessage = (event) => {
      frames.push(...String(event.data).split(RS).filter(Boolean));
      wake?.();
    };
    const next = async () => {
      while (frames.length === 0) await new Promise<void>((done) => (wake = done));
      return JSON.parse(frames.shift()!) as { result?: unknown; error?: string };
    };
    await new Promise((open) => (ws.onopen = open));
    ws.send(`{"protocol":"json","version":1}${RS}`);
    await next();
    const completions = [];
    for (const [i, call] of invocations.entries()) {
      ws.send(`${JSON.stringify({ type: 1, invocationId: String(i), ...call })}${RS}`);
      const { result, error } = await next();
      completions.push(error === undefined ? { result } : { error });
    }
    ws.close();
    return completions;
  }, calls);
}

test('Stub_WhenSetLanguageIsCalled_ShouldRecordItAndAnswerWithTheCurrentState', async ({
  page,
}) => {
  const state = audienceState();
  const hub = await stubDeckHub(page, { audience: { state } });

  const [joined, set] = await invokeAll(page, [
    { target: 'JoinAudience', arguments: ['123456'] },
    { target: 'SetLanguage', arguments: ['123456', 'ja'] },
  ]);

  expect(hub.languageSets).toEqual([{ code: '123456', language: 'ja' }]);
  expect(joined).toEqual({ result: state });
  expect(set).toEqual({ result: state });
});

test('Stub_WhenAReplyIsScripted_ShouldAnswerSetLanguageWithIt', async ({ page }) => {
  const hub = await stubDeckHub(page, { audience: { state: audienceState({ language: null }) } });
  const japanese = audienceState({ seq: 43, language: { code: 'ja', fallbackLines: 5 } });
  hub.setLanguageReply(japanese);

  const [joined, set] = await invokeAll(page, [
    { target: 'JoinAudience', arguments: ['123456'] },
    { target: 'SetLanguage', arguments: ['123456', 'ja'] },
  ]);

  expect(set).toEqual({ result: japanese });
  expect(joined).toEqual({ result: audienceState({ language: null }) });
});

test('Stub_WhenSetLanguageIsMadeToFail_ShouldAnswerWithTheHubError', async ({ page }) => {
  const hub = await stubDeckHub(page, { audience: { state: audienceState() } });
  hub.fail('SetLanguage', 'not_joined');

  const [failed, retried] = await invokeAll(page, [
    { target: 'SetLanguage', arguments: ['123456', 'th'] },
    { target: 'SetLanguage', arguments: ['123456', 'th'] },
  ]);

  expect(failed).toEqual({
    error:
      "An unexpected error occurred invoking 'SetLanguage' on the server. HubException: not_joined",
  });
  expect(retried).toEqual({ result: audienceState() });
  expect(hub.languageSets).toHaveLength(2);
});

test('Stub_WhenSetLanguageIsHeld_ShouldAnswerOnlyOnRelease', async ({ page }) => {
  const hub = await stubDeckHub(page, { audience: { state: audienceState() } });
  hub.holdLanguage();

  const answered = invokeAll(page, [{ target: 'SetLanguage', arguments: ['123456', 'th'] }]);
  await expect.poll(() => hub.languageSets.length).toBe(1);
  hub.releaseLanguage();

  expect(await answered).toEqual([{ result: audienceState() }]);
});

test('Stub_WithoutTheAudienceOption_ShouldAnswerSetLanguageAsMissing', async ({ page }) => {
  await stubDeckHub(page);

  const [set] = await invokeAll(page, [{ target: 'SetLanguage', arguments: ['123456', 'th'] }]);

  expect(set).toEqual({
    error:
      "Failed to invoke 'SetLanguage' due to an error on the server. HubException: Method does not exist.",
  });
});
