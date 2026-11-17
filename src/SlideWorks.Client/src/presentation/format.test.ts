import { describe, expect, it } from 'vitest';
import { formatClock, formatElapsed } from './format';

describe('formatElapsed', () => {
  it('shows minutes and seconds under an hour', () => {
    expect(formatElapsed(760000)).toBe('12:40');
    expect(formatElapsed(0)).toBe('0:00');
    expect(formatElapsed(5_000)).toBe('0:05');
    expect(formatElapsed(59_999)).toBe('0:59');
    expect(formatElapsed(3_599_999)).toBe('59:59');
  });

  it('shows hours from an hour', () => {
    expect(formatElapsed(3_600_000)).toBe('1:00:00');
    expect(formatElapsed(3_723_000)).toBe('1:02:03');
    expect(formatElapsed(36_000_000)).toBe('10:00:00');
  });

  it('puts a minus before a negative value, and none before less than a second', () => {
    expect(formatElapsed(-135_000)).toBe('-2:15');
    expect(formatElapsed(-3_723_000)).toBe('-1:02:03');
    expect(formatElapsed(-999)).toBe('0:00');
  });
});

describe('formatClock', () => {
  it('shows a 24-hour hh:mm', () => {
    expect(formatClock(872)).toBe('14:32');
    expect(formatClock(0)).toBe('00:00');
    expect(formatClock(1)).toBe('00:01');
    expect(formatClock(1439)).toBe('23:59');
  });

  it('wraps at a day', () => {
    expect(formatClock(1440)).toBe('00:00');
    expect(formatClock(1440 + 872)).toBe('14:32');
  });
});
