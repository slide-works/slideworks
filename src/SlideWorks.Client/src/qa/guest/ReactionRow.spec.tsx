import { test, expect } from '@playwright/experimental-ct-react';
import { REACTION_EMOJI } from '../emoji';
import { QA_REACTION_ROW_TEST_IDS, reactionButtonTestId } from '../testIds';
import type { ReactionEmoji } from '../types';
import { PhoneReactionsFixture, type PhoneReactionsFixtureProps } from './PhoneReactionsFixture';
import { ReactionRow } from './ReactionRow';

// The reaction row under the activity card (Q7, Q8, D6; mockup phone state 1) over PhoneReactionsFixture on a 360 px
// phone.
test.beforeEach(async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 740 });
});

// The row as the Slide panel lays it out, on the guest page's dark background.
const row = (props: Omit<PhoneReactionsFixtureProps, 'children'> = {}) => (
  <PhoneReactionsFixture {...props}>
    <div
      style={{
        padding: '48px 16px 12px',
        background: 'var(--sw-night)',
        color: 'white',
      }}
    >
      <ReactionRow presenter="Sam Rivera" />
    </div>
  </PhoneReactionsFixture>
);

test('five buttons in a group named Reactions, each named for its emoji, 44 px tall at least', async ({
  mount,
  page,
}) => {
  await mount(row());
  const group = page.getByRole('group', { name: 'Reactions' });

  await expect(group).toHaveAttribute('data-testid', QA_REACTION_ROW_TEST_IDS.row);
  await expect(group.getByRole('button')).toHaveCount(5);
  for (const [emoji, name] of [
    ['thumbsUp', 'React thumbs up'],
    ['heart', 'React heart'],
    ['laugh', 'React laugh'],
    ['wow', 'React wow'],
    ['clap', 'React clap'],
  ] as const) {
    const button = page.getByTestId(reactionButtonTestId(emoji));
    await expect(button).toHaveAccessibleName(name);
    expect((await button.boundingBox())!.height).toBeGreaterThanOrEqual(44);
  }
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
});

test('a tap calls tap once with its emoji', async ({ mount, page }) => {
  const taps: ReactionEmoji[] = [];
  await mount(row({ onTap: (emoji) => taps.push(emoji) }));

  await page.getByTestId(reactionButtonTestId('clap')).click();
  await expect.poll(() => taps).toEqual(['clap']);

  await page.getByTestId(reactionButtonTestId('heart')).click();
  await expect.poll(() => taps).toEqual(['clap', 'heart']);
});

test('a tap pops its emoji', async ({ mount, page }) => {
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await mount(row());

  await page.getByTestId(reactionButtonTestId('wow')).click();

  const pop = page.getByTestId(QA_REACTION_ROW_TEST_IDS.pop);
  await expect(pop).toHaveCount(1);
  await expect(pop).toBeVisible();
  await expect(pop).toHaveText('😮');
});

test('reduced motion shows no pop', async ({ mount, page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  const taps: ReactionEmoji[] = [];
  await mount(row({ onTap: (emoji) => taps.push(emoji) }));

  await page.getByTestId(reactionButtonTestId('wow')).click();
  await expect.poll(() => taps).toEqual(['wow']);

  // The pop stays in the button after its animation, so hidden here means not displayed, not finished.
  const pop = page.getByTestId(QA_REACTION_ROW_TEST_IDS.pop);
  await expect(pop).toHaveCount(1);
  await expect(pop).toBeHidden();
  await expect(page.getByTestId(reactionButtonTestId('wow'))).toHaveCSS(
    'transition-duration',
    '0s',
  );
});

test('cooling greys the row and no tap goes out', async ({ mount, page }) => {
  const taps: ReactionEmoji[] = [];
  await mount(row({ cooling: true, onTap: (emoji) => taps.push(emoji) }));

  await expect(page.getByTestId(QA_REACTION_ROW_TEST_IDS.row)).toHaveCSS('opacity', '0.4');
  for (const emoji of REACTION_EMOJI)
    await expect(page.getByTestId(reactionButtonTestId(emoji))).toBeDisabled();
  await page.getByTestId(reactionButtonTestId('clap')).click({ force: true });
  expect(taps).toEqual([]);
});

test('not cooling, the row is not greyed', async ({ mount, page }) => {
  await mount(row());

  await expect(page.getByTestId(QA_REACTION_ROW_TEST_IDS.row)).toHaveCSS('opacity', '1');
  await expect(page.getByTestId(reactionButtonTestId('clap'))).toBeEnabled();
});

test('absent when reactions are not enabled', async ({ mount, page }) => {
  await mount(row({ enabled: false }));

  await expect(page.getByTestId(QA_REACTION_ROW_TEST_IDS.row)).toHaveCount(0);
  await expect(page.getByRole('button')).toHaveCount(0);
});

for (const [error, text] of [
  ['too_many_reactions', 'That’s a lot of reactions. Wait a moment, then react again.'],
  ['reactions_off', 'Reactions are off right now.'],
  ['no_session', 'This talk isn’t live any more.'],
  ['Request failed (400)', 'Something went wrong on our side. Try again in a moment.'],
  [
    'Your phone couldn’t reach the talk. Check your connection, then try again.',
    'Your phone couldn’t reach the talk. Check your connection, then try again.',
  ],
] as const) {
  test(`a failed tap shows the hook's message as guest copy: ${error}`, async ({ mount, page }) => {
    await mount(row({ error }));

    const message = page.getByTestId(QA_REACTION_ROW_TEST_IDS.error);
    await expect(message).toHaveText(text);
    await expect(message).toHaveAttribute('role', 'alert');
  });
}

test('no message when nothing has failed', async ({ mount, page }) => {
  await mount(row());

  await expect(page.getByRole('group', { name: 'Reactions' })).toBeVisible();
  await expect(page.getByTestId(QA_REACTION_ROW_TEST_IDS.error)).toHaveCount(0);
});

test('screenshot the row', async ({ mount, page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await mount(row());
  await expect(page).toHaveScreenshot('qa-reaction-row.png');
});

test('screenshot the row cooling with a message', async ({ mount, page }) => {
  await mount(row({ cooling: true, error: 'too_many_reactions' }));
  await expect(page).toHaveScreenshot('qa-reaction-row-cooling.png');
});
