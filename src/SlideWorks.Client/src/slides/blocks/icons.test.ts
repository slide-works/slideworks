// @vitest-environment node
import iconsJson from '@contracts/icons.json' with { type: 'json' };
import { describe, expect, it } from 'vitest';
import { ICONS, iconFor } from './icons';

const listed = iconsJson.icons;
// Everything the installed package exports, by name. Read at run time: the
// app itself only ever takes the 48 by named import.
const exported = (await import('lucide-react')) as unknown as Record<string, unknown>;

describe('icons', () => {
  it('every name maps to a component', () => {
    expect(listed).toHaveLength(48);
    for (const { name, lucide: component } of listed) {
      expect(iconFor(name), name).toBeDefined();
      // The drawing is the one the contract names, not just any drawing.
      expect(iconFor(name), name).toBe(exported[component]);
    }
  });

  it('every lucide name in the contract is exported by the installed package', () => {
    const missing = listed.filter(({ lucide: component }) => exported[component] === undefined);
    expect(missing).toEqual([]);
  });

  it('the map has no name the contract does not list', () => {
    expect(Object.keys(ICONS).sort()).toEqual(listed.map(({ name }) => name).sort());
  });

  it('an unknown name, or the name of an object member, has no drawing', () => {
    expect(iconFor('no-such-icon')).toBeUndefined();
    expect(iconFor('constructor')).toBeUndefined();
    expect(iconFor('toString')).toBeUndefined();
    expect(iconFor('')).toBeUndefined();
  });
});
