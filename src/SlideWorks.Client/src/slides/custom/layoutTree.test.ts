import { describe, expect, it } from 'vitest';
import compiled from '@contracts/custom-layout-compiled.sample.json' with { type: 'json' };
import { variantClass, type LayoutTree, type TreeNode, type TreeVariant } from './layoutTree';

const tree: LayoutTree = { ...compiled.tree, version: 1 };

function nodes(list: TreeNode[]): TreeNode[] {
  return list.flatMap((node) => [node, ...nodes(node.children)]);
}

const variant: TreeVariant = nodes(tree.root).flatMap((node) => node.variants)[0];

describe('variantClass', () => {
  it('picks the class the server wrote for the value', () => {
    expect(variant.slot).toBe('state');
    expect(variantClass(variant, 'alert')).toBe('cl-0a1b2c3d-bar-chart__is-alert');
    expect(variantClass(variant, 'normal')).toBe('cl-0a1b2c3d-bar-chart__is-normal');
  });

  it('picks nothing for a value the map does not hold', () => {
    expect(variantClass(variant, 'loud')).toBeUndefined();
    expect(variantClass(variant, '')).toBeUndefined();
    expect(variantClass(variant, undefined)).toBeUndefined();
    expect(variantClass(variant, 3)).toBeUndefined();
  });

  it('picks nothing for a name every object inherits', () => {
    expect(variantClass(variant, 'constructor')).toBeUndefined();
    expect(variantClass(variant, 'toString')).toBeUndefined();
    expect(variantClass(variant, '__proto__')).toBeUndefined();
  });

  it('never returns a class without the prefix of the tree', () => {
    const prefix = `cl-${tree.scope}-${compiled.name}__`;
    const picked = Object.keys(variant.classes).map((value) => variantClass(variant, value));

    expect(picked).toHaveLength(compiled.schema.slots.bars.of.state.values.length);
    for (const name of picked) {
      expect(name?.startsWith(prefix)).toBe(true);
    }
  });
});
