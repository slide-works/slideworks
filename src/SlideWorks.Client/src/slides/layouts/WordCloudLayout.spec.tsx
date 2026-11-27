import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { DeckPlaySlide } from '../../api/types';
import { OpenResponseScreenLive } from '../../openResponses/OpenResponseScreenLive';
import type { OpenResponseControlView, WordCloudEvent } from '../../openResponses/types';
import { guestView } from '../../test-support/fixtures/guestStates';
import { LAYOUT_SLOTS } from '../../test-support/fixtures/layoutSlots';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { deckPlay } from '../../test-support/fixtures/screens';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import { JoinProvider } from '../JoinContext';
import { SlideRenderer } from '../SlideRenderer';
import type { SlideDeck } from '../slots';
import type { WordCloudSlots } from '../slots/word-cloud';
import { settle } from '../../test-support/gate';

test.use({ viewport: { width: 1920, height: 1080 } });

// The layout under the real screen provider, over the stub hub and a stubbed GET of the slide's control view. Slide 2 of
// the fixture deck is the word cloud.
const SLIDE = 2;
const deckId = deckPlay.id;
const liveJoin = { ...deckPlay.join, live: true };
const QUESTION = LAYOUT_SLOTS['word-cloud'].question;
const cloudSlide: DeckPlaySlide = {
  ...deckPlay.slides[SLIDE - 1],
  layout: 'word-cloud',
  steps: 0,
  slots: structuredClone(LAYOUT_SLOTS['word-cloud']),
} as DeckPlaySlide;
const deck: SlideDeck = {
  title: deckPlay.title,
  author: deckPlay.author,
  parts: [],
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

const ROUND = '6e2f9a14-3c7b-4d58-a1e0-9b4c7d2f5a63';

function control(
  revision: number,
  entries: OpenResponseControlView['entries'],
): OpenResponseControlView {
  return {
    kind: 'word-cloud',
    phase: 'open',
    round: ROUND,
    number: 1,
    revision,
    results: 'live',
    opens: 'arrival',
    revealed: true,
    guests: entries.filter((entry) => !entry.hidden).length,
    entries,
  };
}

// The deck is live and the slide's control view is `view`. GET /live confirms the viewer as someone who can present, or
// with canPresent false answers 403 as it does for anyone else; liveAnswers counts the answers it has sent. Each GET of
// a slide's view is recorded by its slide number.
async function stubLive(page: Page, view: OpenResponseControlView, { canPresent = true } = {}) {
  const hub = await stubDeckHub(page, { audience: { state: guestView('live').state } });
  let liveAnswers = 0;
  await page.route('**/api/decks/*/live', async (route) => {
    if (!canPresent) {
      await route.fulfill({ status: 403, json: { error: 'forbidden' } });
      liveAnswers += 1;
      return;
    }
    await route.fulfill({
      json: {
        sessionId: 'session-1',
        code: liveJoin.code,
        codeLabel: liveJoin.codeLabel,
        joinUrl: liveJoin.joinUrl,
        hostLabel: liveJoin.hostLabel,
        live: true,
        joinedCount: 3,
        joinBar: false,
      },
    });
    liveAnswers += 1;
  });
  const reads: number[] = [];
  await page.route('**/api/decks/*/slides/*/response', (route) => {
    reads.push(Number(new URL(route.request().url()).pathname.split('/')[5]));
    return route.fulfill({ json: view });
  });
  return {
    hub,
    reads,
    get liveAnswers() {
      return liveAnswers;
    },
  };
}

// The player as a viewer the page takes for the owner; GET /live decides whether they can present.
function player(slots: Partial<WordCloudSlots> = {}) {
  const slide = { ...cloudSlide, slots: { ...cloudSlide.slots, ...slots } } as DeckPlaySlide;
  return (
    <JoinProvider deckId={deckId} join={liveJoin} owner>
      <OpenResponseScreenLive deckId={deckId} owner>
        <SlideRenderer slide={slide} deck={deck} total={6} step={0} />
      </OpenResponseScreenLive>
    </JoinProvider>
  );
}

// Thirty words, the longest the phone allows among them, more than the body holds at their starting sizes.
const CROWD: OpenResponseControlView['entries'] = Array.from({ length: 30 }, (_, index) => ({
  number: index + 1,
  text:
    index % 5 === 0 ? `Continuous integration ${String(index).padStart(2, '0')}` : `Word${index}`,
  count: 30 - index,
  hidden: false,
}));

const SEVEN: OpenResponseControlView['entries'] = [
  'Calm',
  'Busy',
  'Focused',
  'Tired',
  'Proud',
  'Rushed',
  'Steady',
].map((text, index) => ({ number: index + 1, text, count: 7 - index, hidden: false }));

// Each drawn word's box on the slide, in slide px from its top-left corner, with its transform.
const drawn = (page: Page) =>
  slide(page).evaluate((element) => {
    const frame = element.getBoundingClientRect();
    return [...element.querySelectorAll('[data-testid^="word-cloud-word-"]')].map((word) => {
      const box = word.getBoundingClientRect();
      return {
        text: word.textContent,
        left: box.left - frame.left,
        top: box.top - frame.top,
        right: box.right - frame.left,
        bottom: box.bottom - frame.top,
        transform: word.getAttribute('transform') ?? '',
        fill: getComputedStyle(word).fill,
        size: parseFloat(getComputedStyle(word).fontSize),
      };
    });
  });

async function tokenColour(page: Page, token: string) {
  return slide(page).evaluate((element, name) => {
    const probe = document.createElement('span');
    probe.style.color = `var(${name})`;
    element.append(probe);
    const color = getComputedStyle(probe).color;
    probe.remove();
    return color;
  }, token);
}

const slide = (page: Page) => page.getByTestId(`slide-${SLIDE}`);
const word = (page: Page, index: number) => slide(page).getByTestId(`word-cloud-word-${index}`);
// Lets the page run its pending work, so a call or render it should not make has had its chance.
const fontPx = (page: Page, index: number) =>
  word(page, index).evaluate((element) => parseFloat(getComputedStyle(element).fontSize));

test('WordCloudLayout_WhenOneWordHasAHigherCount_ShouldRenderItLarger', async ({ mount, page }) => {
  await stubLive(
    page,
    control(5, [
      { number: 1, text: 'Calm', count: 2, hidden: false },
      { number: 2, text: 'Busy', count: 1, hidden: false },
    ]),
  );
  await mount(player());

  await expect(word(page, 0)).toHaveText('Calm');
  await expect(word(page, 1)).toHaveText('Busy');
  expect(await fontPx(page, 0)).toBeGreaterThan(await fontPx(page, 1));
});

test('WordCloudLayout_WhenAWordIsHidden_ShouldLeaveItOutAndKeepTheQuestion', async ({
  mount,
  page,
}) => {
  const { hub } = await stubLive(
    page,
    control(5, [{ number: 1, text: 'Calm', count: 1, hidden: false }]),
  );
  await mount(player());
  await expect(word(page, 0)).toHaveText('Calm');

  const hidden: WordCloudEvent = {
    type: 'wordCloud',
    slide: SLIDE,
    phase: 'open',
    round: ROUND,
    number: 1,
    revision: 6,
    revealed: true,
    guests: 2,
    words: [
      { text: 'Rubbish', count: 3, hidden: true },
      { text: 'Busy', count: 2, hidden: false },
      { text: 'Calm', count: 1, hidden: false },
    ],
  };
  hub.pushWordCloud(hidden, { target: 'screen' });

  await expect(word(page, 0)).toHaveText('Busy');
  await expect(word(page, 1)).toHaveText('Calm');
  await expect(word(page, 2)).toHaveCount(0);
  await expect(slide(page).getByText('Rubbish')).toHaveCount(0);
  await expect(slide(page).getByText(QUESTION)).toBeVisible();
});

test('Cloud_WhenTheViewerCannotPresent_ShouldShowTheQuestionOnly', async ({ mount, page }) => {
  const live = await stubLive(
    page,
    control(5, [{ number: 1, text: 'Calm', count: 4, hidden: false }]),
    {
      canPresent: false,
    },
  );
  await mount(player());
  await expect.poll(() => live.liveAnswers).toBe(1);
  await expect.poll(() => live.hub.sockets).toBeGreaterThanOrEqual(1);
  await settle(page, 300);

  expect(live.hub.watches).toEqual([]);
  expect(live.reads).toEqual([]);
  await expect(slide(page).getByText(QUESTION)).toBeVisible();
  await expect(slide(page).getByRole('list')).toHaveCount(0);
  await expect(word(page, 0)).toHaveCount(0);
});

test('WordCloudLayout_WhenMoreWordsArriveThanFit_ShouldDrawEveryWordInsideTheBody', async ({
  mount,
  page,
}) => {
  await stubLive(page, control(5, CROWD));
  await mount(player({ turn: 'some' }));
  await expect(word(page, 0)).toHaveText('Continuous integration 00');

  // A text's box is its font's line box, a little taller than its letters, which are what the cloud packs inside the
  // body. So the boxes are held to the slide's fit box and kept off the question.
  const heading = await slide(page)
    .getByText(QUESTION)
    .evaluate((element) => {
      const frame = element.closest('[data-testid^="slide-"]')!.getBoundingClientRect();
      return element.getBoundingClientRect().bottom - frame.top;
    });
  const words = await drawn(page);
  expect(words.length).toBeGreaterThan(5);
  for (const placed of words) {
    expect(placed.left, placed.text ?? '').toBeGreaterThanOrEqual(120);
    expect(placed.right, placed.text ?? '').toBeLessThanOrEqual(1800);
    expect(placed.top, placed.text ?? '').toBeGreaterThanOrEqual(heading);
    expect(placed.bottom, placed.text ?? '').toBeLessThanOrEqual(996);
  }
  const sizes = words.map((placed) => placed.size);
  expect(sizes).toEqual([...sizes].sort((a, b) => b - a));
});

test('WordCloudLayout_WhenWordsAreShown_ShouldInkThemFromThePalette', async ({ mount, page }) => {
  await stubLive(page, control(5, SEVEN));
  await mount(player());
  await expect(word(page, 6)).toHaveText('Steady');

  const inks = await Promise.all(
    ['--sw-accent', '--sw-ink', '--sw-dd', '--sw-accent-dark', '--sw-panel-positive'].map((token) =>
      tokenColour(page, token),
    ),
  );
  const fills = (await drawn(page)).map((placed) => placed.fill);
  for (const fill of fills) expect(inks).toContain(fill);
  expect(new Set(fills).size).toBeGreaterThan(1);
});

test('WordCloudLayout_WhenTurnIsSome_ShouldTurnSomeWordsOnTheirSide', async ({ mount, page }) => {
  await stubLive(page, control(5, SEVEN));
  await mount(player({ turn: 'some' }));
  await expect(word(page, 6)).toHaveText('Steady');

  const transforms = (await drawn(page)).map((placed) => placed.transform);
  expect(
    transforms.filter((transform) => transform.includes('rotate(-90)')).length,
  ).toBeGreaterThan(0);
  expect(transforms.filter((transform) => transform.includes('rotate(0)')).length).toBeGreaterThan(
    0,
  );
});

test('WordCloudLayout_WhenTurnIsLeftOut_ShouldKeepEveryWordLevel', async ({ mount, page }) => {
  await stubLive(page, control(5, SEVEN));
  await mount(player({ turn: undefined }));
  await expect(word(page, 6)).toHaveText('Steady');

  for (const placed of await drawn(page)) expect(placed.transform).toContain('rotate(0)');
});

test('WordCloudLayout_WhenANewWordArrives_ShouldKeepThePlacedWordsWhereTheyWere', async ({
  mount,
  page,
}) => {
  const { hub } = await stubLive(page, control(5, SEVEN));
  await mount(player({ turn: 'some' }));
  await expect(word(page, 6)).toHaveText('Steady');
  const before = await drawn(page);

  const joined: WordCloudEvent = {
    type: 'wordCloud',
    slide: SLIDE,
    phase: 'open',
    round: ROUND,
    number: 1,
    revision: 6,
    revealed: true,
    guests: 8,
    words: [
      ...SEVEN.map(({ text, count }) => ({ text, count: count ?? 0, hidden: false })),
      { text: 'Hopeful', count: 1, hidden: false },
    ],
  };
  hub.pushWordCloud(joined, { target: 'screen' });
  await expect(word(page, 7)).toHaveText('Hopeful');

  const after = await drawn(page);
  expect(after.slice(0, 7).map(({ text, transform }) => ({ text, transform }))).toEqual(
    before.map(({ text, transform }) => ({ text, transform })),
  );
});

test('WordCloudLayout_WhenTheScreenMountsAgain_ShouldDrawTheSameCloud', async ({ mount, page }) => {
  await stubLive(page, control(5, SEVEN));
  const first = await mount(player({ turn: 'some' }));
  await expect(word(page, 6)).toHaveText('Steady');
  const before = await drawn(page);
  await first.unmount();

  await mount(player({ turn: 'some' }));
  await expect(word(page, 6)).toHaveText('Steady');

  expect(await drawn(page)).toEqual(before);
});

test('WordCloudLayout_WhenManyWordsAreShownScreenshot_ShouldMatchTheBaseline', async ({
  mount,
  page,
}) => {
  await stubLive(page, control(5, CROWD));
  await mount(player({ turn: 'some' }));
  await expect(word(page, 0)).toHaveText('Continuous integration 00');

  await expect(slide(page)).toHaveScreenshot('word-cloud-many-words.png');
});

test('WordCloudLayout_WhenOneWordIsShownScreenshot_ShouldMatchTheBaseline', async ({
  mount,
  page,
}) => {
  await stubLive(page, control(5, [{ number: 1, text: 'Focused', count: 3, hidden: false }]));
  await mount(player());
  await expect(word(page, 0)).toHaveText('Focused');

  await expect(slide(page)).toHaveScreenshot('word-cloud-one-word.png');
});

test('WordCloudLayout_WhenWordsArePlaced_ShouldMarkItsBodyPacked', async ({ mount, page }) => {
  await stubLive(page, control(5, SEVEN));
  await mount(player());

  await expect(word(page, 0)).toBeVisible();
  await expect(slide(page).getByTestId('word-cloud-body')).toHaveAttribute('data-packed', 'true');
});

test('WordCloudLayout_WhenThereAreNoWords_ShouldMarkItsBodyPackedAtOnce', async ({
  mount,
  page,
}) => {
  await stubLive(page, control(5, []));
  await mount(player());

  await expect(slide(page).getByTestId('word-cloud-body')).toHaveAttribute('data-packed', 'true');
  await expect(word(page, 0)).toHaveCount(0);
});
