import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import fitFixturesJson from '@contracts/fit-fixtures/poll.json' with { type: 'json' };
import type { PollControlView } from '../../polls/types';
import {
  POLL_SLIDE_TEST_IDS as IDS,
  pollSlideBarTestId,
  pollSlideCorrectTestId,
  pollSlidePercentTestId,
  pollSlideRowTestId,
} from '../../polls/testIds';
import { LAYOUT_SLOTS } from '../../test-support/fixtures/layoutSlots';
import { PALETTES, lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { pollControlView } from '../../test-support/fixtures/pollStates';
import { deckPlay } from '../../test-support/fixtures/screens';
import { JoinProvider } from '../JoinContext';
import { PollScreenFixture } from '../PollScreenFixture';
import { SlideRenderer } from '../SlideRenderer';
import type { SlideDeck, Slots } from '../slots';

test.use({ viewport: { width: 1920, height: 1080 } });

// The poll slide on its own, as the player draws it, with live data from the frozen fixture provider. The slide is
// number 4, the poll-control.json samples' slide.
const SLIDE = 4;
const notLive = { ...deckPlay.join, live: false };
const live = { ...deckPlay.join, live: true };

function deckIn(palette = 'cobalt'): SlideDeck {
  return {
    title: 'Ship smaller',
    author: 'Sam Rivera',
    parts: [],
    look: lookViewOf('talk', palette),
    deepDives: [],
  };
}

function pollSlide({
  control,
  slots = LAYOUT_SLOTS.poll,
  join = notLive,
  palette = 'cobalt',
}: {
  control: PollControlView | null;
  slots?: Slots['poll'];
  join?: typeof live;
  palette?: string;
}) {
  return (
    <JoinProvider deckId={deckPlay.id} join={join} owner={false}>
      <PollScreenFixture control={control}>
        <SlideRenderer
          slide={{ number: SLIDE, layout: 'poll', part: null, deepDive: false, slots }}
          deck={deckIn(palette)}
          total={6}
          step={0}
        />
      </PollScreenFixture>
    </JoinProvider>
  );
}

// The sample's open round with the counts the room sees: 38 answers over the four options.
const openLive = (overrides: Partial<PollControlView> = {}) =>
  pollControlView('open', {
    roomSeesCounts: true,
    answered: 38,
    counts: [7, 17, 10, 4],
    ...overrides,
  });

// The sample's closed round (pick up to 2, 38 answers) before the answer is shown.
const closedRound = () => pollControlView('closed', { answerShown: false });

const width = (page: Page, testId: string) =>
  page.getByTestId(testId).evaluate((el) => el.getBoundingClientRect().width);

test('with no live data the slide draws the question, the options and empty tracks', async ({
  mount,
  page,
}) => {
  await mount(pollSlide({ control: null }));
  await expect(page.getByTestId(IDS.question)).toHaveText(LAYOUT_SLOTS.poll.question);
  await expect(page.getByTestId(IDS.kind)).toHaveText('Poll');
  for (const [position, option] of LAYOUT_SLOTS.poll.options.entries()) {
    await expect(page.getByTestId(pollSlideRowTestId(position))).toHaveText(option);
    await expect(page.getByTestId(pollSlideBarTestId(position))).toHaveCount(0);
    await expect(page.getByTestId(pollSlidePercentTestId(position))).toBeEmpty();
  }
  for (const id of [IDS.state, IDS.answered, IDS.hiddenNote, IDS.manyNote, IDS.round, IDS.join])
    await expect(page.getByTestId(id)).toHaveCount(0);
});

test('a poll not open yet says so, with empty tracks and no count', async ({ mount, page }) => {
  await mount(pollSlide({ control: pollControlView('draft') }));
  await expect(page.getByTestId(IDS.state)).toHaveText('Not open yet');
  await expect(page.getByTestId(IDS.kind)).toHaveText('Poll · Pick one');
  await expect(page.getByTestId(IDS.answered)).toHaveCount(0);
  await expect(page.getByTestId(pollSlideBarTestId(0))).toHaveCount(0);
  await expect(page.getByTestId(pollSlidePercentTestId(0))).toBeEmpty();
});

test('an open poll the room sees draws bars sized by the share of people who answered', async ({
  mount,
  page,
}) => {
  await mount(pollSlide({ control: openLive() }));
  await expect(page.getByTestId(IDS.state)).toHaveText('Open');
  await expect(page.getByTestId(IDS.answered)).toHaveText('38 answers');
  await expect(page.getByTestId(IDS.hiddenNote)).toHaveCount(0);
  const expected = ['18%', '45%', '26%', '11%'];
  for (const [position, percent] of expected.entries())
    await expect(page.getByTestId(pollSlidePercentTestId(position))).toHaveText(percent);
  // 17 of 38 against 7 of 38, both on the same track width.
  const track = await page
    .getByTestId(pollSlideBarTestId(1))
    .evaluate((bar) => bar.parentElement!.getBoundingClientRect().width);
  await expect.poll(() => width(page, pollSlideBarTestId(1))).toBeCloseTo((track * 45) / 100, 0);
  await expect.poll(() => width(page, pollSlideBarTestId(0))).toBeCloseTo((track * 18) / 100, 0);
});

test('the bars take the palette accent', async ({ mount, page }) => {
  await mount(pollSlide({ control: openLive() }));
  const [bar, accent] = await page.getByTestId(pollSlideBarTestId(0)).evaluate((el) => {
    const probe = document.createElement('span');
    probe.style.color = 'var(--sw-accent)';
    el.append(probe);
    return [getComputedStyle(el).backgroundColor, getComputedStyle(probe).color];
  });
  expect(bar).toBe(accent);
});

test('with nobody answered every bar is empty and every share is 0%', async ({ mount, page }) => {
  await mount(pollSlide({ control: openLive({ answered: 0, counts: [0, 0, 0, 0] }) }));
  await expect(page.getByTestId(IDS.answered)).toHaveText('0 answers');
  for (const position of [0, 1, 2, 3]) {
    await expect(page.getByTestId(pollSlidePercentTestId(position))).toHaveText('0%');
    expect(await width(page, pollSlideBarTestId(position))).toBe(0);
  }
  await expect(page.getByTestId(IDS.rows)).not.toContainText('NaN');
});

test('one answer is counted in the singular', async ({ mount, page }) => {
  await mount(pollSlide({ control: openLive({ answered: 1, counts: [0, 1, 0, 0] }) }));
  await expect(page.getByTestId(IDS.answered)).toHaveText('1 answer');
  await expect(page.getByTestId(pollSlidePercentTestId(1))).toHaveText('100%');
});

test('while the room cannot see the counts the tracks are hatched and only the count shows', async ({
  mount,
  page,
}) => {
  // The sample's open round: results after close, 21 answers, counts hidden from the room.
  await mount(pollSlide({ control: pollControlView('open') }));
  await expect(page.getByTestId(IDS.state)).toHaveText('Open');
  await expect(page.getByTestId(IDS.answered)).toHaveText('21 answers');
  await expect(page.getByTestId(IDS.hiddenNote)).toHaveText('Results show when the poll closes');
  for (const position of [0, 1, 2, 3]) {
    await expect(page.getByTestId(pollSlideBarTestId(position))).toHaveCount(0);
    await expect(page.getByTestId(pollSlidePercentTestId(position))).toBeEmpty();
  }
  const hatch = await page
    .getByTestId(pollSlideRowTestId(0))
    .evaluate((row) => getComputedStyle(row.children[1]).backgroundImage);
  expect(hatch).toContain('repeating-linear-gradient');
  // Not a count anywhere on the slide but the answer count.
  await expect(page.getByTestId(IDS.rows)).not.toContainText('%');
});

test('a closed poll shows the final result and says Closed', async ({ mount, page }) => {
  const closed = closedRound();
  await mount(pollSlide({ control: closed }));
  await expect(page.getByTestId(IDS.state)).toHaveText('Closed');
  await expect(page.getByTestId(IDS.answered)).toHaveText(`${closed.answered} answers`);
  await expect(page.getByTestId(IDS.hiddenNote)).toHaveCount(0);
  for (const [position, count] of closed.counts.entries())
    await expect(page.getByTestId(pollSlidePercentTestId(position))).toHaveText(
      `${Math.round((count / closed.answered) * 100)}%`,
    );
  await expect(page.getByTestId(pollSlideCorrectTestId(0))).toHaveCount(0);
});

test('the answer shown: the correct bar turns positive with a tick and the others fade', async ({
  mount,
  page,
}) => {
  const closed = closedRound();
  const control = {
    ...closed,
    answerShown: true,
    settings: { ...closed.settings, correct: 2 },
  };
  await mount(pollSlide({ control: control }));
  const tick = page.getByTestId(pollSlideCorrectTestId(1));
  await expect(tick).toBeVisible();
  await expect(tick).toHaveAccessibleName('Correct answer');
  for (const position of [0, 2, 3])
    await expect(page.getByTestId(pollSlideCorrectTestId(position))).toHaveCount(0);
  const [bar, good] = await page.getByTestId(pollSlideBarTestId(1)).evaluate((el) => {
    const probe = document.createElement('span');
    probe.style.color = 'var(--sw-good)';
    el.append(probe);
    return [getComputedStyle(el).backgroundColor, getComputedStyle(probe).color];
  });
  expect(bar).toBe(good);
  const opacity = (position: number) =>
    page.getByTestId(pollSlideRowTestId(position)).evaluate((row) => getComputedStyle(row).opacity);
  await expect.poll(() => opacity(1)).toBe('1');
  for (const position of [0, 2, 3]) await expect.poll(() => opacity(position)).toBe('0.42');
});

test('a correct option is not marked before the answer is shown', async ({ mount, page }) => {
  const closed = closedRound();
  await mount(pollSlide({ control: { ...closed, settings: { ...closed.settings, correct: 2 } } }));
  await expect(page.getByTestId(IDS.state)).toHaveText('Closed');
  await expect(page.getByTestId(pollSlideCorrectTestId(1))).toHaveCount(0);
});

test('pick up to 2 says so, and the footer says the shares add up to more than 100%', async ({
  mount,
  page,
}) => {
  const control = openLive({
    answered: 36,
    counts: [24, 15, 9, 13],
    settings: { ...openLive().settings, pick: 'upTo', max: 2, results: 'live' },
  });
  await mount(pollSlide({ control: control }));
  await expect(page.getByTestId(IDS.kind)).toHaveText('Poll · Pick up to 2');
  await expect(page.getByTestId(IDS.manyNote)).toHaveText(
    'Share of people who picked each. Adds up to more than 100%.',
  );
  await expect(page.getByTestId(pollSlidePercentTestId(0))).toHaveText('67%');
});

test('a later round says which round it is', async ({ mount, page }) => {
  await mount(pollSlide({ control: openLive({ number: 2 }) }));
  await expect(page.getByTestId(IDS.round)).toHaveText('Round 2');
});

test('round 1 says no round', async ({ mount, page }) => {
  await mount(pollSlide({ control: openLive({ number: 1 }) }));
  await expect(page.getByTestId(IDS.state)).toHaveText('Open');
  await expect(page.getByTestId(IDS.round)).toHaveCount(0);
});

test('the join pill shows the address and code while live, top right', async ({ mount, page }) => {
  await mount(pollSlide({ control: openLive(), join: live }));
  const pill = page.getByTestId(IDS.join);
  await expect(pill).toHaveText(`${live.hostLabel} · ${live.codeLabel}`);
  const frame = (await page.getByTestId('slide-frame').boundingBox())!;
  const box = (await pill.boundingBox())!;
  // Top right, under the part tracker (y 40-70) and above the content box.
  expect(box.x + box.width - frame.x).toBeLessThanOrEqual(1800.5);
  expect(box.y - frame.y).toBeGreaterThanOrEqual(70);
  expect(box.y + box.height - frame.y).toBeLessThanOrEqual(150.5);
});

test('the join pill is not drawn while the deck is not live', async ({ mount, page }) => {
  await mount(pollSlide({ control: openLive(), join: notLive }));
  await expect(page.getByTestId(IDS.state)).toHaveText('Open');
  await expect(page.getByTestId(IDS.join)).toHaveCount(0);
});

// Everything the layout draws but the join pill, which sits above the content box, in canvas px from the slide's top
// left; and whether any option label or the question is wider than its box.
async function measure(page: Page) {
  await page.evaluate(() => document.fonts.ready);
  return page.getByTestId('layout-poll').evaluate((root: Element) => {
    const slide = root.closest('[data-layout]')!.getBoundingClientRect();
    const drawn = { left: Infinity, top: Infinity, right: -Infinity, bottom: -Infinity };
    // The top is the boxes' top: a text run's box reaches a little above its line box.
    let boxTop = Infinity;
    const add = (rect: DOMRect) => {
      if (rect.width === 0 && rect.height === 0) return;
      drawn.left = Math.min(drawn.left, rect.left - slide.left);
      drawn.top = Math.min(drawn.top, rect.top - slide.top);
      drawn.right = Math.max(drawn.right, rect.right - slide.left);
      drawn.bottom = Math.max(drawn.bottom, rect.bottom - slide.top);
    };
    const pill = root.querySelector('[data-testid="poll-slide-join"]');
    for (const element of root.querySelectorAll('*')) {
      if (pill?.contains(element)) continue;
      const style = getComputedStyle(element);
      if (style.display === 'contents') continue;
      const box = element.getBoundingClientRect();
      if (box.width > 0 || box.height > 0) boxTop = Math.min(boxTop, box.top - slide.top);
      if (
        element instanceof SVGElement ||
        style.backgroundColor !== 'rgba(0, 0, 0, 0)' ||
        style.backgroundImage !== 'none'
      )
        add(element.getBoundingClientRect());
    }
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
      if (pill?.contains(node)) continue;
      const range = document.createRange();
      range.selectNodeContents(node);
      for (const rect of range.getClientRects()) add(rect);
    }
    // Every descendant, not only the text elements: a bar, a pill or a wrapper that scrolls sideways counts as well.
    const overflowing = [...root.querySelectorAll('*')]
      .filter((el) => !pill?.contains(el) && el.scrollWidth > el.clientWidth + 1)
      .map((el) => el.textContent || el.tagName);
    return { drawn: { ...drawn, top: boxTop }, overflowing };
  });
}

const POLL_EIGHT = fitFixturesJson.pollEight.slots;
const TWO = { question: 'Ship on Friday?', options: ['Yes', 'No'] };
// The longest footer: pick several, a later round, every count shown.
const busy = (options: number) =>
  openLive({
    number: 2,
    answered: 1000,
    counts: Array.from({ length: options }, (_, i) => 1000 - i * 100),
    settings: { ...openLive().settings, pick: 'upTo', max: 2, results: 'live' },
  });

for (const palette of PALETTES.map((p) => p.name)) {
  for (const [name, slots] of [
    ['8 options of 60 characters', POLL_EIGHT],
    ['6 options of 60 characters', { ...POLL_EIGHT, options: POLL_EIGHT.options.slice(0, 6) }],
    ['4 options of 60 characters', { ...POLL_EIGHT, options: POLL_EIGHT.options.slice(0, 4) }],
    ['2 options', TWO],
  ] as const) {
    test(`${name} stay inside x 120-1800, y 150-996 in ${palette}`, async ({ mount, page }) => {
      await mount(
        pollSlide({
          control: busy(slots.options.length),
          slots: { question: slots.question, options: [...slots.options] },
          join: live,
          palette: palette,
        }),
      );
      await expect(page.getByTestId(IDS.manyNote)).toBeVisible();
      const { drawn, overflowing } = await measure(page);
      expect(overflowing).toEqual([]);
      expect(drawn.left).toBeGreaterThanOrEqual(119.5);
      expect(drawn.right).toBeLessThanOrEqual(1800.5);
      expect(drawn.top).toBeGreaterThanOrEqual(149.5);
      expect(drawn.bottom).toBeLessThanOrEqual(996.5);
      await expect(page.getByTestId(pollSlideRowTestId(slots.options.length - 1))).toBeVisible();
    });
  }
}

test('8 options of 60 characters with the counts hidden stay inside y 150-996', async ({
  mount,
  page,
}) => {
  await mount(
    pollSlide({
      control: pollControlView('open', { counts: Array.from({ length: 8 }, () => 0) }),
      slots: { question: POLL_EIGHT.question, options: [...POLL_EIGHT.options] },
      join: live,
    }),
  );
  await expect(page.getByTestId(IDS.hiddenNote)).toBeVisible();
  const { drawn, overflowing } = await measure(page);
  expect(overflowing).toEqual([]);
  expect(drawn.bottom).toBeLessThanOrEqual(996.5);
  expect(drawn.right).toBeLessThanOrEqual(1800.5);
});

const SCREENSHOTS: [string, PollControlView | null][] = [
  ['no-live-data', null],
  ['draft', pollControlView('draft')],
  ['open-live', openLive()],
  ['open-hidden', pollControlView('open')],
  ['closed', closedRound()],
  [
    'answer-shown',
    {
      ...closedRound(),
      answerShown: true,
      settings: { ...closedRound().settings, correct: 2 },
    },
  ],
  [
    'pick-up-to-2',
    openLive({
      answered: 36,
      counts: [24, 15, 9, 13],
      settings: { ...openLive().settings, pick: 'upTo', max: 2, results: 'live' },
    }),
  ],
];

for (const [name, control] of SCREENSHOTS) {
  test(`screenshot: poll slide ${name}`, async ({ mount, page }) => {
    await mount(pollSlide({ control: control, join: control === null ? notLive : live }));
    await expect(page.getByTestId('layout-poll')).toBeVisible();
    await expect(page.getByTestId('slide-frame')).toHaveScreenshot(`poll-slide-${name}.png`);
  });
}

test('screenshot: poll slide with 8 options in midnight', async ({ mount, page }) => {
  await mount(
    pollSlide({
      control: busy(8),
      slots: { question: POLL_EIGHT.question, options: [...POLL_EIGHT.options] },
      join: live,
      palette: 'midnight',
    }),
  );
  await expect(page.getByTestId('slide-frame')).toHaveScreenshot('poll-slide-eight-midnight.png');
});
