import { useEffect, useState } from 'react';
import type { AudienceStatus } from './useAudience';
import type { AudienceState } from './audienceEvents';
import { GuestEnded } from './GuestEnded';
import { GuestFollowing } from './GuestFollowing';
import { GuestJoined } from './GuestJoined';
import { GuestLobby } from './GuestLobby';
import { GuestOffline } from './GuestOffline';
import type { GuestView } from './guestView';
import { PrivacySheet } from './PrivacySheet';
import { ReconnectingBanner } from './ReconnectingBanner';
import { GUEST_TEST_IDS } from './testIds';

// How long "You're in" shows after the first live state.
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
  onRetry: () => void;
}) {
  const [privacyOpen, setPrivacyOpen] = useState(false);
  const [joinedDone, setJoinedDone] = useState(false);
  const live = state.phase === 'live';

  useEffect(() => {
    if (!live) return;
    const timer = setTimeout(() => setJoinedDone(true), JOINED_MS);
    return () => clearTimeout(timer);
  }, [live]);

  let screen;
  if (status === 'offline') screen = <GuestOffline onRetry={onRetry} />;
  else if (state.phase === 'lobby') screen = <GuestLobby view={view} />;
  else if (state.phase === 'ended') screen = <GuestEnded view={view} />;
  else if (!joinedDone) screen = <GuestJoined view={view} />;
  else screen = <GuestFollowing view={view} state={state} />;

  return (
    <>
      {screen}
      {status === 'reconnecting' && <ReconnectingBanner />}
      <footer>
        <button
          type="button"
          data-testid={GUEST_TEST_IDS.privacyOpen}
          onClick={() => setPrivacyOpen(true)}
        >
          Guest · Privacy
        </button>
      </footer>
      {privacyOpen && <PrivacySheet onClose={() => setPrivacyOpen(false)} />}
    </>
  );
}
