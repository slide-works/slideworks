import type { TopBar } from '../../api/look';
import type { ChromeOverrides, FooterMode, TemplateChrome } from '../../api/types';

export type ChromeRowProps = {
  // The effective chrome, after the deck's overrides.
  chrome: TemplateChrome;
  // The look's top bar (SW-50 D13).
  topBar?: TopBar;
  disabled: boolean;
  onChange(chrome: ChromeOverrides): void;
};

const FOOTERS: { value: FooterMode; label: string }[] = [
  { value: 'author', label: 'Author' },
  { value: 'date', label: 'Date' },
  { value: 'off', label: 'Off' },
];

// The deck's chrome over its template's (D17): the part tracker, the footer,
// the progress bar and the top accent bar. Each change sends that one key.
export function ChromeRow({ chrome, topBar, disabled, onChange }: ChromeRowProps) {
  return (
    <fieldset className="grid gap-1 text-meta">
      <legend className="font-semibold text-ink-muted">Chrome</legend>
      <label>
        <input
          data-testid="look-tracker"
          type="checkbox"
          checked={chrome.tracker}
          disabled={disabled}
          onChange={(event) => onChange({ tracker: event.target.checked })}
        />{' '}
        Part tracker
      </label>
      <label>
        <input
          data-testid="look-progress"
          type="checkbox"
          checked={chrome.progress}
          disabled={disabled}
          onChange={(event) => onChange({ progress: event.target.checked })}
        />{' '}
        Progress bar
      </label>
      <label>
        <input
          data-testid="look-top-bar"
          type="checkbox"
          checked={topBar === 'accent'}
          disabled={disabled}
          onChange={(event) => onChange({ topBar: event.target.checked ? 'accent' : 'none' })}
        />{' '}
        Accent bar across the top
      </label>
      <label className="grid gap-1">
        Footer
        <select
          data-testid="look-footer"
          className="field"
          value={chrome.footer}
          disabled={disabled}
          onChange={(event) => onChange({ footer: event.target.value as FooterMode })}
        >
          {FOOTERS.map((footer) => (
            <option key={footer.value} value={footer.value}>
              {footer.label}
            </option>
          ))}
        </select>
      </label>
    </fieldset>
  );
}
