import { useMemo, type ReactNode } from 'react';
import { ScreenReactionsContext, type ScreenBurst } from './useScreenReactions';

// Tests only: provides the big screen's reactions. Add a burst by rendering again with a longer list (a component
// test's update()). null is "no provider". Frozen after wave 0.2.
export function ScreenReactionsFixture({
  bursts,
  children,
}: {
  bursts: ScreenBurst[] | null;
  children: ReactNode;
}) {
  const value = useMemo(() => (bursts === null ? null : { bursts }), [bursts]);
  return <ScreenReactionsContext value={value}>{children}</ScreenReactionsContext>;
}
