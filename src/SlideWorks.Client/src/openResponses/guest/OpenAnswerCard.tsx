import { useRef, useState, type FormEvent } from 'react';
import type { AudienceStatus } from '../../guest/useAudience';
import { OPEN_RESPONSE_TEST_IDS as IDS } from '../testIds';
import { useOpenResponse } from '../useOpenResponse';

const MAX_TEXT_LENGTH = 200;

// The phone's card on an open-text slide (SW-8 D3): the question, one answer to send, the guest's stored answer and, when
// the audience event carries them, the room's answers. After a send, focus goes back to the text box.
export function OpenAnswerCard({
  code,
  slide,
  status,
}: {
  code: string;
  slide: number;
  status: AudienceStatus;
}) {
  const response = useOpenResponse(code, slide, status);
  const [draft, setDraft] = useState('');
  const box = useRef<HTMLTextAreaElement>(null);
  const view = response.view?.kind === 'open-text' ? response.view : null;
  const room = response.room?.type === 'openText' ? response.room : null;

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (await response.sendText(draft)) setDraft('');
    box.current?.focus();
  };

  return (
    <section
      data-testid={IDS.answerCard}
      className="flex flex-col gap-3 rounded-[14px] bg-white/5 p-4 text-sm"
    >
      {view && (
        <form onSubmit={submit} className="flex flex-col gap-2">
          <h2 data-testid={IDS.question} className="font-head text-base font-bold">
            {view.question}
          </h2>
          <textarea
            ref={box}
            data-testid={IDS.input}
            aria-label={`${view.question} (your answer)`}
            maxLength={MAX_TEXT_LENGTH}
            value={draft}
            onChange={(change) => setDraft(change.target.value)}
            className="min-h-20 rounded-lg bg-black/30 px-3 py-2"
          />
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
      {view && view.mineText !== '' && (
        <p data-testid={IDS.mine} className="rounded-lg bg-white/10 px-3 py-2">
          {view.mineText}
        </p>
      )}
      {room && room.answers.length > 0 && (
        <ul
          data-testid={IDS.room}
          aria-label="The room's answers"
          className="flex flex-col gap-1.5"
        >
          {room.answers.map((answer) => (
            <li key={answer.number} data-testid={IDS.roomEntry}>
              {answer.text}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
