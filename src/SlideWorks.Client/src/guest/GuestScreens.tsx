import { useEffect, useState } from 'react';
import type { AudienceStatus } from './useAudience';
import type { AudienceState } from './audienceEvents';
import { GuestEnded } from './GuestEnded';
import { GuestFollowing } from './GuestFollowing';
import { GuestJoined } from './GuestJoined';
import { GuestLobby } from './GuestLobby';
import { GuestNeedsCookies } from './GuestNeedsCookies';
import { GuestOffline } from './GuestOffline';
import { GuestShell } from './GuestShell';
import type { GuestView } from './guestView';
import type { Retry } from './useRetry';
import { ReconnectingBanner } from './ReconnectingBanner';

// How long "You're in" shows after the first live state. A guest still on the page when a second session starts doesn't
// see it again: joinedDone is never reset.
const JOINED_MS = 2000;

// Picks the one screen for the phase and the connection status, and lays the reconnecting banner and the privacy sheet
// over it. The phase comes from the state; this never works it out.
export function GuestScreens({
  view,
  state,
  status,
  onRetry,
}: {
  view: GuestView;
  state: AudienceState;
  status: AudienceStatus;
  onRetry: Retry;
}) {
  const [joinedDone, setJoinedDone] = useState(false);
  const live = state.phase === 'live';
  const ended = state.phase === 'ended';

  // Once offline, the offline screen stays up through 'connecting' (a successful re-read puts the status back there) until
  // the hub is live again, so its backoff keeps growing and its focus isn't taken every few seconds. A talk that has ended
  // wins over all of it: the thank-you screen is true whatever the network does.
  const [heldOffline, setHeldOffline] = useState(false);
  if (status === 'offline' && !heldOffline) setHeldOffline(true);
  else if ((status === 'live' || status === 'needs_cookies') && heldOffline) setHeldOffline(false);
  const offline = !ended && (status === 'offline' || (heldOffline && status === 'connecting'));
  const needsCookies = !ended && status === 'needs_cookies';
  const following = !offline && !needsCookies && live && joinedDone;

  useEffect(() => {
    if (!live) return;
    const timer = setTimeout(() => setJoinedDone(true), JOINED_MS);
    return () => clearTimeout(timer);
  }, [live]);

  let screen;
  if (ended) screen = <GuestEnded view={view} />;
  else if (needsCookies) screen = <GuestNeedsCookies onRetry={onRetry} />;
  else if (offline) screen = <GuestOffline onRetry={onRetry} />;
  else if (state.phase === 'lobby') screen = <GuestLobby view={view} />;
  else if (!joinedDone) screen = <GuestJoined view={view} />;
  else screen = <GuestFollowing view={view} state={state} status={status} />;

  return (
    <GuestShell fill={following}>
      {screen}
      {status === 'reconnecting' && !ended && <ReconnectingBanner />}
    </GuestShell>
  );
}
