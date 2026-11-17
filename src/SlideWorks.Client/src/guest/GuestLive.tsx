import { GuestErrorBoundary } from './GuestErrorBoundary';
import { GuestScreens } from './GuestScreens';
import type { GuestView } from './guestView';
import { useAudience } from './useAudience';
import type { Retry } from './useRetry';

function Screens({ code, view: initial, onRetry }: Props) {
  const { view, state, status } = useAudience(code, initial);
  return <GuestScreens view={view} state={state} status={status} onRetry={onRetry} />;
}

type Props = { code: string; view: GuestView; onRetry: Retry };

// A loaded guest view with its realtime state. The hook starts from the view the GET returned and hands back the view to
// show, so a re-read inside it reaches the screens. An unexpected failure in the hook lands on the boundary.
export function GuestLive(props: Props) {
  return (
    <GuestErrorBoundary onRetry={props.onRetry}>
      <Screens {...props} />
    </GuestErrorBoundary>
  );
}
