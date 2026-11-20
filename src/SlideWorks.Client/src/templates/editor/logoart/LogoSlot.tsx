import type { TemplateAsset } from '../../types';
import { ImagePicker } from './ImagePicker';

export type LogoSide = 'light' | 'dark';

const TEXT: Record<LogoSide, { legend: string; upload: string; replace: string; remove: string }> =
  {
    light: {
      legend: 'Logo on light slides',
      upload: 'Upload logo on light slides',
      replace: 'Replace logo on light slides',
      remove: 'Remove the light logo',
    },
    dark: {
      legend: 'Logo on dark slides',
      upload: 'Upload logo on dark slides',
      replace: 'Replace logo on dark slides',
      remove: 'Remove the dark logo',
    },
  };

type Props = {
  id: string;
  side: LogoSide;
  asset: TemplateAsset | undefined;
  busy: boolean;
  // A refused upload is an alert; a refused save is text beside the control.
  uploadError: string | null;
  saveError: string | null;
  onPick(file: File): void;
  onRemove(): void;
};

// One logo: a preview on the surface it is for, its picker, and Remove.
export function LogoSlot({
  id,
  side,
  asset,
  busy,
  uploadError,
  saveError,
  onPick,
  onRemove,
}: Props) {
  const text = TEXT[side];
  const errorId = `${id}-${side}-error`;
  const described = uploadError !== null || saveError !== null ? errorId : undefined;
  return (
    <fieldset className="grid gap-2">
      <legend className="text-meta font-semibold text-ink-muted">{text.legend}</legend>
      <div
        className={`flex h-20 items-center justify-center rounded-panel border border-edge p-2 ${
          side === 'dark' ? 'bg-ink' : 'bg-panel'
        }`}
      >
        {asset ? (
          <img
            src={asset.url}
            alt={`${text.legend}: ${asset.fileName}`}
            className="max-h-full max-w-full object-contain"
          />
        ) : (
          <span className={`text-meta ${side === 'dark' ? 'text-white' : 'text-ink-muted'}`}>
            No logo
          </span>
        )}
      </div>
      {asset ? <p className="text-meta text-ink-muted">{asset.fileName}</p> : null}
      <div className="flex flex-wrap gap-2">
        <ImagePicker
          id={`${id}-${side}`}
          label={asset ? text.replace : text.upload}
          busy={busy}
          describedBy={described}
          onPick={onPick}
        />
        {asset ? (
          <button
            id={`${id}-${side}-remove`}
            type="button"
            className="btn-ghost"
            onClick={onRemove}
          >
            {text.remove}
          </button>
        ) : null}
      </div>
      {uploadError === null ? null : (
        <p id={errorId} role="alert" className="text-meta text-bad">
          {uploadError}
        </p>
      )}
      {saveError === null ? null : (
        <p id={errorId} className="text-meta text-bad">
          {saveError}
        </p>
      )}
    </fieldset>
  );
}
