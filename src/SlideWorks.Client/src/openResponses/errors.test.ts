import { describe, expect, it } from 'vitest';
import { OPEN_RESPONSE_ERRORS, OPEN_RESPONSE_FAILED, openResponseErrorSentence } from './errors';

describe('openResponseErrorSentence', () => {
  it('OpenResponseErrorSentence_WhenTheCodeIsProfane_ShouldReturnItsSentence', () => {
    expect(openResponseErrorSentence('profane')).toBe(OPEN_RESPONSE_ERRORS.profane);
  });

  it('OpenResponseErrorSentence_WhenTheCodeIsUnknown_ShouldReturnTheFailedSentence', () => {
    expect(openResponseErrorSentence('toString')).toBe(OPEN_RESPONSE_FAILED);
    expect(openResponseErrorSentence(undefined)).toBe(OPEN_RESPONSE_FAILED);
  });
});
