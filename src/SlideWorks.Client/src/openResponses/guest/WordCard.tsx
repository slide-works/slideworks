import { useRef, useState, type FormEvent } from 'react';
import type { AudienceStatus } from '../../guest/useAudience';
import { OPEN_RESPONSE_TEST_IDS as IDS } from '../testIds';
import { useOpenResponse } from '../useOpenResponse';

const SLOTS = [0, 1, 2] as const;
const MAX_WORD_LENGTH = 25;

// The phone's card on a word-cloud slide (SW-8 D2, D3): the question, up to three words to send, the guest's stored words
// and, when the audience event carries them, the room's words. After a send, focus goes back to the first box.
export function WordCard({
  code,
  slide,
  status,
}: {
  code: string;
  slide: number;
  status: AudienceStatus;
}) {
  const response = useOpenResponse(code, slide, status);
  const [drafts, setDrafts] = useState<readonly string[]>(['', '', '']);
  const first = useRef<HTMLInputElement>(null);
  const view = response.view?.kind === 'word-cloud' ? response.view : null;
  const room = response.room?.type === 'wordCloud' ? response.room : null;

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const sent = await response.sendWords(drafts.filter((word) => word.trim() !== ''));
    if (sent) setDrafts(['', '', '']);
    first.current?.focus();
  };

  return (
    <section
      data-testid={IDS.wordCard}
      className="flex flex-col gap-3 rounded-[14px] bg-white/5 p-4 text-sm"
    >
      {view && (
        <form onSubmit={submit} className="flex flex-col gap-2">
          <h2 data-testid={IDS.question} className="font-head text-base font-bold">
            {view.question}
          </h2>
          {SLOTS.map((slot) => (
            <input
              key={slot}
              ref={slot === 0 ? first : undefined}
              data-testid={IDS.input}
              aria-label={`${view.question} (word ${slot + 1} of ${SLOTS.length})`}
              maxLength={MAX_WORD_LENGTH}
              value={drafts[slot]}
              onChange={(change) =>
                setDrafts(drafts.map((word, at) => (at === slot ? change.target.value : word)))
              }
              className="rounded-lg bg-black/30 px-3 py-2"
            />
          ))}
          <button
            type="submit"
            data-testid={IDS.submit}
            disabled={view.phase !== 'open' || response.sending}
            className="rounded-lg bg-(--sw-accent-glow) px-3 py-2 font-semibold disabled:opacity-50"
          >
            Send
          </button>
        </form>
      )}
      {response.error !== null && (
        <p data-testid={IDS.error} role="alert" className="text-[#fca5a5]">
          {response.error}
        </p>
      )}
      {view && view.mineWords.length > 0 && (
        <ul data-testid={IDS.mine} aria-label="Your words" className="flex flex-wrap gap-1.5">
          {view.mineWords.map((word) => (
            <li key={word} data-testid={IDS.mineEntry} className="rounded-full bg-white/10 px-2.5">
              {word}
            </li>
          ))}
        </ul>
      )}
      {room && room.words.length > 0 && (
        <ul data-testid={IDS.room} aria-label="The room's words" className="flex flex-wrap gap-1.5">
          {room.words.map((word) => (
            <li key={word.text} data-testid={IDS.roomEntry} data-count={word.count}>
              {word.text}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
