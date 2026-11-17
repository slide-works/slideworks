import { act, Component, createElement, useEffect, type ReactNode } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { pollControlView } from '../../test-support/fixtures/pollStates';
import type { PollControlView } from '../types';
import { PollControlFixture } from './PollControlFixture';
import { PollControlLive } from './PollControlLive';
import { usePollControl, type PollAction, type PollControlValue } from './usePollControl';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

// The real provider (PollControlLive) holds the page's hub connection, which SignalR can't build outside a browser. A
// connection that starts and whose calls never answer: the audience watch never comes in, so nothing is read.
vi.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    withUrl() {
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    configureLogging() {
      return this;
    }
    build() {
      return {
        start: async () => undefined,
        stop: async () => undefined,
        invoke: () => new Promise(() => undefined),
        on: () => undefined,
        onreconnecting: () => undefined,
        onreconnected: () => undefined,
        onclose: () => undefined,
      };
    }
  }
  return { HubConnectionBuilder, HttpTransportType: { WebSockets: 1 }, LogLevel: { None: 6 } };
});

const deckId = 'deck-1';
let root: Root;
let seen: PollControlValue | null;
let thrown: unknown;

const report = (value: PollControlValue) => {
  seen = value;
};

function Reader({ deck }: { deck: string }) {
  const value = usePollControl(deck);
  useEffect(() => report(value));
  return null;
}

// Catches what the reader throws, so a wiring mistake can be asserted.
class Catch extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  componentDidCatch(error: unknown) {
    thrown = error;
  }
  render() {
    return this.state.failed ? null : this.props.children;
  }
}

async function read(wrap: (children: ReactNode) => ReactNode, deck = deckId) {
  seen = null;
  thrown = undefined;
  const reader = createElement(Catch, null, createElement(Reader, { deck }));
  await act(async () => root.render(wrap(reader)));
  return seen as PollControlValue | null;
}

const fixture =
  (
    control: PollControlView | null,
    error: string | null = null,
    onAction?: (a: PollAction) => void,
  ) =>
  (children: ReactNode) =>
    createElement(PollControlFixture, { deckId, control, error, onAction, children });

beforeEach(() => {
  root = createRoot(document.createElement('div'));
});

afterEach(async () => {
  await act(() => root.unmount());
});

describe('PollControlFixture', () => {
  for (const phase of ['draft', 'open', 'closed'] as const) {
    it(`reaches ${phase}`, async () => {
      const control = pollControlView(phase);
      expect((await read(fixture(control)))?.control).toEqual(control);
    });
  }

  it('reaches a slide with no poll', async () => {
    expect((await read(fixture(null)))?.control).toBeNull();
  });

  it('reaches revealed, the answer shown and earlier rounds', async () => {
    const control = pollControlView('closed', {
      revealed: true,
      answerShown: true,
      rounds: [
        { number: 1, answered: 38, joined: 41 },
        { number: 2, answered: 36, joined: 41 },
      ],
    });
    expect((await read(fixture(control)))?.control).toEqual(control);
  });

  it('reaches a refused call', async () => {
    const value = await read(fixture(pollControlView('open'), 'The poll is not open.'));
    expect(value?.error).toBe('The poll is not open.');
  });

  it('tells the test each action, by name, and settles', async () => {
    const onAction = vi.fn();
    const value = (await read(fixture(pollControlView('open'), null, onAction)))!;
    await act(async () => {
      await value.open();
      await value.close();
      await value.reveal();
      await value.showAnswer();
      await value.newRound();
    });
    expect(onAction.mock.calls.map(([action]) => action)).toEqual([
      'open',
      'close',
      'reveal',
      'showAnswer',
      'newRound',
    ]);
  });
});

describe('usePollControl', () => {
  it('throws outside a provider', async () => {
    await read((children) => children);
    expect(thrown).toBeInstanceOf(Error);
  });

  it("throws under another deck's provider", async () => {
    await read(fixture(pollControlView('open')), 'deck-2');
    expect(String(thrown)).toContain('deck-2');
  });

  it('gives no control and no error under the live provider before its audience watch is in', async () => {
    const live = (children: ReactNode) =>
      createElement(PollControlLive, { deckId, slide: 4, children });
    const value = await read(live);
    expect(value?.control).toBeNull();
    expect(value?.error).toBeNull();
  });
});
