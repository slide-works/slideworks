import type { TemplateAsset, TemplateManifestFont } from '../../types';

type Props = {
  font: TemplateManifestFont;
  asset: TemplateAsset | undefined;
  onRemove(): void;
};

// One font file the template uses: its file name, family, role, weight, style and measured width.
export function FontFileRow({ font, asset, onRemove }: Props) {
  const fileName = asset?.fileName ?? font.file;
  const width = asset?.meta.avgCharEm;
  return (
    <li className="flex flex-wrap items-center gap-x-3 gap-y-1 rounded-panel border border-edge p-2 text-meta">
      <span className="font-medium text-ink">{fileName}</span>
      <span className="text-ink-muted">
        {font.family}, {font.role}, weight {font.weight}, {font.style}
        {width === undefined ? '' : `, ${width.toFixed(2)} em`}
      </span>
      <button
        type="button"
        className="btn-ghost ml-auto"
        data-font-remove
        aria-label={`Remove ${fileName}`}
        onClick={onRemove}
      >
        Remove
      </button>
    </li>
  );
}
