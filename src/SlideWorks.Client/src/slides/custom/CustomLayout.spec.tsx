import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { customLayoutSample } from '../../test-support/fixtures/customLayout';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { CustomLayoutView, TreeNode } from './layoutTree';

const { view, sample } = customLayoutSample();
const P = 'cl-0a1b2c3d-bar-chart__';

function node(over: Partial<TreeNode>): TreeNode {
  return { tag: 'div', classes: [], vars: [], variants: [], children: [], ...over };
}

function deckOf(...layouts: CustomLayoutView[]) {
  return {
    title: 'Ship smaller',
    author: 'Sam Rivera',
    parts: ['The cost', 'Slice it', 'Ship it'],
    look: { ...lookViewOf('talk', 'cobalt'), customLayouts: layouts },
    deepDives: [],
  };
}

type MountResult = { update: (c: React.JSX.Element) => Promise<void> };

type Options = {
  layouts?: CustomLayoutView[];
  slots?: { [name: string]: unknown };
  step?: number;
  surface?: 'light' | 'dark';
  layout?: string;
};

function slideOf(options: Options) {
  const layouts = options.layouts ?? [view];
  return (
    <SlideRenderer
      slide={
        {
          number: 2,
          layout: options.layout ?? layouts[0].name,
          part: 1,
          deepDive: false,
          surface: options.surface,
          slots: options.slots ?? sample,
        } as unknown as RenderableSlide
      }
      deck={deckOf(...layouts)}
      total={4}
      step={options.step ?? 0}
    />
  );
}

// A layout of the sample's name, scope and slots with its own tree and css.
function layoutOf(root: TreeNode[], css = '', slots: CustomLayoutView['slots'] = view.slots) {
  return { ...view, tree: { ...view.tree, root }, css, slots };
}

async function open(
  mount: (c: React.JSX.Element) => Promise<MountResult>,
  page: Page,
  options: Options = {},
) {
  await stubApi(page, {});
  return mount(slideOf(options));
}

const bars = (page: Page) => page.locator(`.${P}row`);
const barOf = (page: Page, index: number) => page.locator(`.${P}bar`).nth(index);
const colour = (locator: ReturnType<Page['locator']>) =>
  locator.evaluate((el) => getComputedStyle(el).color);

test.describe('bindings', () => {
  test('number sets a variable', async ({ mount, page }) => {
    await open(mount, page);
    const track = await page.locator(`.${P}track`).first().boundingBox();
    const bar = await barOf(page, 0).boundingBox();
    expect(bar!.width / track!.width).toBeCloseTo(0.46, 2);
    const second = await barOf(page, 1).boundingBox();
    expect(second!.width / track!.width).toBeCloseTo(0.38, 2);
  });

  test('choice picks a variant', async ({ mount, page }) => {
    await open(mount, page);
    await expect(barOf(page, 0)).toHaveClass(new RegExp(`${P}is-alert`));
    await expect(barOf(page, 1)).toHaveClass(new RegExp(`${P}is-normal`));
    const background = (i: number) =>
      barOf(page, i).evaluate((el) => getComputedStyle(el).backgroundColor);
    expect(await background(0)).not.toBe(await background(2));
  });

  test('repeats an element for each item', async ({ mount, page }) => {
    await open(mount, page);
    await expect(bars(page)).toHaveCount(3);
    await expect(page.locator(`.${P}label`)).toHaveText(['Meetings', 'Building', 'Reviews']);
  });

  test('data-if hides an element whose slot is absent', async ({ mount, page }) => {
    const without: { [name: string]: unknown } = { ...sample, note: undefined };
    await open(mount, page, { slots: without });
    await expect(page.locator(`.${P}note`)).toHaveCount(0);
  });

  test('an absent optional slot draws no element', async ({ mount, page }) => {
    const slots = {
      headline: 'x',
      bars: [],
    };
    const layout = layoutOf([
      node({
        classes: [`${P}chart`],
        children: [
          node({ classes: [`${P}headline`], slot: 'headline' }),
          node({ classes: [`${P}note`], slot: 'note' }),
        ],
      }),
    ]);
    await open(mount, page, { layouts: [layout], slots });
    await expect(page.locator(`.${P}headline`)).toHaveText('x');
    await expect(page.locator(`.${P}note`)).toHaveCount(0);
  });

  test('an inner list reads the inner item', async ({ mount, page }) => {
    const layout = layoutOf(
      [
        node({
          each: 'groups',
          classes: [`${P}row`],
          children: [
            node({ tag: 'span', classes: [`${P}label`], slot: 'name' }),
            node({
              each: 'tags',
              classes: [`${P}bar`],
              children: [node({ tag: 'span', classes: [`${P}note`], slot: 'item' })],
            }),
          ],
        }),
      ],
      '',
      {
        groups: {
          type: 'list',
          min: 1,
          max: 3,
          of: {
            name: { type: 'text', max: 20 },
            tags: { type: 'list', min: 0, max: 3, of: { type: 'text', max: 10 } },
          },
        },
      },
    );
    const slots = {
      groups: [
        { name: 'A', tags: ['a1', 'a2'] },
        { name: 'B', tags: ['b1'] },
      ],
    };
    await open(mount, page, { layouts: [layout], slots });
    await expect(page.locator(`.${P}label`)).toHaveText(['A', 'B']);
    await expect(page.locator(`.${P}note`)).toHaveText(['a1', 'a2', 'b1']);
  });
});

test.describe('steps', () => {
  const oneByOne = { ...sample, reveal: 'one-by-one' };

  test('data-step reveals', async ({ mount, page }) => {
    const component = await open(mount, page, { slots: oneByOne, step: 1 });
    const second = bars(page).nth(1);
    await expect(second).toHaveCSS('visibility', 'hidden');
    await expect(second).toHaveAttribute('data-revealed', 'false');
    const hiddenBox = await second.boundingBox();
    await expect(bars(page).nth(0)).toHaveCSS('visibility', 'visible');
    // The same slide at step 2: visible, in the same box.
    await component.update(slideOf({ slots: oneByOne, step: 2 }));
    const shown = bars(page).nth(1);
    await expect(shown).toHaveCSS('visibility', 'visible');
    expect(await shown.boundingBox()).toEqual(hiddenBox);
  });

  test('everything shows when reveal is off', async ({ mount, page }) => {
    await open(mount, page, { step: 0 });
    for (let i = 0; i < 3; i++) {
      await expect(bars(page).nth(i)).toHaveCSS('visibility', 'visible');
    }
  });

  test('a layout rule cannot show a waiting step', async ({ mount, page }) => {
    const css = `${view.css}\n.${P}row { visibility: visible !important; opacity: 1 !important; }`;
    await open(mount, page, { layouts: [{ ...view, css }], slots: oneByOne, step: 0 });
    await expect(bars(page).nth(2)).toHaveAttribute('style', /visibility: hidden/);
    await expect(bars(page).nth(2)).toHaveAttribute('aria-hidden', 'true');
  });
});

test.describe('blocks and surfaces', () => {
  test('levels slot draws the block', async ({ mount, page }) => {
    const layout = layoutOf([node({ classes: [`${P}chart`], slot: 'ladder' })], '', {
      ladder: { type: 'levels' },
    });
    const slots = { ladder: { items: [{ text: 'One' }, { text: 'Two', state: 'current' }] } };
    await open(mount, page, { layouts: [layout], slots });
    const wrapper = page.locator('[data-sw-block]');
    await expect(wrapper).toHaveClass(new RegExp(`${P}chart`));
    await expect(wrapper.getByTestId('levels-rung')).toHaveCount(2);
  });

  test('dark surface resolves night tokens', async ({ mount, page }) => {
    const component = await open(mount, page, { surface: 'light' });
    const chart = page.locator(`.${P}chart`);
    const light = await colour(chart);
    await component.update(slideOf({ surface: 'dark' }));
    const dark = await colour(chart);
    expect(dark).not.toBe(light);
    const night = await page.getByTestId('slide-frame').evaluate((frame) => {
      const probe = document.createElement('i');
      probe.style.color = 'var(--sw-night-ink)';
      frame.appendChild(probe);
      const value = getComputedStyle(probe).color;
      probe.remove();
      return value;
    });
    expect(dark).toBe(night);
  });
});

test.describe('hostile input', () => {
  test('slot text with markup in it is drawn as text', async ({ mount, page }) => {
    const hostile = '<img src=x onerror=alert(1)>';
    await open(mount, page, { slots: { ...sample, headline: hostile } });
    await expect(page.locator(`.${P}headline`)).toHaveText(hostile);
    await expect(page.locator('img[src="x"]')).toHaveCount(0);
  });

  test('an unknown tag draws the error and nothing else', async ({ mount, page }) => {
    const layout = layoutOf([node({ tag: 'script', classes: [`${P}chart`] })]);
    await open(mount, page, { layouts: [layout] });
    await expect(page.getByTestId('custom-layout-error')).toHaveText(
      "This slide's layout could not be drawn.",
    );
    await expect(page.getByTestId('custom-layout-error')).toHaveAttribute('role', 'alert');
    await expect(page.getByTestId('custom-layout')).toHaveCount(0);
    await expect(page.getByTestId('slide-2').locator('script')).toHaveCount(0);
  });

  test('an event-handler attribute on a node draws the error', async ({ mount, page }) => {
    const odd = { ...node({ classes: [`${P}chart`] }), onclick: 'alert(1)' } as unknown as TreeNode;
    await open(mount, page, { layouts: [layoutOf([odd])] });
    await expect(page.getByTestId('custom-layout-error')).toBeVisible();
    await expect(page.locator('[onclick]')).toHaveCount(0);
  });

  test('a style attribute on a node draws the error', async ({ mount, page }) => {
    const odd = {
      ...node({ classes: [`${P}chart`] }),
      style: 'position:fixed;inset:0',
    } as unknown as TreeNode;
    await open(mount, page, { layouts: [layoutOf([odd])] });
    await expect(page.getByTestId('custom-layout-error')).toBeVisible();
    await expect(page.locator('[style*="fixed"]')).toHaveCount(0);
  });

  test('a class outside the prefix draws the error', async ({ mount, page }) => {
    const layout = layoutOf([node({ classes: ['sw-footer'] })]);
    await open(mount, page, { layouts: [layout] });
    await expect(page.getByTestId('custom-layout-error')).toBeVisible();
    await expect(page.getByTestId('custom-layout')).toHaveCount(0);
  });

  test('a variant class outside the prefix draws the error', async ({ mount, page }) => {
    const variant = { slot: 'state', classes: { normal: 'sw-footer', alert: `${P}is-alert` } };
    const layout = layoutOf([
      node({ each: 'bars', classes: [`${P}row`], children: [node({ variants: [variant] })] }),
    ]);
    await open(mount, page, { layouts: [layout] });
    await expect(page.getByTestId('custom-layout-error')).toBeVisible();
  });

  test('an unknown slot binding draws the error', async ({ mount, page }) => {
    const layout = layoutOf([node({ classes: [`${P}chart`], slot: 'ghost' })]);
    await open(mount, page, { layouts: [layout] });
    await expect(page.getByTestId('custom-layout-error')).toBeVisible();
  });

  test('a slot that does not hold what its type says draws the error', async ({ mount, page }) => {
    await open(mount, page, { slots: { ...sample, headline: { not: 'text' } } });
    await expect(page.getByTestId('custom-layout-error')).toBeVisible();
  });

  test('a variant value outside the enum adds no class', async ({ mount, page }) => {
    const bad = { ...sample, bars: [{ label: 'A', share: 5, state: 'loud' }] };
    await open(mount, page, { slots: bad });
    const bar = barOf(page, 0);
    await expect(bar).toHaveClass(`${P}bar`);
  });

  test('a variable that is not a number is not set', async ({ mount, page }) => {
    const bad = { ...sample, bars: [{ label: 'A', share: '50', state: 'normal' }] };
    await open(mount, page, { slots: bad });
    await expect(barOf(page, 0)).not.toHaveAttribute('style', /--v/);
  });

  test('a variable whose name is not --[a-z][a-z0-9-]* is not set', async ({ mount, page }) => {
    const component = await open(mount, page);
    for (const name of ['--V', '--sw-ink', 'width']) {
      const layout = layoutOf(
        [
          node({
            classes: [`${P}chart`],
            vars: [{ name, slot: 'n' }],
            children: [],
          }),
        ],
        '',
        { n: { type: 'number', min: 0, max: 9 } },
      );
      await component.update(slideOf({ layouts: [layout], slots: { n: 5 } }));
      await expect(page.locator(`.${P}chart`)).not.toHaveAttribute('style', /.+/);
    }
  });

  test("the layout's css is a text node", async ({ mount, page }) => {
    const css = `${view.css}\n/* </style><img src=x onerror=alert(1)> */`;
    await open(mount, page, { layouts: [{ ...view, css }] });
    await expect(page.locator('img[src="x"]')).toHaveCount(0);
    const style = page.getByTestId('custom-layout').locator('> style');
    await expect(style).toHaveCount(1);
    expect(await style.evaluate((el) => [el.childNodes.length, el.firstChild?.nodeType])).toEqual([
      1, 3,
    ]);
  });

  test('first-child matches the first top-level element', async ({ mount, page }) => {
    const css = `.${P}chart:first-child { outline: 3px solid; }`;
    await open(mount, page, { layouts: [{ ...view, css: view.css + css }] });
    await expect(page.locator(`.${P}chart`)).toHaveCSS('outline-width', '3px');
    const root = page.getByTestId('custom-layout');
    expect(
      await root.evaluate((el) => [...el.children].map((c) => c.tagName.toLowerCase())),
    ).toEqual(['style', 'div']);
  });
});

test.describe('frame', () => {
  test('an unknown layout name draws the unknown-layout state', async ({ mount, page }) => {
    const component = await open(mount, page, { layout: 'no-such-layout' });
    await expect(page.getByTestId('unknown-layout')).toHaveText(
      "This slide's layout is not in the deck's template.",
    );
    for (const name of ['constructor', 'toString']) {
      await component.update(slideOf({ layout: name }));
      await expect(page.getByTestId('unknown-layout')).toBeVisible();
    }
  });

  test('a custom slide keeps the tracker and footer', async ({ mount, page }) => {
    await open(mount, page);
    await expect(page.getByTestId('part-tracker')).toBeVisible();
    await expect(page.getByTestId('slide-footer')).toBeVisible();
  });

  test("the layout's css does not style the footer", async ({ mount, page }) => {
    const component = await open(mount, page);
    const read = () =>
      page
        .getByTestId('slide-footer')
        .evaluate((el) => JSON.stringify(Object.entries(getComputedStyle(el))));
    const before = await read();
    const css = `.${P}footer { display: none; font-size: 90px; color: red; }`;
    await component.update(slideOf({ layouts: [{ ...view, css: view.css + css }] }));
    expect(await read()).toBe(before);
  });

  test('a box positioned outside the body is clipped', async ({ mount, page }) => {
    const css = `.${P}chart { position: absolute; top: -400px; left: 0; width: 1500px; height: 600px; background: var(--sw-bad); }`;
    await open(mount, page, { layouts: [{ ...view, css: view.css + css }] });
    const tracker = page.getByTestId('part-tracker');
    const box = (await tracker.boundingBox())!;
    // The tracker's centre still hits the tracker, not the layout's box.
    const hit = await page.evaluate(
      ([x, y]) => {
        const target = document.elementFromPoint(x, y);
        return target?.closest('[data-testid="part-tracker"]') !== null;
      },
      [box.x + box.width / 2, box.y + box.height / 2],
    );
    expect(hit).toBe(true);
  });

  test('a tall layout does not move the footer', async ({ mount, page }) => {
    const component = await open(mount, page);
    const before = await page.getByTestId('slide-footer').boundingBox();
    const css = `.${P}chart { height: 5000px; }`;
    await component.update(slideOf({ layouts: [{ ...view, css: view.css + css }] }));
    expect(await page.getByTestId('slide-footer').boundingBox()).toEqual(before);
    const body = (await page.locator('[data-custom-layout-body]').boundingBox())!;
    expect(body.height).toBeLessThan(1100);
  });
});

test.describe('screenshots', () => {
  test('screenshot: custom layout, sample', async ({ mount, page }) => {
    await open(mount, page);
    await expect(page.getByTestId('slide-2')).toHaveScreenshot('custom-layout-sample.png');
  });

  test('screenshot: custom layout, dark', async ({ mount, page }) => {
    await open(mount, page, { surface: 'dark' });
    await expect(page.getByTestId('slide-2')).toHaveScreenshot('custom-layout-dark.png');
  });
});
