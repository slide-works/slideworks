import { useEffect, useState } from 'react';
import { AUDIENCE_EVENT_METHOD, type AudienceState } from '../guest/audienceEvents';
import { createAudienceConnection } from '../realtime/audienceConnection';

// Component tests only: opens the guest connection the way useAudience will (start, JoinAudience, listen for
// audienceEvent) and shows what came back, so a spec can check each transport.
export function AudienceProbe({ code }: { code: string }) {
  const [seq, setSeq] = useState<number | null>(null);
  const [error, setError] = useState('');

  useEffect(() => {
    const connection = createAudienceConnection();
    connection.on(AUDIENCE_EVENT_METHOD, (event: AudienceState) => setSeq(event.seq));
    connection
      .start()
      .then(() => connection.invoke<AudienceState>('JoinAudience', code))
      .then((state) => setSeq(state.seq))
      .catch((failure: Error) => setError(failure.message));
    return () => void connection.stop();
  }, [code]);

  return (
    <div>
      <p data-testid="probe-state">{seq === null ? '' : `seq ${seq}`}</p>
      <p data-testid="probe-error">{error}</p>
    </div>
  );
}
