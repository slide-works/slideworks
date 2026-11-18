type Props = {
  label: string;
  // The slide's switch: true is on (the default for a slide nothing was switched off on).
  on: boolean;
  // Why the switch can't be used, when the deck has that feature off; null when it can.
  reason: string | null;
  onChange(on: boolean): void;
  testId: string;
  reasonTestId: string;
  // Text colours for the surface it sits on.
  labelClass: string;
  reasonClass: string;
};

// One of a slide's two switches (SW-9 Q1: a switch can only turn off what the deck has on), for the settings strip and
// the deck page's card. A switch whose feature the deck has off is disabled and says why.
export function QaSlideSwitch({
  label,
  on,
  reason,
  onChange,
  testId,
  reasonTestId,
  labelClass,
  reasonClass,
}: Props) {
  return (
    <span className="inline-flex flex-wrap items-center gap-x-2">
      <label className={`inline-flex items-center gap-1.5 ${labelClass}`}>
        <input
          data-testid={testId}
          type="checkbox"
          checked={on}
          disabled={reason !== null}
          aria-describedby={reason === null ? undefined : reasonTestId}
          onChange={(event) => onChange(event.target.checked)}
        />
        {label}
      </label>
      {reason !== null && (
        <span id={reasonTestId} data-testid={reasonTestId} className={reasonClass}>
          {reason}
        </span>
      )}
    </span>
  );
}
