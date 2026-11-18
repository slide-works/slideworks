import type { TemplateAsset } from '../../types';
import { ImagePicker } from './ImagePicker';

export type ArtChoice = 'none' | 'waves' | 'image';

const CHOICES: { choice: ArtChoice; label: string }[] = [
  { choice: 'none', label: 'None' },
  { choice: 'waves', label: 'Waves' },
  { choice: 'image', label: 'An image' },
];

type Props = {
  id: string;
  // The choice on show; null when the manifest names a generator this editor has no choice for.
  shown: ArtChoice | null;
  image: TemplateAsset | undefined;
  busy: boolean;
  uploadError: string | null;
  saveError: string | null;
  onChoose(choice: ArtChoice): void;
  onPick(file: File): void;
};

// The title art: none, the built-in waves, or an uploaded image with its preview and picker.
export function TitleArtChoice({
  id,
  shown,
  image,
  busy,
  uploadError,
  saveError,
  onChoose,
  onPick,
}: Props) {
  const errorId = `${id}-art-error`;
  const described = uploadError !== null || saveError !== null ? errorId : undefined;
  return (
    <fieldset className="grid gap-2">
      <legend className="text-meta font-semibold text-ink-muted">Title art</legend>
      <div className="flex flex-wrap gap-4 text-meta">
        {CHOICES.map(({ choice, label }) => (
          <label key={choice}>
            <input
              id={choice === 'none' ? `${id}-art` : undefined}
              type="radio"
              name={`${id}-art-kind`}
              checked={shown === choice}
              aria-describedby={described}
              onChange={() => onChoose(choice)}
            />{' '}
            {label}
          </label>
        ))}
      </div>
      {shown === 'image' ? (
        <div className="grid gap-2">
          {image ? (
            <div className="flex h-24 items-center justify-center rounded-panel border border-edge bg-ink p-2">
              <img
                src={image.url}
                alt={`Title art: ${image.fileName}`}
                className="max-h-full max-w-full object-contain"
              />
            </div>
          ) : null}
          <div className="flex flex-wrap items-center gap-2">
            <ImagePicker
              id={`${id}-art-upload`}
              label={image ? 'Replace title art image' : 'Upload title art image'}
              busy={busy}
              describedBy={described}
              onPick={onPick}
            />
            {image ? <span className="text-meta text-ink-muted">{image.fileName}</span> : null}
          </div>
        </div>
      ) : null}
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
