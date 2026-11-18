export type QuestionErrorTextOptions = {
  // The presenter's name, as the copy names them ("Sam has closed questions.").
  presenter: string;
  // While asking is limited: the whole seconds left, from useQuestions().retryIn. 0 or left out otherwise.
  retryIn?: number;
};

// A rule the server names that isn't listed below, a code it doesn't know, or a 400 with no error in its body: the
// guest is never shown a code.
const UNKNOWN = 'Something went wrong on our side. Try again in a moment.';

// One plain sentence per code (Q4, Q7), so no code reaches a guest. The ones that name the presenter are functions.
const MESSAGES: Record<string, (options: QuestionErrorTextOptions) => string> = {
  questions_closed: ({ presenter }) => `${presenter} has closed questions. You can still upvote.`,
  questions_off_here: () => 'Questions are off on this slide. You can still upvote.',
  questions_off: ({ presenter }) => `${presenter} has turned questions off.`,
  too_many_questions: ({ retryIn = 0 }) =>
    retryIn > 0
      ? `You can ask again in ${retryIn} s`
      : 'You’ve asked a lot in the last minute. Wait a moment, then ask again.',
  too_many_votes: () =>
    'You’ve changed your upvotes a lot in the last minute. Wait a moment, then try again.',
  stale: () =>
    'The questions changed before your upvote was sent. Check the list and upvote again.',
  reactions_off: () => 'Reactions are off right now.',
  too_many_reactions: () => 'That’s a lot of reactions. Wait a moment, then react again.',
  too_many_attempts: () =>
    'Too many tries from this network right now. Wait a moment, then try again.',
  no_session: () => 'This talk isn’t live any more.',
  no_guest: () =>
    'Your browser isn’t keeping this site’s cookie. Allow cookies for this site, then reload the page.',
  'text: required': () => 'Type a question first.',
  'name: not allowed': ({ presenter }) =>
    `${presenter} has turned names off. Send your question without one.`,
};

const TEXT_AT_MOST = /^text: at most (\d+)$/;
const NAME_AT_MOST = /^name: at most (\d+)$/;
const UNKNOWN_QUESTION = /^picks: unknown question \d+$/;
// api/client's and questionsApi's text for a refusal whose body had no error.
const NO_ERROR_BODY = /^Request failed \(\d+\)$/;
// A sentence the client wrote itself (a call that never reached the server) starts with a capital; a code or a rule
// from the server never does.
const SENTENCE = /^[A-Z]/;

// What the guest reads for useQuestions().error or usePhoneReactions().error: a code or rule from the server becomes
// a sentence, and a sentence the client wrote passes through as it is.
export function questionErrorText(error: string, options: QuestionErrorTextOptions): string {
  const message = MESSAGES[error];
  if (message) return message(options);
  const text = TEXT_AT_MOST.exec(error);
  if (text) return `Keep your question to ${text[1]} characters.`;
  const name = NAME_AT_MOST.exec(error);
  if (name) return `Keep your name to ${name[1]} characters.`;
  if (UNKNOWN_QUESTION.test(error))
    return 'That question isn’t on the list any more. Check the list and upvote again.';
  if (NO_ERROR_BODY.test(error)) return UNKNOWN;
  return SENTENCE.test(error) ? error : UNKNOWN;
}
