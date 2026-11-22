import { describe, expect, it } from 'vitest';
import { customLayoutSample } from '../../test-support/fixtures/customLayout';
import {
  checkLayout,
  classNameOf,
  isOwnClass,
  isPresent,
  lastStepOf,
  prefixOf,
  scopeOf,
  varsOf,
  variantClassesOf,
  visibleAt,
} from './bindings';
import type { CustomLayoutView, TreeNode } from './layoutTree';

const { view, sample } = customLayoutSample();

function node(over: Partial<TreeNode>): TreeNode {
  return { tag: 'div', classes: [], vars: [], variants: [], children: [], ...over };
}

function withRoot(root: TreeNode[], over: Partial<CustomLayoutView> = {}): CustomLayoutView {
  return { ...view, ...over, tree: { ...view.tree, root } };
}

const P = 'cl-0a1b2c3d-bar-chart__';

describe('scopeOf', () => {
  it('reads the slots at the top and the innermost item inside a list', () => {
    expect(scopeOf(sample, [])).toBe(sample);
    expect(scopeOf(sample, [{ list: 'bars', index: 1 }])).toEqual({
      label: 'Building',
      share: 38,
      state: 'normal',
    });
  });

  it('reads only the inner item for a nested path, and item for a scalar', () => {
    const slots = { rows: [{ name: 'a', cells: ['x', 'y'] }] };
    const path = [
      { list: 'rows', index: 0 },
      { list: 'cells', index: 1 },
    ];
    expect(scopeOf(slots, path)).toEqual({ item: 'y' });
    expect(scopeOf(slots, [{ list: 'rows', index: 0 }])).toEqual({ name: 'a', cells: ['x', 'y'] });
  });

  it('reads nothing for a path that leads nowhere or a name every object inherits', () => {
    expect(scopeOf(sample, [{ list: 'bars', index: 9 }])).toEqual({});
    expect(scopeOf(sample, [{ list: 'constructor', index: 0 }])).toEqual({});
  });
});

describe('isPresent', () => {
  it('is false for missing, null, empty text and an empty list; true for zero', () => {
    expect(isPresent(undefined)).toBe(false);
    expect(isPresent(null)).toBe(false);
    expect(isPresent('')).toBe(false);
    expect(isPresent([])).toBe(false);
    expect(isPresent(0)).toBe(true);
    expect(isPresent('a')).toBe(true);
    expect(isPresent([1])).toBe(true);
  });
});

describe('varsOf', () => {
  const vars = (name: string) => node({ vars: [{ name, slot: 'share' }] });
  it('sets a finite number as a plain number', () => {
    expect(varsOf(vars('--v'), { share: 46 })).toEqual({ '--v': 46 });
  });
  it('sets nothing for a value that is not a finite number', () => {
    expect(varsOf(vars('--v'), { share: '46' })).toEqual({});
    expect(varsOf(vars('--v'), { share: Number.NaN })).toEqual({});
    expect(varsOf(vars('--v'), { share: Infinity })).toEqual({});
    expect(varsOf(vars('--v'), {})).toEqual({});
  });
  it('sets nothing for a bad name or a frame token', () => {
    expect(varsOf(vars('v'), { share: 1 })).toEqual({});
    expect(varsOf(vars('--V'), { share: 1 })).toEqual({});
    expect(varsOf(vars('--a;b'), { share: 1 })).toEqual({});
    expect(varsOf(vars('--sw-ink'), { share: 1 })).toEqual({});
  });
});

describe('variantClassesOf', () => {
  const bar = view.tree.root[0].children[1].children[0].children[1].children[0];
  const types = { state: { type: 'enum' as const, values: ['normal', 'alert'] } };
  it('adds the prefixed class the server wrote', () => {
    expect(variantClassesOf(bar, { state: 'alert' }, types)).toEqual([`${P}is-alert`]);
  });
  it('adds nothing for a value outside the enum, a non-text or a non-enum slot', () => {
    expect(variantClassesOf(bar, { state: 'loud' }, types)).toEqual([]);
    expect(variantClassesOf(bar, { state: 3 }, types)).toEqual([]);
    expect(variantClassesOf(bar, { state: 'alert' }, { state: { type: 'text', max: 3 } })).toEqual(
      [],
    );
  });
  it('adds nothing for a value the enum names but the map lacks', () => {
    const wide = { state: { type: 'enum' as const, values: ['normal', 'alert', 'loud'] } };
    expect(variantClassesOf(bar, { state: 'loud' }, wide)).toEqual([]);
  });
});

describe('classNameOf', () => {
  it('joins the classes as given', () => {
    expect(classNameOf(node({ classes: ['a', 'b'] }))).toBe('a b');
    expect(classNameOf(node({}))).toBe('');
  });
});

describe('visibleAt', () => {
  it('shows everything with reveal off or no step', () => {
    expect(visibleAt(node({ step: 3 }), undefined, 0, false)).toBe(true);
    expect(visibleAt(node({}), undefined, 0, true)).toBe(true);
  });
  it('shows step N from click N', () => {
    expect(visibleAt(node({ step: 2 }), undefined, 1, true)).toBe(false);
    expect(visibleAt(node({ step: 2 }), undefined, 2, true)).toBe(true);
  });
  it('shows item i (1-based) from click i on step 0', () => {
    const each = node({ each: 'bars', step: 0 });
    expect(visibleAt(each, 0, 0, true)).toBe(false);
    expect(visibleAt(each, 0, 1, true)).toBe(true);
    expect(visibleAt(each, 1, 1, true)).toBe(false);
    expect(visibleAt(each, 1, 2, true)).toBe(true);
  });
});

describe('prefixOf and isOwnClass', () => {
  it('names the prefix from the scope and the layout name', () => {
    expect(prefixOf(view)).toBe(P);
    expect(prefixOf({ ...view, tree: { version: 1, root: [] } })).toBeNull();
    expect(prefixOf({ ...view, name: 'Bad Name' })).toBeNull();
  });
  it('accepts only one plain class under the prefix', () => {
    expect(isOwnClass(view, `${P}bar`)).toBe(true);
    expect(isOwnClass(view, `${P}bar other`)).toBe(false);
    expect(isOwnClass(view, `${P}`)).toBe(false);
    expect(isOwnClass(view, 'bar')).toBe(false);
    expect(isOwnClass(view, 'cl-ffffffff-bar-chart__bar')).toBe(false);
    expect(isOwnClass(view, 3)).toBe(false);
  });
  it('without a scope accepts any 8 hex characters before the name', () => {
    const bare = { ...view, tree: { version: 1 as const, root: [] } };
    expect(isOwnClass(bare, 'cl-ffffffff-bar-chart__bar')).toBe(true);
    expect(isOwnClass(bare, 'cl-xyz-bar-chart__bar')).toBe(false);
    expect(isOwnClass(bare, 'cl-ffffffff-other__bar')).toBe(false);
  });
});

describe('one prefix per layout', () => {
  const other = 'cl-ffffffff-bar-chart__';
  const variant = (c: string) => [{ slot: 'state', classes: { alert: c } }];
  it('refuses a class from another scope when the tree names its scope', () => {
    expect(checkLayout(withRoot([node({ classes: [`${P}a`, `${other}b`] })]))).toBe(false);
    expect(
      checkLayout(withRoot([node({ classes: [`${P}a`] }), node({ classes: [`${other}b`] })])),
    ).toBe(false);
  });
  it('without a scope, takes the first class and refuses any other prefix', () => {
    const bare = (root: TreeNode[]) => withRoot(root, { tree: { version: 1, root } });
    expect(checkLayout(bare([node({ classes: [`${P}a`] }), node({ classes: [`${P}b`] })]))).toBe(
      true,
    );
    expect(
      checkLayout(bare([node({ classes: [`${P}a`] }), node({ classes: [`${other}b`] })])),
    ).toBe(false);
    expect(
      checkLayout(bare([node({ classes: [`${other}a`] }), node({ classes: [`${P}b`] })])),
    ).toBe(false);
    expect(
      checkLayout(
        bare([node({ classes: [`${P}a`], children: [node({ classes: [`${other}b`] })] })]),
      ),
    ).toBe(false);
    expect(
      checkLayout(
        bare([node({ each: 'bars', classes: [`${P}a`], variants: variant(`${other}is-alert`) })]),
      ),
    ).toBe(false);
  });
  it('a malformed scope is treated as absent, and a wrong layout name is refused', () => {
    const root = [node({ classes: [`${P}a`] }), node({ classes: [`${other}b`] })];
    const bad = { version: 1 as const, scope: 'nothex!!', root };
    expect(checkLayout(withRoot(root, { tree: bad }))).toBe(false);
    const wrongName = [node({ classes: ['cl-0a1b2c3d-other__a'] })];
    expect(checkLayout(withRoot(wrongName, { tree: { version: 1, root: wrongName } }))).toBe(false);
  });
});

describe('checkLayout', () => {
  it('passes the contract sample', () => {
    expect(checkLayout(view)).toBe(true);
  });
  it('refuses an unknown tag', () => {
    expect(checkLayout(withRoot([node({ tag: 'script' })]))).toBe(false);
    expect(checkLayout(withRoot([node({ tag: 'img' })]))).toBe(false);
  });
  it('refuses a key the contract does not know', () => {
    const odd = { ...node({}), onclick: 'alert(1)' } as unknown as TreeNode;
    expect(checkLayout(withRoot([odd]))).toBe(false);
    const style = { ...node({}), style: 'color:red' } as unknown as TreeNode;
    expect(checkLayout(withRoot([style]))).toBe(false);
  });
  it('refuses a class outside the prefix, on a node or a variant', () => {
    expect(checkLayout(withRoot([node({ classes: ['evil'] })]))).toBe(false);
    const variant = { slot: 'headline', classes: { a: 'evil' } };
    expect(checkLayout(withRoot([node({ variants: [variant] })]))).toBe(false);
  });
  it('refuses a binding to a slot the layout does not have', () => {
    expect(checkLayout(withRoot([node({ slot: 'ghost' })]))).toBe(false);
    expect(checkLayout(withRoot([node({ if: 'ghost' })]))).toBe(false);
    expect(checkLayout(withRoot([node({ each: 'ghost' })]))).toBe(false);
    expect(checkLayout(withRoot([node({ vars: [{ name: '--v', slot: 'ghost' }] })]))).toBe(false);
    expect(checkLayout(withRoot([node({ slot: 'constructor' })]))).toBe(false);
  });
  it('refuses a name that reads an outer scope inside an each', () => {
    const inner = node({ slot: 'headline' });
    expect(checkLayout(withRoot([node({ each: 'bars', children: [inner] })]))).toBe(false);
  });
  it('refuses a bad step and a slot element with children', () => {
    expect(checkLayout(withRoot([node({ step: 21 })]))).toBe(false);
    expect(checkLayout(withRoot([node({ step: 0 })]))).toBe(false);
    expect(checkLayout(withRoot([node({ slot: 'headline', children: [node({})] })]))).toBe(false);
  });
});

describe('lastStepOf', () => {
  it('counts the steps the elements wait for', () => {
    expect(lastStepOf(view.tree.root, sample)).toBe(3);
    expect(lastStepOf([node({ step: 5 })], {})).toBe(5);
  });
});
