import { act, createElement, useEffect, type ReactNode } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { pollControlView } from '../test-support/fixtures/pollStates';
import type { PollControlView } from '../polls/types';
import { usePollScreen, type PollScreen } from './PollContext';
import { PollScreenFixture } from './PollScreenFixture';
import { PollScreenLive } from './PollScreenLive';
import { SlideNumberContext } from './slideNumber';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let root: Root;
let seen: PollScreen | null | 'unset';

const report = (screen: PollScreen | null) => {
  seen = screen;
};

function Reader() {
  const screen = usePollScreen();
  useEffect(() => report(screen));
  return null;
}

// Renders a reader on slide `slide` (or outside any slide) under `wrap`, and returns what usePollScreen gave.
async function screenOn(slide: number | null, wrap: (children: ReactNode) => ReactNode) {
  seen = 'unset';
  const reader = createElement(Reader);
  const onSlide =
    slide === null ? reader : createElement(SlideNumberContext, { value: slide }, reader);
  await act(async () => root.render(wrap(onSlide)));
  return seen;
}

const fixture = (control: PollControlView | null) => (children: ReactNode) =>
  createElement(PollScreenFixture, { control, children });

beforeEach(() => {
  root = createRoot(document.createElement('div'));
});

afterEach(async () => {
  await act(() => root.unmount());
});

describe('usePollScreen', () => {
  it('gives null outside a provider, as on the deck page and the render page', async () => {
    expect(await screenOn(4, (children) => children)).toBeNull();
  });

  it('gives null on a slide the provider is not for', async () => {
    expect(await screenOn(3, fixture(pollControlView('open')))).toBeNull();
  });

  it('gives null outside a slide', async () => {
    expect(await screenOn(null, fixture(pollControlView('open')))).toBeNull();
  });

  it('gives null when the provider has no live data', async () => {
    expect(await screenOn(4, fixture(null))).toBeNull();
  });

  it('gives null under the live provider stub', async () => {
    const live = (children: ReactNode) =>
      createElement(PollScreenLive, { deckId: 'deck-1', owner: true, children });
    expect(await screenOn(4, live)).toBeNull();
  });
});

describe('PollScreenFixture', () => {
  it('reaches draft: no answers, no counts shown yet', async () => {
    const control = pollControlView('draft');
    expect(await screenOn(control.slide, fixture(control))).toEqual({
      phase: 'draft',
      number: control.number,
      answered: 0,
      counts: control.roomSeesCounts ? control.counts : null,
      answerShown: false,
      correct: null,
      pick: control.settings.pick,
      max: control.settings.max,
    });
  });

  it('reaches open with the counts the room sees', async () => {
    const control = pollControlView('open', { roomSeesCounts: true, counts: [7, 17, 10, 4] });
    const screen = await screenOn(control.slide, fixture(control));
    expect(screen).toMatchObject({
      phase: 'open',
      answered: control.answered,
      counts: [7, 17, 10, 4],
    });
  });

  it('reaches open with the counts hidden from the room', async () => {
    const control = pollControlView('open', { roomSeesCounts: false, revealed: false });
    const screen = await screenOn(control.slide, fixture(control));
    expect(screen).toMatchObject({ phase: 'open', answered: control.answered, counts: null });
  });

  it('reaches closed, and keeps the correct option back until the answer is shown', async () => {
    const control = pollControlView('closed', {
      settings: { ...pollControlView('closed').settings, correct: 2 },
      answerShown: false,
    });
    expect(await screenOn(control.slide, fixture(control))).toMatchObject({
      phase: 'closed',
      answerShown: false,
      correct: null,
    });
  });

  it('reaches the answer shown, with the correct option', async () => {
    const closed = pollControlView('closed');
    const control = { ...closed, settings: { ...closed.settings, correct: 2 }, answerShown: true };
    expect(await screenOn(control.slide, fixture(control))).toMatchObject({
      phase: 'closed',
      answerShown: true,
      correct: 2,
    });
  });

  it('reaches pick several, and a later round', async () => {
    const open = pollControlView('open');
    const control = { ...open, number: 2, settings: { ...open.settings, pick: 'upTo', max: 2 } };
    expect(await screenOn(control.slide, fixture(control as PollControlView))).toMatchObject({
      number: 2,
      pick: 'upTo',
      max: 2,
    });
  });
});
