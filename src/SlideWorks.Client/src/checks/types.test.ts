import { describe, expect, it } from 'vitest';
import { RULE_ORDER } from './types';

describe('RULE_ORDER', () => {
  it('lists each of the 10 rules once, in the server enum order', () => {
    expect(RULE_ORDER).toHaveLength(10);
    expect(new Set(RULE_ORDER).size).toBe(10);
    expect(RULE_ORDER[0]).toBe('overflow');
    expect(RULE_ORDER[9]).toBe('textOnImage');
  });
});
