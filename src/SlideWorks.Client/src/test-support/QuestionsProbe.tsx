import { useState } from 'react';
import { useQuestions } from '../qa/guest/useQuestions';
import { usePhoneReactions } from '../qa/guest/usePhoneReactions';
import { REACTION_EMOJI } from '../qa/emoji';

// Component tests only: renders useQuestions() and usePhoneReactions() as plain text, one test id per value, with a
// control per action, so the real providers can be tested without any part. Asking sends the text and name boxes'
// values (an empty name box is anonymous); probe-vote-{n} toggles question n's upvote; probe-tap-{emoji} taps it.
export function QuestionsProbe({ numbers = [1, 2, 3, 4, 5, 6] }: { numbers?: number[] }) {
  const questions = useQuestions();
  const reactions = usePhoneReactions();
  const [text, setText] = useState('');
  const [name, setName] = useState('');
  return (
    <div>
      <p data-testid="probe-state">{questions.state}</p>
      <p data-testid="probe-room">{JSON.stringify(questions.room)}</p>
      <ol data-testid="probe-questions">
        {questions.questions.map((q) => (
          <li key={q.number} data-testid={`probe-question-${q.number}`}>
            {`${q.number} ${q.state} ${q.votes} ${q.yours ? 'yours' : 'theirs'} ${q.onScreen ? 'onScreen' : '-'} ${q.name ?? 'anonymous'} ${q.age} ${q.text}`}
          </li>
        ))}
      </ol>
      <p data-testid="probe-mine">{JSON.stringify(questions.mine)}</p>
      <p data-testid="probe-asking">{questions.asking}</p>
      <p data-testid="probe-retry-in">{questions.retryIn}</p>
      <p data-testid="probe-error">{questions.error ?? ''}</p>
      <p data-testid="probe-sort">{questions.sort}</p>
      <input data-testid="probe-text" value={text} onChange={(e) => setText(e.target.value)} />
      <input data-testid="probe-name" value={name} onChange={(e) => setName(e.target.value)} />
      <button
        type="button"
        data-testid="probe-ask"
        onClick={() => void questions.ask(text, name === '' ? null : name)}
      >
        Ask
      </button>
      {numbers.map((n) => (
        <button
          key={n}
          type="button"
          data-testid={`probe-vote-${n}`}
          onClick={() => questions.toggleVote(n)}
        >
          {`Vote ${n}`}
        </button>
      ))}
      <button type="button" data-testid="probe-retry" onClick={() => questions.retry()}>
        Retry
      </button>
      <button
        type="button"
        data-testid="probe-sort-toggle"
        onClick={() => questions.setSort(questions.sort === 'top' ? 'new' : 'top')}
      >
        Sort
      </button>
      <p data-testid="probe-reactions-enabled">{String(reactions.enabled)}</p>
      <p data-testid="probe-cooling">{String(reactions.cooling)}</p>
      <p data-testid="probe-reaction-error">{reactions.error ?? ''}</p>
      {REACTION_EMOJI.map((emoji) => (
        <button
          key={emoji}
          type="button"
          data-testid={`probe-tap-${emoji}`}
          onClick={() => void reactions.tap(emoji)}
        >
          {emoji}
        </button>
      ))}
    </div>
  );
}
