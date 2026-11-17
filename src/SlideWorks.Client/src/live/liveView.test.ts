import { describe, expect, it } from 'vitest';
import liveJson from '../../../contracts/screens/live.json';
import type { LiveView } from './liveView';

// Every key of LiveView, so a key added to the type without the JSON (or the other way round) fails here.
const KEYS: { [K in keyof LiveView]-?: true } = {
  sessionId: true,
  code: true,
  codeLabel: true,
  joinUrl: true,
  hostLabel: true,
  live: true,
  joinedCount: true,
  joinBar: true,
};

describe('LiveView', () => {
  it('has the keys of live.json', () => {
    expect(Object.keys(liveJson).sort()).toEqual(Object.keys(KEYS).sort());
  });

  it('types the sample as a LiveView', () => {
    const view: LiveView = liveJson;
    expect(view.codeLabel).toBe(`${view.code.slice(0, 3)} ${view.code.slice(3)}`);
    expect(view.joinUrl.endsWith(`/${view.code}`)).toBe(true);
  });
});
