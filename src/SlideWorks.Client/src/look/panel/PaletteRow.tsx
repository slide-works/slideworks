import type { DeckPageLook, LooksView } from '../../api/types';
import { contrastMessage } from './contrastMessage';

export type PaletteRowProps = {
  look: DeckPageLook;
  looks: LooksView | null;
  // The palette the deck's template locks it to, or null.
  lockedBy: string | null;
  disabled: boolean;
  onChange(palette: string): void;
};

// The palette select, its swatch bar and the contrast check. A template that
// locks its palette disables the select.
export function PaletteRow({ look, looks, lockedBy, disabled, onChange }: PaletteRowProps) {
  const entry = looks?.palettes.find((palette) => palette.name === look.palette.name);
  return (
    <div className="grid gap-1 text-meta">
      <label className="grid gap-1">
        <span className="font-semibold text-ink-muted">Palette</span>
        <select
          data-testid="look-palette"
          className="field"
          value={look.palette.name}
          disabled={disabled || looks === null || lockedBy !== null}
          onChange={(event) => onChange(event.target.value)}
        >
          {looks ? (
            looks.palettes.map((palette) => (
              <option key={palette.name} value={palette.name}>
                {palette.label}
              </option>
            ))
          ) : (
            <option value={look.palette.name}>{look.palette.label}</option>
          )}
        </select>
      </label>
      {lockedBy !== null && (
        <span data-testid="look-palette-locked" className="text-ink-muted">
          The {lockedBy} template sets the palette.
        </span>
      )}
      {entry && (
        <div
          data-testid="look-swatch"
          aria-hidden="true"
          className="flex h-4 overflow-hidden rounded"
        >
          {entry.swatch.map((colour, index) => (
            <span key={index} className="flex-1" style={{ background: colour }} />
          ))}
        </div>
      )}
      <span
        data-testid="look-contrast"
        data-pass={look.contrast.pass}
        className={look.contrast.pass ? 'text-ok' : 'text-bad'}
      >
        {contrastMessage(look.contrast)}
      </span>
    </div>
  );
}
