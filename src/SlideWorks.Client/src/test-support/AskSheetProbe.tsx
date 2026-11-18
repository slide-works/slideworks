import { useAskSheet } from '../qa/guest/AskSheetContext';

// Component tests only: renders useAskSheet() as plain text, one test id per value, with a control per action, so the
// tabs' sheet value can be tested without the sheet itself. The controls may sit in a hidden tab panel: click them with
// dispatchEvent.
export function AskSheetProbe() {
  const sheet = useAskSheet();
  return (
    <div>
      <p data-testid="probe-sheet-open">{String(sheet.open)}</p>
      <p data-testid="probe-poll-just-opened">{String(sheet.pollJustOpened)}</p>
      <button type="button" data-testid="probe-sheet-show" onClick={sheet.show}>
        Show
      </button>
      <button type="button" data-testid="probe-sheet-close" onClick={sheet.close}>
        Close
      </button>
      <button type="button" data-testid="probe-sheet-go-to-poll" onClick={sheet.goToPoll}>
        Go to poll
      </button>
    </div>
  );
}
