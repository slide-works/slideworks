import { GuestScreens } from './GuestScreens';
import type { GuestView } from './guestView';
import { useAudience } from './useAudience';

// A loaded guest view with its realtime state, which starts from the state the GET returned.
export function GuestLive({
  code,
  view,
  onRetry,
}: {
  code: string;
  view: GuestView;
  onRetry: () => void;
}) {
  const { state, status } = useAudience(code, view.state);
  return <GuestScreens view={view} state={state} status={status} onRetry={onRetry} />;
}
