import { useMemo, type ReactNode } from 'react';
import { AskSheetContext, type AskSheet } from './AskSheetContext';

export type AskSheetFixtureProps = {
  // Defaults: closed, no poll just opened.
  open?: boolean;
  pollJustOpened?: boolean;
  // Called with each show, close and goToPoll, so a spec sees what the parts asked for.
  onShow?: () => void;
  onClose?: () => void;
  onGoToPoll?: () => void;
  children: ReactNode;
};

// Component tests only: gives the parts an AskSheet with every value set by props. Nothing moves on a call: it is only
// recorded, and the spec mounts again with other props.
export function AskSheetFixture({
  open = false,
  pollJustOpened = false,
  onShow,
  onClose,
  onGoToPoll,
  children,
}: AskSheetFixtureProps) {
  const value = useMemo<AskSheet>(
    () => ({
      open,
      pollJustOpened,
      show: () => onShow?.(),
      close: () => onClose?.(),
      goToPoll: () => onGoToPoll?.(),
    }),
    [open, pollJustOpened, onShow, onClose, onGoToPoll],
  );
  return <AskSheetContext value={value}>{children}</AskSheetContext>;
}
