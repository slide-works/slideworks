import { useEffect, useState } from 'react';
import { useAudiencePolls } from '../guest/AudiencePollsContext';
import type { PollTally } from '../polls/types';

// Component tests only: reads the poll channel ActivitySlot hands to PollAnswerLive, lists every tally it delivers in
// order (one line each: round, answered, counts) and invokes WatchPoll from a button, showing how it ended.
export function AudiencePollsProbe() {
  const polls = useAudiencePolls()!;
  const [tallies, setTallies] = useState<PollTally[]>([]);
  const [watch, setWatch] = useState('');

  useEffect(() => polls.onTally((tally) => setTallies((held) => [...held, tally])), [polls]);

  return (
    <div>
      <ol data-testid="probe-tallies">
        {tallies.map((tally, i) => (
          <li key={i}>{`${tally.round} ${tally.answered} ${JSON.stringify(tally.counts)}`}</li>
        ))}
      </ol>
      <button
        type="button"
        data-testid="probe-watch"
        onClick={() =>
          polls.watchPoll().then(
            () => setWatch('watching'),
            (error: Error) => setWatch(error.message),
          )
        }
      >
        Watch poll
      </button>
      <p data-testid="probe-watch-result">{watch}</p>
    </div>
  );
}
