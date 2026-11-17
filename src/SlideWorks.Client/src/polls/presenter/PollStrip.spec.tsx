import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { pollControlView } from '../../test-support/fixtures/pollStates';
import type { PollControlView } from '../types';
import { POLL_STRIP_TEST_IDS as IDS } from '../testIds';
import { PollControlFixture } from './PollControlFixture';
import { PollStrip } from './PollStrip';
import type { PollAction } from './usePollControl';

// The poll strip on the phone remote (SW-6 V8), mounted under the control fixture. The strip is the whole mount: its own
// width is the phone's, so the 360 px specs set the viewport.

const QUESTION = 'How big was your last merge request?';
const ALL = [IDS.open, IDS.close, IDS.reveal, IDS.showAnswer, IDS.newRound];

const strip = (
  control: PollControlView | null,
  extra: { error?: string | null; onAction?: (action: PollAction) => void } = {},
) => (
  <PollControlFixture deckId="deck-1" control={control} {...extra}>
    <PollStrip deckId="deck-1" />
  </PollControlFixture>
);

// Exactly these controls are on the page, in no other phase's company.
async function expectControls(page: Page, shown: string[]) {
  for (const id of ALL) await expect(page.getByTestId(id)).toHaveCount(shown.includes(id) ? 1 : 0);
}

test('the strip is not on the page, and takes no space, on a slide with no poll', async ({
  mount,
  page,
}) => {
  const component = await mount(strip(null));
  await expect(page.getByTestId(IDS.strip)).toHaveCount(0);
  expect((await component.boundingBox())?.height ?? 0).toBe(0);
});

test('an open poll shows its state, the count and the rate, with Close and Reveal', async ({
  mount,
  page,
}) => {
  await mount(strip(pollControlView('open', { answered: 38, joined: 41, roomSeesCounts: false })));
  await expect(page.getByTestId(IDS.state)).toHaveText('Open');
  await expect(page.getByTestId(IDS.answered)).toHaveText('38');
  await expect(page.getByTestId(IDS.strip)).toContainText('38 answers');
  await expect(page.getByTestId(IDS.rate)).toHaveText('93% of 41');
  await expectControls(page, [IDS.close, IDS.reveal]);
});

test('Reveal is offered only while the room does not already see the counts', async ({
  mount,
  page,
}) => {
  const component = await mount(strip(pollControlView('open', { roomSeesCounts: false })));
  await expect(page.getByTestId(IDS.reveal)).toBeVisible();
  await component.update(strip(pollControlView('open', { roomSeesCounts: true })));
  await expectControls(page, [IDS.close]);
});

test('a draft poll shows Open alone, with the count at zero', async ({ mount, page }) => {
  await mount(strip(pollControlView('draft')));
  await expect(page.getByTestId(IDS.state)).toHaveText('Draft');
  await expect(page.getByTestId(IDS.answered)).toHaveText('0');
  await expectControls(page, [IDS.open]);
});

test('a closed poll with a correct answer shows Show answer and New round, and not Close', async ({
  mount,
  page,
}) => {
  const closed = pollControlView('closed');
  closed.settings.correct = 2;
  closed.answerShown = false;
  const component = await mount(strip(closed));
  await expect(page.getByTestId(IDS.state)).toHaveText('Closed');
  await expectControls(page, [IDS.showAnswer, IDS.newRound]);

  await component.update(strip({ ...closed, answerShown: true }));
  await expectControls(page, [IDS.newRound]);

  await component.update(strip({ ...closed, settings: { ...closed.settings, correct: null } }));
  await expectControls(page, [IDS.newRound]);
});

test('with nobody joined the strip shows no percentage, never NaN or Infinity', async ({
  mount,
  page,
}) => {
  await mount(strip(pollControlView('open', { answered: 0, joined: 0 })));
  await expect(page.getByTestId(IDS.answered)).toHaveText('0');
  await expect(page.getByTestId(IDS.rate)).toHaveCount(0);
  await expect(page.getByTestId(IDS.strip)).not.toContainText(/NaN|Infinity|%/);
});

test('a single answer reads "1 answer"', async ({ mount, page }) => {
  await mount(strip(pollControlView('open', { answered: 1, joined: 4 })));
  await expect(page.getByTestId(IDS.strip)).toContainText('1 answer');
  await expect(page.getByTestId(IDS.strip)).not.toContainText('1 answers');
  await expect(page.getByTestId(IDS.rate)).toHaveText('25% of 4');
});

test('every button names the question and sends its own action', async ({ mount, page }) => {
  const actions: PollAction[] = [];
  const onAction = (action: PollAction) => void actions.push(action);
  const names: Record<string, [string, PollAction]> = {
    [IDS.open]: ['Open poll', 'open'],
    [IDS.close]: ['Close poll', 'close'],
    [IDS.reveal]: ['Reveal results', 'reveal'],
    [IDS.showAnswer]: ['Show answer', 'showAnswer'],
    [IDS.newRound]: ['New round', 'newRound'],
  };
  const closed = pollControlView('closed');
  closed.settings.correct = 1;
  closed.answerShown = false;
  const component = await mount(strip(pollControlView('draft'), { onAction }));
  const states = [
    pollControlView('draft'),
    pollControlView('open', { roomSeesCounts: false }),
    closed,
  ];
  for (const state of states) {
    await component.update(strip(state, { onAction }));
    for (const id of ALL) {
      if ((await page.getByTestId(id).count()) === 0) continue;
      const [name, action] = names[id];
      await expect(page.getByTestId(id)).toHaveAccessibleName(`${name}: ${QUESTION}`);
      await page.getByTestId(id).click();
      expect(actions.at(-1)).toBe(action);
    }
  }
  expect(actions).toEqual(['open', 'close', 'reveal', 'showAnswer', 'newRound']);
});

test('at 360 px every button is at least 48 px tall and wide, and nothing scrolls sideways', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 640 });
  const closed = pollControlView('closed', { question: `${QUESTION} ${QUESTION}` });
  closed.settings.correct = 1;
  closed.answerShown = false;
  const component = await mount(
    strip(closed, { error: 'Another poll is open on a different slide.' }),
  );
  const states = [
    pollControlView('draft'),
    pollControlView('open', { roomSeesCounts: false, answered: 1234, joined: 1234 }),
    closed,
  ];
  for (const state of states) {
    await component.update(strip(state, { error: 'Another poll is open on a different slide.' }));
    for (const id of ALL) {
      if ((await page.getByTestId(id).count()) === 0) continue;
      const box = (await page.getByTestId(id).boundingBox())!;
      expect(box.height).toBeGreaterThanOrEqual(48);
      expect(box.width).toBeGreaterThanOrEqual(48);
    }
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );
    expect(overflow).toBeLessThanOrEqual(0);
    const stripBox = (await page.getByTestId(IDS.strip).boundingBox())!;
    expect(stripBox.x + stripBox.width).toBeLessThanOrEqual(360);
  }
});

test('Close and New round never share a column, so a tap meant for one cannot land on the other', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 640 });
  const component = await mount(strip(pollControlView('open', { roomSeesCounts: false })));
  const close = (await page.getByTestId(IDS.close).boundingBox())!;
  await component.update(strip(pollControlView('closed')));
  const newRound = (await page.getByTestId(IDS.newRound).boundingBox())!;
  expect(newRound.x).toBeGreaterThanOrEqual(close.x + close.width);
});

test('a failed call shows its message as given, announced', async ({ mount, page }) => {
  const component = await mount(strip(pollControlView('open')));
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
  await component.update(strip(pollControlView('open'), { error: 'The poll is already closed.' }));
  await expect(page.getByTestId(IDS.error)).toHaveText('The poll is already closed.');
  await expect(page.getByTestId(IDS.error)).toHaveAttribute('role', 'alert');
});

test('a count that changes leaves focus on the button', async ({ mount, page }) => {
  const component = await mount(strip(pollControlView('open', { answered: 10, joined: 40 })));
  await page.getByTestId(IDS.close).focus();
  await component.update(strip(pollControlView('open', { answered: 11, joined: 40 })));
  await expect(page.getByTestId(IDS.answered)).toHaveText('11');
  await expect(page.getByTestId(IDS.close)).toBeFocused();
});

test('when the button under the thumb goes away with the phase, focus moves to the strip', async ({
  mount,
  page,
}) => {
  const component = await mount(strip(pollControlView('open')));
  await page.getByTestId(IDS.close).focus();
  await component.update(strip(pollControlView('closed')));
  await expect(page.getByTestId(IDS.close)).toHaveCount(0);
  await expect(page.getByTestId(IDS.strip)).toBeFocused();
});

test('in forced-colors mode each state and each button keeps a visible edge and its text', async ({
  mount,
  page,
}) => {
  await page.emulateMedia({ forcedColors: 'active' });
  const closed = pollControlView('closed');
  closed.settings.correct = 1;
  closed.answerShown = false;
  const component = await mount(strip(pollControlView('draft')));
  const states: [PollControlView, string, string[]][] = [
    [pollControlView('draft'), 'Draft', [IDS.open]],
    [pollControlView('open', { roomSeesCounts: false }), 'Open', [IDS.close, IDS.reveal]],
    [closed, 'Closed', [IDS.showAnswer, IDS.newRound]],
  ];
  const labels = new Map<string, string>();
  for (const [state, text, buttons] of states) {
    await component.update(strip(state));
    await expect(page.getByTestId(IDS.state)).toHaveText(text);
    labels.set(text, text);
    const pill = await page
      .getByTestId(IDS.state)
      .evaluate((el) => getComputedStyle(el).borderTopWidth);
    expect(parseFloat(pill)).toBeGreaterThan(0);
    for (const id of buttons) {
      const button = page.getByTestId(id);
      await expect(button).toBeVisible();
      await expect(button).not.toHaveText('');
      const width = await button.evaluate((el) => getComputedStyle(el).borderTopWidth);
      expect(parseFloat(width)).toBeGreaterThan(0);
    }
  }
  expect(labels.size).toBe(3);
});
