import { useMemo, type ReactNode } from 'react';
import type { ReactionEmoji } from '../types';
import { PhoneReactionsContext, type PhoneReactions } from './usePhoneReactions';

export type PhoneReactionsFixtureProps = {
  // Defaults: enabled, not cooling, no error.
  enabled?: boolean;
  cooling?: boolean;
  error?: string | null;
  // Called with each tap, so a spec sees what the row sent.
  onTap?: (emoji: ReactionEmoji) => void;
  children: ReactNode;
};

// Component tests only: gives the reaction row a PhoneReactions value in whatever state the spec names, with no network.
// tap resolves at once and changes nothing.
export function PhoneReactionsFixture({
  enabled = true,
  cooling = false,
  error = null,
  onTap,
  children,
}: PhoneReactionsFixtureProps) {
  const value = useMemo<PhoneReactions>(
    () => ({ enabled, cooling, error, tap: async (emoji) => onTap?.(emoji) }),
    [enabled, cooling, error, onTap],
  );
  return <PhoneReactionsContext value={value}>{children}</PhoneReactionsContext>;
}
