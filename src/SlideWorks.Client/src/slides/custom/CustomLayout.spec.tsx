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
    const withNote = { ...sample, note: 'A note' };
    const component = await open(mount, page, { slots: withNote });
    await expect(page.locator(`.${P}note`)).toHaveText('A note');
    const without: { [name: string]: unknown } = { ...sample };
    delete without.note;
    await component.update(slideOf({ slots: without }));
    await expect(page.locator(`.${P}headline`)).toBeVisible();
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
    const css = `${view.css}\n.${P}row { visibility: visible; opacity: 1; }`;
    await open(mount, page, { layouts: [{ ...view, css }], slots: oneByOne, step: 0 });
    const waiting = bars(page).nth(2);
    await expect(waiting).toHaveAttribute('aria-hidden', 'true');
    const drawn = await waiting.evaluate((el) => {
      const style = getComputedStyle(el);
      return [style.visibility, style.opacity];
    });
    expect(drawn).toEqual(['hidden', '0']);
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

  test('a levels block with a bad item draws the error', async ({ mount, page }) => {
    const layout = layoutOf([node({ classes: [`${P}chart`], slot: 'ladder' })], '', {
      ladder: { type: 'levels' },
    });
    const bad = [
      { items: [null] },
      { items: [{ text: 5 }] },
      { items: [{ text: 'a', state: 'loud' }] },
      { items: [{ text: 'a', mark: 1 }] },
      { title: 3, items: [] },
    ];
    const component = await open(mount, page, { layouts: [layout], slots: { ladder: bad[0] } });
    for (const ladder of bad) {
      await component.update(slideOf({ layouts: [layout], slots: { ladder } }));
      await expect(page.getByTestId('custom-layout-error')).toBeVisible();
    }
  });

  test('a facts block with a bad tile draws the error', async ({ mount, page }) => {
    const layout = layoutOf([node({ classes: [`${P}chart`], slot: 'f' })], '', {
      f: { type: 'facts' },
    });
    const bad = [
      [null],
      [{ label: 'a' }],
      [{ label: 'a', value: 2 }],
      [{ label: 'a', value: 'b', tone: 'loud' }],
    ];
    const component = await open(mount, page, { layouts: [layout], slots: { f: bad[0] } });
    for (const f of bad) {
      await component.update(slideOf({ layouts: [layout], slots: { f } }));
      await expect(page.getByTestId('custom-layout-error')).toBeVisible();
    }
  });

  test('a callout with a bad tone, text or reveal draws the error', async ({ mount, page }) => {
    const layout = layoutOf([node({ classes: [`${P}chart`], slot: 'c' })], '', {
      c: { type: 'callout' },
    });
    const good = { tone: 'accent', text: 'Hi' };
    const component = await open(mount, page, { layouts: [layout], slots: { c: good } });
    await expect(page.getByTestId('custom-slot-c')).toBeVisible();
    for (const c of [
      { tone: 'loud', text: 'Hi' },
      { tone: 'accent', text: 4 },
      { tone: 'accent', text: 'Hi', reveal: 'sometimes' },
      { tone: 'accent', text: 'Hi', fill: 'neon' },
    ]) {
      await component.update(slideOf({ layouts: [layout], slots: { c } }));
      await expect(page.getByTestId('custom-layout-error')).toBeVisible();
    }
  });

  test('a list slot that is not a list draws the error; an empty list does not', async ({
    mount,
    page,
  }) => {
    const component = await open(mount, page, { slots: { ...sample, bars: [] } });
    await expect(page.getByTestId('custom-layout')).toBeVisible();
    await expect(bars(page)).toHaveCount(0);
    for (const bars of ['nope', { a: 1 }, 7, [null], ['x']]) {
      await component.update(slideOf({ slots: { ...sample, bars } }));
      await expect(page.getByTestId('custom-layout-error')).toBeVisible();
    }
  });

  test('a malformed list schema draws the error', async ({ mount, page }) => {
    const root = [
      node({
        each: 'g',
        classes: [`${P}row`],
        children: [node({ classes: [`${P}label`], slot: 'name' })],
      }),
    ];
    const list = (of: unknown) =>
      ({ g: { type: 'list', min: 0, max: 3, of } }) as unknown as CustomLayoutView['slots'];
    const slots = { g: [{ name: 'a' }] };
    const good = layoutOf(root, '', list({ name: { type: 'text', max: 9 } }));
    const component = await open(mount, page, { layouts: [good], slots });
    await expect(page.locator(`.${P}label`)).toHaveText('a');
    for (const of of [null, 'x', [], { name: { type: 'weird' } }, { name: null }]) {
      await component.update(slideOf({ layouts: [layoutOf(root, '', list(of))], slots }));
      await expect(page.getByTestId('custom-layout-error')).toBeVisible();
    }
  });

  test('a class from another scope draws the error, with and without tree.scope', async ({
    mount,
    page,
  }) => {
    const foreign = 'cl-ffffffff-bar-chart__x';
    const root = [node({ classes: [`${P}chart`], children: [node({ classes: [foreign] })] })];
    const component = await open(mount, page, { layouts: [layoutOf(root)] });
    await expect(page.getByTestId('custom-layout-error')).toBeVisible();
    const bare = { ...layoutOf(root), tree: { version: 1 as const, root } };
    await component.update(slideOf({ layouts: [bare] }));
    await expect(page.getByTestId('custom-layout-error')).toBeVisible();
    const fine = [node({ classes: [`${P}chart`], children: [node({ classes: [`${P}x`] })] })];
    await component.update(
      slideOf({ layouts: [{ ...layoutOf(fine), tree: { version: 1, root: fine } }] }),
    );
    await expect(page.getByTestId('custom-layout')).toBeVisible();
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
    const reads = (testId: string) =>
      page.getByTestId(testId).evaluate((el) => {
        const style = getComputedStyle(el);
        return [style.fontSize, style.color, style.position, style.display];
      });
    const marked = layoutOf([node({ classes: [`${P}chart`, `${P}footer`] })]);
    const component = await open(mount, page, { layouts: [marked] });
    const chart = page.locator(`.${P}chart`);
    const chartSize = () => chart.evaluate((el) => getComputedStyle(el).fontSize);
    const footerBefore = [await reads('slide-footer'), await reads('slide-footer-text')];
    const chartBefore = await chartSize();
    const css = `.${P}footer { font-size: 90px; color: rgb(255, 0, 0); position: absolute; }`;
    await component.update(slideOf({ layouts: [{ ...marked, css }] }));
    // The rule matches inside the custom layout...
    await expect(chart).toHaveCSS('font-size', '90px');
    expect(chartBefore).not.toBe('90px');
    // ...and leaves the footer as it was.
    expect([await reads('slide-footer'), await reads('slide-footer-text')]).toEqual(footerBefore);
  });

  test('a box positioned outside the body is clipped', async ({ mount, page }) => {
    const css = `.${P}chart { position: absolute; top: -400px; left: 0; width: 1500px; height: 600px; background: var(--sw-bad); }`;
    await open(mount, page, { layouts: [{ ...view, css: view.css + css }] });
    const tracker = page.getByTestId('part-tracker');
    const box = (await tracker.boundingBox())!;
    const x = box.x + box.width / 2;
    const y = box.y + box.height / 2;
    // The layout's box reaches over the tracker's centre, so only clipping keeps it off.
    const chart = (await page.locator(`.${P}chart`).boundingBox())!;
    expect(x).toBeGreaterThanOrEqual(chart.x);
    expect(x).toBeLessThanOrEqual(chart.x + chart.width);
    expect(y).toBeGreaterThanOrEqual(chart.y);
    expect(y).toBeLessThanOrEqual(chart.y + chart.height);
    const hit = await page.evaluate(
      ([px, py]) => {
        const target = document.elementFromPoint(px, py);
        return target?.closest('[data-testid="part-tracker"]') ?? null;
      },
      [x, y],
    );
    expect(hit).not.toBeNull();
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
