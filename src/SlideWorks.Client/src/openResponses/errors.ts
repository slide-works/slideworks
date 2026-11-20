// The sentence a guest sees for each error code the open-response routes refuse with (SW-8 D3, D4). The codes are the
// server's OpenTextRules codes and the rate limit's.
export const OPEN_RESPONSE_ERRORS = {
  required: 'Type something first.',
  too_many_words: 'Send at most 3 words.',
  word_too_long: 'Each word can be at most 25 characters.',
  text_too_long: 'Your answer can be at most 200 characters.',
  profane: 'That word isn’t allowed here. Try a different one.',
  too_many_answers: 'You’re sending too fast. Wait a moment and try again.',
  stale: 'The question moved on. Check it and send again.',
  wrong_phase: 'This question isn’t taking answers right now.',
} as const;

// The refusals that mean the phone's copy of the round is out of date, so the hook reads it again.
export const OPEN_RESPONSE_REREAD: readonly string[] = ['stale', 'wrong_phase'];

// The sentence for a failed call whose code is not one of OPEN_RESPONSE_ERRORS: a network failure or a refusal this page
// has no words for.
export const OPEN_RESPONSE_FAILED = 'That didn’t go through. Try again.';

export function openResponseErrorSentence(code: string | undefined): string {
  return code !== undefined && Object.hasOwn(OPEN_RESPONSE_ERRORS, code)
    ? OPEN_RESPONSE_ERRORS[code as keyof typeof OPEN_RESPONSE_ERRORS]
    : OPEN_RESPONSE_FAILED;
}
