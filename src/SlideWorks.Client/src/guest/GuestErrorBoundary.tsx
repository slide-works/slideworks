import { Component, type ErrorInfo, type ReactNode } from 'react';
import { GuestShell } from './GuestShell';
import { GUEST_TEST_IDS } from './testIds';

type Props = { onRetry: () => void; children: ReactNode };

// Catches what the guest's realtime hook throws (a JoinAudience refusal the contract doesn't name), so the phone shows a
// message it can recover from instead of a blank page. Retry clears the error, remounts the screens and reads the view again.
export class GuestErrorBoundary extends Component<Props, { failed: boolean }> {
  state = { failed: false };

  static getDerivedStateFromError() {
    return { failed: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Guest page failed', error, info.componentStack);
  }

  private retry = () => {
    this.setState({ failed: false });
    this.props.onRetry();
  };

  render() {
    if (!this.state.failed) return this.props.children;
    return (
      <GuestShell>
        <main
          data-testid={GUEST_TEST_IDS.error}
          role="alert"
          className="flex flex-1 flex-col items-center justify-center gap-3.5 px-6 text-center"
        >
          <h1 className="font-head text-[22px] leading-tight font-bold">Couldn't join the talk</h1>
          <p className="text-base text-(--sw-night-2)">
            Something went wrong on our side. Try again in a moment.
          </p>
          <button
            type="button"
            data-testid={GUEST_TEST_IDS.errorRetry}
            aria-label="Try again to join the talk"
            onClick={this.retry}
            className="mt-3 min-h-12 w-full rounded-xl bg-white/10 px-4 text-base font-bold text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--sw-accent-glow)"
          >
            Try again
          </button>
        </main>
      </GuestShell>
    );
  }
}
