import { test, expect } from '@playwright/experimental-ct-react';
import type { Locator } from 'playwright/test';
import type { CalloutBlock } from '../slots';
import { calloutStep } from '../steps';
import { BodyCallout } from './BodyCallout';

const LAST: CalloutBlock = {
  tone: 'accent',
  text: 'Start with one team this month.',
  reveal: 'last',
};
const WITH_SLIDE: CalloutBlock = { ...LAST, reveal: 'with-slide' };

// A body as a layout builds it: a flex column 800 px wide with its content, a
// 200 px block, above the callout. Called as a function, never mounted as a
// component: a component test can only mount what it imports.
function body(callout: CalloutBlock, step: number, at: number) {
  return (
    <div
      data-testid="body"
      style={{ display: 'flex', flexDirection: 'column', width: 800, height: 600 }}
    >
      <div data-testid="content" style={{ height: 200 }} />
      <BodyCallout callout={callout} step={step} at={at} />
    </div>
  );
}

async function box(locator: Locator) {
  const bounds = await locator.boundingBox();
  expect(bounds).not.toBeNull();
  return bounds!;
}

test('shows on its step', async ({ mount, page }) => {
  const at = calloutStep(3, LAST);
  expect(at).toBe(4);
  const component = await mount(body(LAST, 3, at));

  const callout = page.getByTestId('block-callout');
  const reveal = page.getByTestId('body-callout').locator('.sw-reveal');
  // Before its step it is hidden but keeps its space.
  await expect(callout).toBeAttached();
  await expect(callout).toBeHidden();
  await expect(reveal).toHaveAttribute('data-revealed', 'false');
  await expect(reveal).toHaveAttribute('aria-hidden', 'true');
  await expect(reveal).toHaveCSS('visibility', 'hidden');
  await expect(reveal).toHaveCSS('display', 'block');
  const hidden = await box(page.getByTestId('body-callout'));
  expect(hidden.height).toBeGreaterThan(50);

  await component.update(body(LAST, 4, at));
  await expect(callout).toBeVisible();
  await expect(callout).toHaveText('Start with one team this month.');
  await expect(reveal).toHaveAttribute('data-revealed', 'true');
  await expect(reveal).not.toHaveAttribute('aria-hidden');
  // Nothing moved when it appeared.
  expect(await box(page.getByTestId('body-callout'))).toEqual(hidden);

  // A later step keeps it; going back hides it again.
  await component.update(body(LAST, 5, at));
  await expect(callout).toBeVisible();
  await component.update(body(LAST, 0, at));
  await expect(callout).toBeHidden();
  expect(await box(page.getByTestId('body-callout'))).toEqual(hidden);
});

test('with-slide shows from the start', async ({ mount, page }) => {
  const at = calloutStep(3, WITH_SLIDE);
  expect(at).toBe(0);
  await mount(body(WITH_SLIDE, 0, at));

  const callout = page.getByTestId('block-callout');
  await expect(callout).toBeVisible();
  await expect(callout).toHaveText('Start with one team this month.');
  await expect(callout).toHaveAttribute('data-tone', 'accent');
  // No step of its own, so no Reveal around it.
  await expect(page.getByTestId('body-callout').locator('.sw-reveal')).toHaveCount(0);
});

test('a callout that does not say when it shows is there from the start', async ({
  mount,
  page,
}) => {
  const callout: CalloutBlock = { tone: 'good', fill: 'solid', text: 'Keep going.' };
  await mount(body(callout, 0, calloutStep(3, callout)));

  await expect(page.getByTestId('block-callout')).toBeVisible();
  await expect(page.getByTestId('block-callout')).toHaveAttribute('data-fill', 'solid');
});

for (const [name, callout] of [
  ['shown from the start', WITH_SLIDE],
  ['hidden before its step', LAST],
] as const) {
  test(`sits 30 px under the content at its parent's full width, ${name}`, async ({
    mount,
    page,
  }) => {
    await mount(body(callout, 0, calloutStep(3, callout)));

    const parent = await box(page.getByTestId('body'));
    const content = await box(page.getByTestId('content'));
    const wrapper = await box(page.getByTestId('body-callout'));
    const block = await box(page.getByTestId('block-callout'));
    expect(wrapper.y).toBe(content.y + content.height + 30);
    expect(wrapper.x).toBe(parent.x);
    expect(wrapper.width).toBe(800);
    expect(block).toEqual(wrapper);
  });
}

test('keeps its height when the content above would squeeze it', async ({ mount, page }) => {
  await mount(
    <div style={{ display: 'flex', flexDirection: 'column', width: 800, height: 300 }}>
      <div data-testid="content" style={{ flex: 1, minHeight: 0 }} />
      <BodyCallout callout={WITH_SLIDE} step={0} at={0} />
    </div>,
  );

  const wrapper = await box(page.getByTestId('body-callout'));
  const block = await box(page.getByTestId('block-callout'));
  const content = await box(page.getByTestId('content'));
  expect(wrapper.height).toBe(block.height);
  expect(content.height + 30 + wrapper.height).toBe(300);
});

test('its text is plain text, never markup', async ({ mount, page }) => {
  const callout: CalloutBlock = {
    tone: 'info',
    text: '<b>not bold</b> **nor this**',
    reveal: 'last',
  };
  await mount(body(callout, 1, 1));

  await expect(page.getByTestId('block-callout')).toHaveText('<b>not bold</b> **nor this**');
  await expect(page.getByTestId('block-callout').locator('b')).toHaveCount(0);
});
