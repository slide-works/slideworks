import { useId, useState } from 'react';
import type { ChangeOutcome } from '../../useTemplateDraft';
import type { TemplateAsset, TemplateFontRole, TemplateManifestFont } from '../../types';

const ROLES: TemplateFontRole[] = ['head', 'body', 'mono'];
const STYLES: TemplateManifestFont['style'][] = ['normal', 'italic'];
const WHOLE_NUMBER = /^\d+$/;
const NOT_A_WEIGHT = 'Weight is a whole number, such as 400.';

type Field = 'role' | 'family' | 'weight' | 'style';

type Props = {
  asset: TemplateAsset;
  // The family the template names for a role: the family a new file of that role starts with.
  familyOf(role: TemplateFontRole): string;
  onAdd(font: TemplateManifestFont): Promise<ChangeOutcome>;
};

// A stored font file the manifest does not use yet: it needs a role, a family, a weight and a style
// before it is added, with its asset id as the file.
export function FontToAddRow({ asset, familyOf, onAdd }: Props) {
  const id = useId();
  const [role, setRole] = useState<TemplateFontRole>('head');
  const [typedFamily, setTypedFamily] = useState<string | null>(null);
  const [weight, setWeight] = useState('400');
  const [style, setStyle] = useState<TemplateManifestFont['style']>('normal');
  const [error, setError] = useState<{ field: Field | 'add'; message: string } | null>(null);
  const family = typedFamily ?? familyOf(role);
  const width = asset.meta.avgCharEm;

  const fail = (field: Field | 'add', message: string) => {
    setError({ field, message });
    document.getElementById(`${id}-${field}`)?.focus();
  };

  async function add() {
    setError(null);
    if (!WHOLE_NUMBER.test(weight)) {
      fail('weight', NOT_A_WEIGHT);
      return;
    }
    const outcome = await onAdd({
      role,
      family,
      file: asset.assetId,
      weight: Number(weight),
      style,
    });
    if (outcome.ok || outcome.code === null || outcome.code === 'stale_revision') return;
    const named = /\.(role|family|weight|style)$/.exec(outcome.path ?? '')?.[1] as
      Field | undefined;
    fail(named ?? 'add', outcome.message);
  }

  const described = (field: Field | 'add') => (error?.field === field ? `${id}-error` : undefined);
  const invalid = (field: Field | 'add') => (error?.field === field ? true : undefined);
  const label = 'text-meta font-semibold text-ink-muted';

  return (
    <li className="grid gap-2 rounded-panel border border-edge p-2 text-meta">
      <p className="text-ink">
        <span className="font-medium">{asset.fileName}</span>
        {width === undefined ? null : (
          <span className="text-ink-muted">, {width.toFixed(2)} em</span>
        )}
      </p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="grid gap-1">
          <label htmlFor={`${id}-role`} className={label}>
            Role of {asset.fileName}
          </label>
          <select
            id={`${id}-role`}
            className="field"
            value={role}
            aria-invalid={invalid('role')}
            aria-describedby={described('role')}
            onChange={(event) => setRole(event.target.value as TemplateFontRole)}
          >
            {ROLES.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </div>
        <div className="grid gap-1">
          <label htmlFor={`${id}-family`} className={label}>
            Family of {asset.fileName}
          </label>
          <input
            id={`${id}-family`}
            type="text"
            className="field"
            value={family}
            aria-invalid={invalid('family')}
            aria-describedby={described('family')}
            onChange={(event) => setTypedFamily(event.target.value)}
          />
        </div>
        <div className="grid gap-1">
          <label htmlFor={`${id}-weight`} className={label}>
            Weight of {asset.fileName}
          </label>
          <input
            id={`${id}-weight`}
            type="text"
            inputMode="numeric"
            className="field w-24"
            value={weight}
            aria-invalid={invalid('weight')}
            aria-describedby={described('weight')}
            onChange={(event) => setWeight(event.target.value)}
          />
        </div>
        <div className="grid gap-1">
          <label htmlFor={`${id}-style`} className={label}>
            Style of {asset.fileName}
          </label>
          <select
            id={`${id}-style`}
            className="field"
            value={style}
            aria-invalid={invalid('style')}
            aria-describedby={described('style')}
            onChange={(event) => setStyle(event.target.value as TemplateManifestFont['style'])}
          >
            {STYLES.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </div>
        <button
          id={`${id}-add`}
          type="button"
          className="btn-primary"
          aria-label={`Add ${asset.fileName} to the template`}
          aria-describedby={described('add')}
          onClick={() => void add()}
        >
          Add to template
        </button>
      </div>
      {error === null ? null : (
        <p id={`${id}-error`} role="alert" className="text-bad">
          {error.message}
        </p>
      )}
    </li>
  );
}
