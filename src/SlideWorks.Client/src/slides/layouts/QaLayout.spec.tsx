import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import fitFixturesJson from '@contracts/fit-fixtures.json' with { type: 'json' };
import { QaScreenProvider } from '../../qa/screen/QaScreenContext';
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

test('a question on screen that is no longer open never counts below zero waiting', async ({
  mount,
  page,
}) => {
  const control = viewOf([question(1, { state: 'answered', onScreen: true })]);
  await mount(qaSlide({ control }));
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
  await expect(page.getByTestId(IDS.footerNote)).toHaveCount(0);
});

test('closed with questions keeps the list and the Closed pill', async ({ mount, page }) => {
  const control = viewOf(seven().slice(0, 2), {
    settings: { ...qaControlView('live').settings, questions: 'closed' },
  });
  await mount(qaSlide({ control }));
  await expect(page.getByTestId(IDS.rows).locator('li')).toHaveCount(2);
  await expect(page.getByTestId(IDS.phase)).toHaveText('Closed');
  // Nobody can ask, so no invite (D-B1b).
  await expect(page.getByTestId(IDS.footerNote)).toHaveCount(0);
  await expect(page.getByText('Ask and upvote on your phone')).toHaveCount(0);
});

test('a closed list of six or more still says "Top 5 of n"', async ({ mount, page }) => {
  const control = viewOf(seven().slice(0, 6), {
    settings: { ...qaControlView('live').settings, questions: 'closed' },
  });
  await mount(qaSlide({ control }));
  await expect(page.getByTestId(IDS.footerNote)).toHaveText('Top 5 of 6, by upvotes');
});

test('exactly five open questions invite the room, with no "Top 5 of"', async ({ mount, page }) => {
  await mount(qaSlide({ control: viewOf(seven().slice(0, 5)) }));
  await expect(page.getByTestId(IDS.rows).locator('li')).toHaveCount(5);
  await expect(page.getByTestId(IDS.footerNote)).toHaveText('Ask and upvote on your phone');
});

test('questions off while the deck is live says "Questions are off", never "Not live"', async ({
  mount,
  page,
}) => {
  await mount(qaSlide({ control: viewOf([], { round: null }), join: live }));
  await expect(page.getByTestId(IDS.title)).toHaveText('Questions from the room');
  await expect(page.getByTestId(IDS.off)).toContainText('Questions are off');
  await expect(page.getByTestId(IDS.notLive)).toHaveCount(0);
  await expect(page.getByTestId(IDS.notLivePill)).toHaveCount(0);
  await expect(page.getByText('Not live')).toHaveCount(0);
  await expect(page.getByTestId(IDS.rows)).toHaveCount(0);
});

test('a live join with no provider data is "Not live", never "Questions are off"', async ({
  mount,
  page,
}) => {
  await mount(qaSlide({ control: null, join: live }));
  await expect(page.getByTestId(IDS.notLive)).toBeVisible();
  await expect(page.getByTestId(IDS.notLivePill)).toHaveText('Not live');
  await expect(page.getByTestId(IDS.off)).toHaveCount(0);
});

test('a pending screen draws the title and a blank body, with no copy and no pill', async ({
  mount,
  page,
}) => {
  await mount(
    <JoinProvider deckId={deckPlay.id} join={live} owner={false}>
      <QaScreenProvider state={{ kind: 'pending' }}>
        <SlideRenderer
          slide={{ number: 6, layout: 'qa', part: null, deepDive: false, slots: {} }}
          deck={deckIn()}
          total={6}
          step={0}
        />
      </QaScreenProvider>
    </JoinProvider>,
  );
  await expect(page.getByTestId(IDS.title)).toHaveText('Questions from the room');
  await expect(page.getByTestId(IDS.off)).toHaveCount(0);
  await expect(page.getByTestId(IDS.notLive)).toHaveCount(0);
  await expect(page.getByTestId(IDS.notLivePill)).toHaveCount(0);
  await expect(page.getByTestId(IDS.rows)).toHaveCount(0);
  await expect(page.getByText('Not live')).toHaveCount(0);
  await expect(page.getByText('Questions are off')).toHaveCount(0);
  await expect(page.getByTestId('layout-qa').locator('.sw-body')).toBeEmpty();
});

test('the phase pill text uses the readable good ink', async ({ mount, page }) => {
  await mount(qaSlide({ control: viewOf([]), join: live }));
  const [pill, ink] = await page.getByTestId(IDS.phase).evaluate((el) => {
    const probe = document.createElement('i');
    probe.style.color = 'var(--sw-good-ink)';
    el.appendChild(probe);
    const wanted = getComputedStyle(probe).color;
    probe.remove();
    return [getComputedStyle(el).color, wanted];
  });
  expect(pill).toBe(ink);
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

// The one-up question at its longest (200 characters, what the server accepts), in the shapes that break a slide:
// the text must sit below the title, above the footer, inside y 150-960 and x 120-1800.
const LONG_NAME = 'Alexandria Montgomery-Featherstonehaugh'.slice(0, 30);
const ONE_UP_CASES: [string, string][] = [
  ['200 W', 'W'.repeat(200)],
  ['200 m', 'm'.repeat(200)],
  ['200 a', 'a'.repeat(200)],
  ['spaced text', 'How do you split a big change into small ones '.repeat(5).trim().slice(0, 200)],
  ['a URL', `https://example.com/${'a-long-path/'.repeat(17)}`.slice(0, 200)],
  ['Arabic', 'كيف نقسم تغييرا كبيرا إلى تغييرات صغيرة '.repeat(6).slice(0, 200)],
  ['emoji', '🚀'.repeat(100)],
  ['a short question', 'Why now?'],
];

for (const [title, slots] of [
  ['the qaTitle fixture title', { title: QA_TITLE }],
  ['the default title', {}],
] as const) {
  for (const [name, text] of ONE_UP_CASES) {
    test(`one-up: ${name} with ${title} fits between the title and the footer`, async ({
      mount,
      page,
    }) => {
      const control = viewOf([question(1, { text, name: LONG_NAME, votes: 1234, onScreen: true })]);
      await mount(qaSlide({ control, slots, join: live }));
      await page.evaluate(() => document.fonts.ready);
      const box = await page.getByTestId('layout-qa').evaluate((root: Element) => {
        const frame = root.closest('[data-layout]')!.getBoundingClientRect();
        const of = (id: string) => {
          const r = root.querySelector(`[data-testid="${id}"]`)!.getBoundingClientRect();
          return {
            top: r.top - frame.top,
            bottom: r.bottom - frame.top,
            right: r.right - frame.left,
          };
        };
        const text = of('qa-slide-one-up-text');
        const label = of('qa-slide-one-up');
        const textEl = root.querySelector('[data-testid="qa-slide-one-up-text"]')!;
        return {
          title: of('qa-slide-title'),
          footer: of('qa-slide-footer'),
          text,
          label,
          overflowX: textEl.scrollWidth - textEl.clientWidth,
        };
      });
      expect(box.text.top).toBeGreaterThanOrEqual(box.title.bottom);
      expect(box.label.top).toBeGreaterThanOrEqual(box.title.bottom);
      expect(box.text.bottom).toBeLessThanOrEqual(box.footer.top);
      expect(box.footer.bottom).toBeLessThanOrEqual(960.5);
      expect(box.text.top).toBeGreaterThanOrEqual(149.5);
      expect(box.text.right).toBeLessThanOrEqual(1800.5);
      expect(box.overflowX).toBeLessThanOrEqual(0);
      const drawn = await measure(page);
      expect(drawn.bottom).toBeLessThanOrEqual(960.5);
      expect(drawn.top).toBeGreaterThanOrEqual(149.5);
    });
  }
}

for (const [label, text, check] of [
  ['a normal one-up question is the mockup 80px', 'How do we ship smaller changes?', 'is80'],
  ['a 200-character one-up question is smaller than 80px', 'W'.repeat(200), 'smaller'],
] as const) {
  test(label, async ({ mount, page }) => {
    await mount(qaSlide({ control: viewOf([question(1, { text, onScreen: true })]), join: live }));
    const size = await page
      .getByTestId(IDS.oneUpText)
      .evaluate((el) => parseFloat(getComputedStyle(el).fontSize));
    if (check === 'is80') expect(size).toBe(80);
    else expect(size).toBeLessThan(80);
  });
}

// The size steps at their boundaries (D-B1a): 80px to 60 characters, 64 to 100, 52 to 140, then 40.
for (const [length, px] of [
  [60, 80],
  [61, 64],
  [100, 64],
  [101, 52],
  [140, 52],
  [141, 40],
] as const) {
  test(`a ${length}-character one-up question is ${px}px`, async ({ mount, page }) => {
    await mount(
      qaSlide({
        control: viewOf([question(1, { text: 'a'.repeat(length), onScreen: true })]),
        join: live,
      }),
    );
    const size = await page
      .getByTestId(IDS.oneUpText)
      .evaluate((el) => parseFloat(getComputedStyle(el).fontSize));
    expect(size).toBe(px);
  });
}

test('a long unbroken word wraps inside the slide (overflow-wrap anywhere)', async ({
  mount,
  page,
}) => {
  await mount(
    qaSlide({
      control: viewOf([question(1, { text: 'x'.repeat(200), onScreen: true })]),
      join: live,
    }),
  );
  const el = page.getByTestId(IDS.oneUpText);
  expect(await el.evaluate((e) => getComputedStyle(e).overflowWrap)).toBe('anywhere');
  expect(await el.evaluate((e) => e.scrollWidth <= e.clientWidth)).toBe(true);
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
