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
  await page.clock.install();
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
    await page.clock.runFor(600);
    for (const id of ALL) {
      if ((await page.getByTestId(id).count()) === 0) continue;
      const [name, action] = names[id];
      await expect(page.getByTestId(id)).toHaveAccessibleName(`${name}: ${QUESTION}`);
      await page.getByTestId(id).click();
      expect(actions.at(-1)).toBe(action);
      await page.clock.runFor(600);
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

test('Open, Close and New round sit in columns that never overlap Close', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 640 });
  const component = await mount(strip(pollControlView('open', { roomSeesCounts: false })));
  const close = (await page.getByTestId(IDS.close).boundingBox())!;
  const reveal = (await page.getByTestId(IDS.reveal).boundingBox())!;
  await component.update(strip(pollControlView('closed')));
  const newRound = (await page.getByTestId(IDS.newRound).boundingBox())!;
  await component.update(strip(pollControlView('draft')));
  const open = (await page.getByTestId(IDS.open).boundingBox())!;
  expect(open.x).toBeGreaterThanOrEqual(reveal.x + reveal.width);
  expect(newRound.x).toBeGreaterThanOrEqual(reveal.x + reveal.width);
  expect(open.x).toBeGreaterThanOrEqual(close.x + close.width);
});

test('a double tap on Open sends one open and nothing else', async ({ mount, page }) => {
  await page.clock.install();
  const actions: PollAction[] = [];
  await mount(strip(pollControlView('draft'), { onAction: (a) => void actions.push(a) }));
  await page.clock.runFor(600);
  await page.getByTestId(IDS.open).dblclick();
  expect(actions).toEqual(['open']);
});

test('a tap where Open was, right after the poll opens, does nothing; after the guard time Close works', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 640 });
  await page.clock.install();
  const actions: PollAction[] = [];
  const onAction = (a: PollAction) => void actions.push(a);
  const component = await mount(strip(pollControlView('draft'), { onAction }));
  await page.clock.runFor(600);
  const open = (await page.getByTestId(IDS.open).boundingBox())!;
  await page.getByTestId(IDS.open).click();
  await component.update(strip(pollControlView('open', { roomSeesCounts: false }), { onAction }));
  await expect(page.getByTestId(IDS.close)).toBeVisible();

  // Where Open was: nothing now sits there that could be mistaken, and a tap anywhere on the strip is ignored.
  await page.mouse.click(open.x + open.width / 2, open.y + open.height / 2);
  await page.getByTestId(IDS.close).click({ force: true });
  await page.getByTestId(IDS.reveal).click({ force: true });
  expect(actions).toEqual(['open']);

  await page.clock.runFor(600);
  await page.getByTestId(IDS.close).click();
  expect(actions).toEqual(['open', 'close']);
});

test('the guard does not draw the buttons disabled', async ({ mount, page }) => {
  await page.clock.install();
  const component = await mount(strip(pollControlView('draft')));
  await page.clock.runFor(600);
  await component.update(strip(pollControlView('open', { roomSeesCounts: false })));
  await expect(page.getByTestId(IDS.close)).toBeEnabled();
  await expect(page.getByTestId(IDS.close)).not.toHaveAttribute('aria-busy');
});

test('the rate never shows above 100%, and the real numbers stay', async ({ mount, page }) => {
  await mount(strip(pollControlView('open', { answered: 45, joined: 40 })));
  await expect(page.getByTestId(IDS.rate)).toHaveText('100% of 40');
  await expect(page.getByTestId(IDS.answered)).toHaveText('45');
});

test('the strip is named with its state and question, so a screen reader hears the new state', async ({
  mount,
  page,
}) => {
  const component = await mount(strip(pollControlView('open')));
  await expect(page.getByTestId(IDS.strip)).toHaveAccessibleName(`Poll, open: ${QUESTION}`);
  await component.update(strip(pollControlView('closed')));
  await expect(page.getByTestId(IDS.strip)).toHaveAccessibleName(`Poll, closed: ${QUESTION}`);
});

test('the answer count is announced politely, once at first and then calmly', async ({
  mount,
  page,
}) => {
  const component = await mount(strip(pollControlView('open', { answered: 10, joined: 40 })));
  const region = page.getByTestId(IDS.strip).getByRole('status');
  await expect(region).toHaveAttribute('aria-live', 'polite');
  await expect(region).toHaveText('10 answers');
  await component.update(strip(pollControlView('open', { answered: 11, joined: 40 })));
  await expect(page.getByTestId(IDS.answered)).toHaveText('11');
  await expect(region).toHaveText('11 answers');
});

test('focus that left the strip is not pulled back by a later phase change', async ({
  mount,
  page,
}) => {
  const component = await mount(
    <>
      <p data-testid="elsewhere" style={{ marginTop: 200 }}>
        Notes
      </p>
      {strip(pollControlView('open'))}
    </>,
  );
  await page.getByTestId(IDS.close).focus();
  await page.getByTestId('elsewhere').click();
  await component.update(
    <>
      <p data-testid="elsewhere" style={{ marginTop: 200 }}>
        Notes
      </p>
      {strip(pollControlView('closed'))}
    </>,
  );
  await expect(page.getByTestId(IDS.close)).toHaveCount(0);
  await expect(page.getByTestId(IDS.strip)).not.toBeFocused();
});

test('a failed call shows its message as given, announced', async ({ mount, page }) => {
  const component = await mount(strip(pollControlView('open')));
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
  await component.update(strip(pollControlView('open'), { error: 'The poll is already closed.' }));
  await expect(page.getByTestId(IDS.error)).toHaveText('The poll is already closed.');
  await expect(page.getByTestId(IDS.error)).toHaveAttribute('role', 'alert');
});

test('an error is laid over the preview above the strip: the strip keeps its height, and the message can be dismissed', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 568 });
  const component = await mount(
    <div style={{ marginTop: 300 }}>{strip(pollControlView('open'))}</div>,
  );
  const before = (await page.getByTestId(IDS.strip).boundingBox())!;
  await component.update(
    <div style={{ marginTop: 300 }}>
      {strip(pollControlView('open'), {
        error: "The deck isn't live, so the poll can't open. Go live first, then open it.",
      })}
    </div>,
  );
  const after = (await page.getByTestId(IDS.strip).boundingBox())!;
  const message = (await page.getByTestId(IDS.error).boundingBox())!;
  expect(after.height).toBe(before.height);
  expect(after.y).toBe(before.y);
  expect(message.y + message.height).toBeLessThanOrEqual(after.y);
  const dismiss = page.getByRole('button', { name: 'Dismiss this message' });
  expect((await dismiss.boundingBox())!.height).toBeGreaterThanOrEqual(44);
  await dismiss.click();
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
});

test('a dismissed error shows again when the same one arrives after the error cleared', async ({
  mount,
  page,
}) => {
  const message = "Couldn't load this slide's poll. Check your connection.";
  const component = await mount(strip(pollControlView('open'), { error: message }));
  await page.getByRole('button', { name: 'Dismiss this message' }).click();
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);

  await component.update(strip(pollControlView('open'), { error: null }));
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
  await component.update(strip(pollControlView('open'), { error: message }));
  await expect(page.getByTestId(IDS.error)).toHaveText(message);
});

test('dismissing the error from the keyboard leaves focus on the strip', async ({
  mount,
  page,
}) => {
  await mount(strip(pollControlView('open'), { error: 'The poll had already moved on.' }));
  await page.getByRole('button', { name: 'Dismiss this message' }).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
  await expect(page.getByTestId(IDS.strip)).toBeFocused();
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

test('in forced-colors mode every state and button gets a 2 px edge, and differs by its text', async ({
  mount,
  page,
}) => {
  const closed = pollControlView('closed');
  closed.settings.correct = 1;
  closed.answerShown = false;
  const states: [PollControlView, string, string[]][] = [
    [pollControlView('draft'), 'Draft', [IDS.open]],
    [pollControlView('open', { roomSeesCounts: false }), 'Open', [IDS.close, IDS.reveal]],
    [closed, 'Closed', [IDS.showAnswer, IDS.newRound]],
  ];
  const component = await mount(strip(states[0][0]));
  const edges = (id: string) =>
    page.getByTestId(id).evaluate((el) => {
      const style = getComputedStyle(el);
      return { width: parseFloat(style.borderTopWidth), color: style.borderTopColor };
    });
  const seen = new Map<string, number>();

  // Without forced colors the edges are 1 px (the pill and the plain buttons): the 2 px below is the mode's doing.
  await component.update(strip(states[1][0]));
  expect((await edges(IDS.state)).width).toBe(1);
  expect((await edges(IDS.reveal)).width).toBe(1);

  await page.emulateMedia({ forcedColors: 'active' });
  for (const [state, text, buttons] of states) {
    await component.update(strip(state));
    await expect(page.getByTestId(IDS.state)).toHaveText(text);
    expect((await edges(IDS.state)).width).toBe(2);
    for (const id of buttons) {
      await expect(page.getByTestId(id)).not.toHaveText('');
      const edge = await edges(id);
      expect(edge.width).toBe(2);
      // Open is transparent outside forced colors; the mode gives it a system colour.
      expect(edge.color).not.toBe('rgba(0, 0, 0, 0)');
      seen.set(await page.getByTestId(id).innerText(), edge.width);
    }
  }
  expect([...seen.keys()].sort()).toEqual(['Close', 'New round', 'Open', 'Reveal', 'Show answer']);
});
