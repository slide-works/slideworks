import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import fitFixturesJson from '@contracts/fit-fixtures.json' with { type: 'json' };
import { QaScreenFixture } from '../../qa/screen/QaScreenFixture';
import {
  QA_SLIDE_TEST_IDS as IDS,
  qaSlideRowTestId,
  qaSlideRowTextTestId,
  qaSlideRowVotesTestId,
} from '../../qa/testIds';
import type { QaControlView, QaQuestion } from '../../qa/types';
import { deckPlay } from '../../test-support/fixtures/screens';
import { PALETTES, lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { qaControlView } from '../../test-support/fixtures/qaStates';
import { JoinProvider } from '../JoinContext';
import { SlideRenderer } from '../SlideRenderer';
import type { SlideDeck, Slots } from '../slots';

test.use({ viewport: { width: 1920, height: 1080 } });

// The Q&A slide on its own, as the player draws it, with live data from the frozen fixture provider.
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

function qaSlide({
  control,
  slots = {},
  join = notLive,
  palette = 'cobalt',
}: {
  control: QaControlView | null;
  slots?: Slots['qa'];
  join?: typeof live;
  palette?: string;
}) {
  return (
    <JoinProvider deckId={deckPlay.id} join={join} owner={false}>
      <QaScreenFixture control={control}>
        <SlideRenderer
          slide={{ number: 6, layout: 'qa', part: null, deepDive: false, slots }}
          deck={deckIn(palette)}
          total={6}
          step={0}
        />
      </QaScreenFixture>
    </JoinProvider>
  );
}

const question = (number: number, overrides: Partial<QaQuestion> = {}): QaQuestion => ({
  number,
  text: `Question number ${number}?`,
  name: null,
  age: '1 min',
  state: 'open',
  hidden: false,
  onScreen: false,
  votes: 1,
  ...overrides,
});

// A live view with these questions; the counts follow them.
function viewOf(questions: QaQuestion[], overrides: Partial<QaControlView> = {}): QaControlView {
  const open = questions.filter((q) => q.state === 'open' && !q.hidden).length;
  return qaControlView('live', {
    questions,
    onScreen: questions.find((q) => q.onScreen)?.number ?? null,
    counts: { open, waiting: 0, answered: 0, hidden: 0 },
    ...overrides,
  });
}

// Seven open questions with votes 7 down to 1, numbered by when they were asked: the list shows the five most upvoted.
const seven = () => Array.from({ length: 7 }, (_, i) => question(i + 1, { votes: 7 - i }));

test('the list shows the five most upvoted of seven, with the footer saying so', async ({
  mount,
  page,
}) => {
  await mount(qaSlide({ control: viewOf(seven()), join: live }));
  await expect(page.getByTestId(IDS.rows).locator('li')).toHaveCount(5);
  for (const number of [1, 2, 3, 4, 5]) {
    await expect(page.getByTestId(qaSlideRowTextTestId(number))).toHaveText(
      `Question number ${number}?`,
    );
    await expect(page.getByTestId(qaSlideRowVotesTestId(number))).toHaveText(String(8 - number));
  }
  await expect(page.getByTestId(qaSlideRowTestId(6))).toHaveCount(0);
  await expect(page.getByTestId(IDS.phase)).toHaveText('Open');
  await expect(page.getByTestId(IDS.total)).toHaveText('7 questions');
  await expect(page.getByTestId(IDS.footerNote)).toHaveText('Top 5 of 7, by upvotes');
  await expect(page.getByTestId(IDS.join)).toBeVisible();
  await expect(page.getByTestId(IDS.kicker)).toHaveText('Q&A');
  await expect(page.getByTestId(IDS.title)).toHaveText('Questions from the room');
});

test('fewer than five questions list all of them and invite the room', async ({ mount, page }) => {
  await mount(qaSlide({ control: viewOf(seven().slice(0, 3)) }));
  await expect(page.getByTestId(IDS.rows).locator('li')).toHaveCount(3);
  await expect(page.getByTestId(IDS.total)).toHaveText('3 questions');
  await expect(page.getByTestId(IDS.footerNote)).toHaveText('Ask and upvote on your phone');
  await expect(page.getByTestId(IDS.join)).toHaveCount(0);
});

test('one question reads "1 question"', async ({ mount, page }) => {
  await mount(qaSlide({ control: viewOf([question(1)]) }));
  await expect(page.getByTestId(IDS.total)).toHaveText('1 question');
});

test('a long question is cut to one line with an ellipsis', async ({ mount, page }) => {
  const text = 'How do you split a big change into small ones '.repeat(8).trim();
  await mount(qaSlide({ control: viewOf([question(1, { text })]) }));
  const row = page.getByTestId(qaSlideRowTextTestId(1));
  await expect(row).toHaveText(text);
  const cut = await row.evaluate((el) => ({
    clipped: el.scrollWidth > el.clientWidth,
    height: el.getBoundingClientRect().height,
    overflow: getComputedStyle(el).textOverflow,
  }));
  expect(cut.clipped).toBe(true);
  expect(cut.overflow).toBe('ellipsis');
  expect(cut.height).toBeLessThan(60);
});

test('one question up shows whole, with its votes and name, in place of the list', async ({
  mount,
  page,
}) => {
  const text = 'How do you split a big change into small ones '.repeat(5).trim().slice(0, 200);
  const control = viewOf([
    question(1, { votes: 14, name: 'Priya', text, onScreen: true }),
    ...seven().slice(1, 5),
  ]);
  await mount(qaSlide({ control }));
  await expect(page.getByTestId(IDS.oneUp)).toBeVisible();
  await expect(page.getByTestId(IDS.rows)).toHaveCount(0);
  await expect(page.getByTestId(IDS.oneUpText)).toHaveText(text);
  await expect(page.getByTestId(IDS.oneUpVotes)).toHaveText('▲ 14');
  await expect(page.getByTestId(IDS.oneUpName)).toHaveText('Priya');
  await expect(page.getByTestId(IDS.oneUpMore)).toHaveCount(0);
  await expect(page.getByTestId(IDS.footerNote)).toHaveText('On screen now · 4 more waiting');
  // The longest question the server accepts (200 characters) is drawn whole: the text ends above the footer, inside
  // the content box.
  const gap = await page.evaluate(() => {
    const text = document
      .querySelector('[data-testid="qa-slide-one-up-text"]')!
      .getBoundingClientRect();
    const foot = document.querySelector('[data-testid="qa-slide-footer"]')!.getBoundingClientRect();
    const slide = document
      .querySelector('[data-testid="layout-qa"]')!
      .closest('[data-layout]')!
      .getBoundingClientRect();
    return {
      clear: foot.top - text.bottom,
      bottom: foot.bottom - slide.top,
      right: text.right - slide.left,
    };
  });
  expect(gap.clear).toBeGreaterThanOrEqual(0);
  expect(gap.bottom).toBeLessThanOrEqual(996.5);
  expect(gap.right).toBeLessThanOrEqual(1800.5);
});

test('an anonymous question on screen says "Anonymous"', async ({ mount, page }) => {
  await mount(qaSlide({ control: viewOf([question(1, { onScreen: true })]) }));
  await expect(page.getByTestId(IDS.oneUpName)).toHaveText('Anonymous');
  await expect(page.getByTestId(IDS.footerNote)).toHaveText('On screen now · 0 more waiting');
});

test('an onScreen number that is not in the list shows no question', async ({ mount, page }) => {
  await mount(qaSlide({ control: viewOf(seven().slice(0, 2), { onScreen: 99 }) }));
  await expect(page.getByTestId(IDS.oneUp)).toHaveCount(0);
  await expect(page.getByTestId(IDS.rows).locator('li')).toHaveCount(2);
});

test('no questions yet says what to do', async ({ mount, page }) => {
  await mount(qaSlide({ control: viewOf([]), join: live }));
  await expect(page.getByTestId(IDS.empty)).toContainText('No questions yet');
  await expect(page.getByTestId(IDS.empty)).toContainText(
    'Ask on your phone. The most upvoted questions show here.',
  );
  await expect(page.getByTestId(IDS.closed)).toHaveCount(0);
  await expect(page.getByTestId(IDS.total)).toHaveText('0 questions');
  await expect(page.getByTestId(IDS.footerNote)).toHaveText('Ask and upvote on your phone');
});

test('closed and empty says so, and the pill says Closed', async ({ mount, page }) => {
  const control = viewOf([], {
    settings: { ...qaControlView('live').settings, questions: 'closed' },
  });
  await mount(qaSlide({ control }));
  await expect(page.getByTestId(IDS.closed)).toContainText('Questions are closed');
  await expect(page.getByTestId(IDS.closed)).toContainText('Thanks for asking.');
  await expect(page.getByTestId(IDS.empty)).toHaveCount(0);
  await expect(page.getByTestId(IDS.phase)).toHaveText('Closed');
});

test('closed with questions keeps the list and the Closed pill', async ({ mount, page }) => {
  const control = viewOf(seven().slice(0, 2), {
    settings: { ...qaControlView('live').settings, questions: 'closed' },
  });
  await mount(qaSlide({ control }));
  await expect(page.getByTestId(IDS.rows).locator('li')).toHaveCount(2);
  await expect(page.getByTestId(IDS.phase)).toHaveText('Closed');
});

test('no live data draws the not-live state with a Not live pill and no join pill', async ({
  mount,
  page,
}) => {
  await mount(qaSlide({ control: null }));
  await expect(page.getByTestId(IDS.notLive)).toContainText('Questions from the room appear here');
  await expect(page.getByTestId(IDS.notLive)).toContainText(
    'While the deck is live, the audience asks and upvotes on their phones.',
  );
  await expect(page.getByTestId(IDS.notLivePill)).toHaveText('Not live');
  for (const id of [IDS.rows, IDS.phase, IDS.total, IDS.footerNote, IDS.join])
    await expect(page.getByTestId(id)).toHaveCount(0);
});

test('a title slot replaces the default title', async ({ mount, page }) => {
  await mount(qaSlide({ control: null, slots: { title: 'Ask me anything' } }));
  await expect(page.getByTestId(IDS.title)).toHaveText('Ask me anything');
});

// The shared max-content fixture (src/contracts/fit-fixtures.json): the longest title the server accepts. It must stay
// inside the content box with the full list of five rows and the join pill up, in every palette.
const QA_TITLE = fitFixturesJson.qaTitle.slots.title;

async function measure(page: Page) {
  await page.evaluate(() => document.fonts.ready);
  return page.getByTestId('layout-qa').evaluate((root: Element) => {
    const slide = root.closest('[data-layout]')!.getBoundingClientRect();
    const pill = root.querySelector('[data-testid="qa-slide-join"]');
    const drawn = { left: Infinity, top: Infinity, right: -Infinity, bottom: -Infinity };
    for (const element of root.querySelectorAll('*')) {
      if (pill?.contains(element)) continue;
      if (getComputedStyle(element).display === 'contents') continue;
      const box = element.getBoundingClientRect();
      if (box.width === 0 && box.height === 0) continue;
      drawn.left = Math.min(drawn.left, box.left - slide.left);
      drawn.top = Math.min(drawn.top, box.top - slide.top);
      drawn.right = Math.max(drawn.right, box.right - slide.left);
      drawn.bottom = Math.max(drawn.bottom, box.bottom - slide.top);
    }
    return drawn;
  });
}

for (const palette of PALETTES.map((p) => p.name)) {
  test(`the qaTitle fit fixture with five rows stays inside x 120-1800, y 150-996 in ${palette}`, async ({
    mount,
    page,
  }) => {
    await mount(
      qaSlide({ control: viewOf(seven()), slots: { title: QA_TITLE }, join: live, palette }),
    );
    await expect(page.getByTestId(IDS.title)).toHaveText(QA_TITLE);
    const drawn = await measure(page);
    expect(drawn.left).toBeGreaterThanOrEqual(119.5);
    expect(drawn.right).toBeLessThanOrEqual(1800.5);
    expect(drawn.top).toBeGreaterThanOrEqual(149.5);
    expect(drawn.bottom).toBeLessThanOrEqual(996.5);
    await expect(page.getByTestId(qaSlideRowTestId(5))).toBeVisible();
  });
}

test('the qaTitle fit fixture is at most two lines', async ({ mount, page }) => {
  await mount(qaSlide({ control: null, slots: { title: QA_TITLE } }));
  const lines = await page.getByTestId(IDS.title).evaluate((el) => {
    const style = getComputedStyle(el);
    return Math.round(el.getBoundingClientRect().height / parseFloat(style.lineHeight));
  });
  expect(lines).toBeLessThanOrEqual(2);
});

const SCREENSHOTS: [string, QaControlView | null][] = [
  ['not-live', null],
  ['list', viewOf(seven())],
  [
    'one-up',
    viewOf([question(1, { votes: 14, name: 'Priya', onScreen: true }), ...seven().slice(1)]),
  ],
  ['empty', viewOf([])],
  ['closed', viewOf([], { settings: { ...qaControlView('live').settings, questions: 'closed' } })],
];

for (const [name, control] of SCREENSHOTS) {
  test(`screenshot: qa slide ${name}`, async ({ mount, page }) => {
    await mount(qaSlide({ control, join: control === null ? notLive : live }));
    await expect(page.getByTestId('layout-qa')).toBeVisible();
    await expect(page.getByTestId('slide-frame')).toHaveScreenshot(`qa-slide-${name}.png`);
  });
}
