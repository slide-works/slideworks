import { describe, expect, it } from 'vitest';
import names from '@contracts/layout-names.json';
import { LAYOUT_NAMES } from './slots';

describe('the layout names', () => {
  it('names match the names file', () => {
    expect([...LAYOUT_NAMES]).toEqual(names.layouts);
  });
});
