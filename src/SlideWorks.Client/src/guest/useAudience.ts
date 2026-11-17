import { useEffect, useState } from 'react';
import { api } from '../api/client';
import { createAudienceConnection } from '../realtime/audienceConnection';
import { AUDIENCE_EVENT_METHOD, isAudienceEvent, type AudienceState } from './audienceEvents';
import { NO_SESSION, TOO_MANY_ATTEMPTS, type GuestView } from './guestView';

export type AudienceStatus = 'connecting' | 'live' | 'reconnecting' | 'offline';

export type Audience = { view: GuestView; state: AudienceState; status: AudienceStatus };

// The text the real hub's error completion ends with when JoinAudience throws HubException(message) (J4).
const hubRefusal = (error: unknown, message: string) =>
  error instanceof Error && error.message.endsWith(`HubException: ${message}`);

// The guest's realtime state (SW-5 J5). initial is the view GET /api/join/{code} returned; the hook returns the view the
// screens show, which it re-reads when the state's revision changes. It joins the audience on every connection (the first
// and each reconnect) and lands on the state that answers, never replaying what it missed. A refusal the contract names
// (no_session, too_many_attempts) and a connection that has used up its retries both report offline; any other failure
// is thrown to the page's error boundary.
export function useAudience(code: string, initial: GuestView): Audience {
  const [view, setView] = useState(initial);
  const [state, setState] = useState(initial.state);
  const [status, setStatus] = useState<AudienceStatus>('connecting');
  const [failure, setFailure] = useState<unknown>(null);

  // A new code or a new GET (the page's retry) starts again from the view it was given.
  const [seenInitial, setSeenInitial] = useState(initial);
  if (seenInitial !== initial) {
    setSeenInitial(initial);
    setView(initial);
    setState(initial.state);
    setStatus('connecting');
  }

  useEffect(() => {
    let active = true;
    let lastSeq = initial.state.seq;
    let viewRevision = initial.state.revision;
    let reading: number | null = null;
    const connection = createAudienceConnection();

    const offline = () => active && setStatus('offline');

    const reread = (revision: number) => {
      if (reading === revision || viewRevision === revision) return;
      reading = revision;
      api.get<GuestView>(`/api/join/${encodeURIComponent(code)}`).then(
        (next) => {
          if (!active) return;
          viewRevision = next.state.revision;
          setView(next);
        },
        // A refused or failed read (no_session, too_many_attempts, the network) leaves the phone unable to follow.
        offline,
      );
    };

    // A state from the hub is applied only when it is newer than the last one.
    const apply = (next: AudienceState) => {
      if (!active || next.seq <= lastSeq) return;
      lastSeq = next.seq;
      setState(next);
      reread(next.revision);
    };

    // The answer to a (re)join is where the guest lands, whatever its seq: it is the server's current state.
    const land = (next: AudienceState) => {
      if (!active) return;
      lastSeq = next.seq;
      setState(next);
      setStatus('live');
      reread(next.revision);
    };

    const join = () =>
      connection.invoke<AudienceState>('JoinAudience', code).then(land, (error: unknown) => {
        if (hubRefusal(error, NO_SESSION) || hubRefusal(error, TOO_MANY_ATTEMPTS)) offline();
        else if (active) setFailure(error);
      });

    connection.on(AUDIENCE_EVENT_METHOD, (event: unknown) => {
      if (!isAudienceEvent(event)) return;
      if (event.type === 'audienceState') apply(event);
      else if (event.type === 'audienceEnded' && active) {
        setState((current) => ({ ...current, phase: 'ended', current: null }));
      }
    });
    connection.onreconnecting(() => active && setStatus('reconnecting'));
    connection.onreconnected(() => void join());
    connection.onclose(offline);
    connection.start().then(join, offline);

    return () => {
      active = false;
      void connection.stop();
    };
  }, [code, initial]);

  if (failure) throw failure;
  return { view, state, status };
}
