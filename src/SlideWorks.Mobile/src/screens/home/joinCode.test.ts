import { describe, expect, it } from 'vitest';
import { isJoinCode } from './joinCode';

describe('join code', () => {
  it('is exactly six digits', () => {
    expect(isJoinCode('123456')).toBe(true);
    for (const text of ['', '12345', '1234567', '12345a', '123 456', ' 123456', '１２３４５６']) {
      expect(isJoinCode(text)).toBe(false);
    }
  });
});
