import { useState } from 'react';
import type { PollActivity } from '../types';
import { POLL_CARD_TEST_IDS } from '../testIds';
import { putPollAnswer } from './pollApi';

const MAX_TEXT = 200;

// A free-text or word-cloud question on a guest's phone. The cloud is drawn on the big screen; the phone sends the words.
export function OpenAnswerCard({
  activity,
  presenter,
  code,
}: {
  activity: PollActivity;
  presenter: string;
  code: string;
}) {
  const [text, setText] = useState('');
  const [revision, setRevision] = useState(0);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const open = activity.phase === 'open' && activity.round !== null;
  const kind = activity.answers === 'cloud' ? 'Word cloud' : 'Open question';

  const send = async () => {
    const answer = text.trim();
    if (!open || activity.round === null || answer.length === 0 || busy) return;
    setBusy(true);
    setError(null);
    try {
      const outcome = await putPollAnswer(code, {
        round: activity.round,
        picks: [],
        revision,
        text: answer,
      });
      if (outcome.type === 'accepted') {
        setRevision(outcome.answer.revision);
        setSent(true);
      } else {
        setRevision(outcome.current.mine?.revision ?? revision);
        setError('That answer was not saved. Try again.');
      }
    } catch {
      setError('That answer was not saved. Try again.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-2">
      <article
        data-testid={POLL_CARD_TEST_IDS.card}
        className="flex min-h-0 flex-1 flex-col gap-3 rounded-[14px] bg-white p-4 text-[#0f172a] shadow-[0_10px_30px_rgb(0_0_0/.25)]"
      >
        <header className="flex shrink-0 flex-col gap-2">
          <p
            data-testid={POLL_CARD_TEST_IDS.kind}
            className="w-fit rounded-[4px] bg-[#eef2ff] px-1.5 py-0.5 text-[10px] font-bold tracking-[.08em] text-[#3730a3] uppercase"
          >
            {kind}
          </p>
          <h2
            data-testid={POLL_CARD_TEST_IDS.question}
            className="font-head text-[17px] leading-snug font-bold"
          >
            {activity.question}
          </h2>
        </header>
        <label className="flex min-h-0 flex-1 flex-col gap-2 text-[13px]">
          Your answer
          <textarea
            data-testid="poll-open-text"
            className="min-h-24 flex-1 resize-none rounded-[10px] border border-[#cbd5e1] p-3 text-[16px]"
            maxLength={MAX_TEXT}
            value={text}
            disabled={!open || busy}
            onChange={(event) => setText(event.target.value)}
          />
        </label>
        <button
          type="button"
          data-testid="poll-open-send"
          className="rounded-[10px] bg-[#3730a3] px-3 py-2 text-[15px] font-bold text-white disabled:opacity-50"
          disabled={!open || busy || text.trim().length === 0}
          onClick={() => void send()}
        >
          {sent ? 'Update answer' : 'Send'}
        </button>
        {sent ? <p data-testid="poll-open-sent">Answer sent</p> : null}
        {error ? <p data-testid="poll-open-error">{error}</p> : null}
      </article>
      <p
        data-testid={POLL_CARD_TEST_IDS.privacy}
        className="shrink-0 px-1 text-[11px] text-(--sw-night-muted)"
      >
        {`Your name is not shown. ${presenter} sees the words, not who wrote them.`}
      </p>
    </div>
  );
}
