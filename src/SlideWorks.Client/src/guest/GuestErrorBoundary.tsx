import { Component, type ErrorInfo, type ReactNode } from 'react';
import { GuestShell } from './GuestShell';
import { GuestRetryButton } from './GuestRetryButton';
import { GUEST_TEST_IDS } from './testIds';
import type { Retry } from './useRetry';

type Props = { onRetry: Retry; children: ReactNode };

// Catches what the guest's realtime hook throws (a JoinAudience refusal the contract doesn't name), so the phone shows a
// message it can recover from instead of a blank page. Retry reads the view again, then clears the error and remounts the screens.
export class GuestErrorBoundary extends Component<Props, { failed: boolean }> {
  state = { failed: false };

  static getDerivedStateFromError() {
    return { failed: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Guest page failed', error, info.componentStack);
  }

  // The error stays up until the retry went through; a throttled one leaves it, with its message, for the button to retry.
  private retry = async () => {
    const outcome = await this.props.onRetry();
    if (outcome !== 'throttled') this.setState({ failed: false });
    return outcome;
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
          <GuestRetryButton onRetry={this.retry} testId={GUEST_TEST_IDS.errorRetry} />
        </main>
      </GuestShell>
    );
  }
}
