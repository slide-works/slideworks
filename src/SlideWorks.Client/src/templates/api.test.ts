import { describe, expect, it } from 'vitest';
import { ApiError } from '../api/client';
import { templateErrorCode, templateErrorMessage, templateErrorPath } from './api';

const refusal = new ApiError('manifest_invalid', 400, {
  code: 'manifest_invalid',
  detail: 'The template manifest is not valid: fonts.',
  path: 'fonts',
});

describe('template errors', () => {
  it("hands over a refusal's code, the server's sentence and the field", () => {
    expect(templateErrorCode(refusal)).toBe('manifest_invalid');
    expect(templateErrorMessage(refusal)).toBe('The template manifest is not valid: fonts.');
    expect(templateErrorPath(refusal)).toBe('fonts');
  });

  it('takes the code of a 409 too', () => {
    const stale = new ApiError('stale_revision', 409, { code: 'stale_revision' });
    expect(templateErrorCode(stale)).toBe('stale_revision');
  });

  it('never shows a code: a refusal without a sentence gets a fixed one', () => {
    const bare = new ApiError('layout_overflows', 400, { code: 'layout_overflows' });
    expect(templateErrorCode(bare)).toBe('layout_overflows');
    expect(templateErrorMessage(bare)).toBe("That didn't work. Try again.");
    expect(templateErrorPath(bare)).toBeNull();
  });

  it('shows an error that is itself a sentence, and reads no code from it', () => {
    const missing = new ApiError('No such template.', 404, { code: 'No such template.' });
    expect(templateErrorMessage(missing)).toBe('No such template.');
    expect(templateErrorCode(missing)).toBeNull();
    const invalid = new ApiError('name: required', 400, { code: 'name: required' });
    expect(templateErrorMessage(invalid)).toBe('name: required');
    expect(templateErrorCode(invalid)).toBeNull();
  });

  it('gives the fixed sentence for a failure with no body, and no code', () => {
    const failed = new ApiError('Request failed (500)', 500);
    expect(templateErrorMessage(failed)).toBe("That didn't work. Try again.");
    expect(templateErrorCode(failed)).toBeNull();
    expect(templateErrorCode(new ApiError('Request failed (400)', 400))).toBeNull();
  });

  it('says the server cannot be reached when the call never got an answer', () => {
    const dropped = new TypeError('Failed to fetch');
    expect(templateErrorMessage(dropped)).toBe("Can't reach SlideWorks.");
    expect(templateErrorCode(dropped)).toBeNull();
    expect(templateErrorPath(dropped)).toBeNull();
  });
});
