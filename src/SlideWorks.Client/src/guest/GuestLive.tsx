import { GuestScreens } from './GuestScreens';
import type { GuestView } from './guestView';
import { useAudience } from './useAudience';

// A loaded guest view with its realtime state. The hook starts from the view the GET returned and hands back the view to
// show, so a re-read inside it reaches the screens.
export function GuestLive({
  code,
  view: initial,
  onRetry,
}: {
  code: string;
  view: GuestView;
  onRetry: () => void;
}) {
  const { view, state, status } = useAudience(code, initial);
  return <GuestScreens view={view} state={state} status={status} onRetry={onRetry} />;
}
