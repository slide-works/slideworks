import { editorTabTestId } from '../../testIds';
import type { EditorTabProps } from '../tabProps';

// TODO: a placeholder. The Logo and art tab of the template editor replaces it.
export function LogoArtTab({ draft }: EditorTabProps) {
  return (
    <section data-testid={editorTabTestId('logo-art')} aria-label={`Logo and art of ${draft.name}`}>
      <h2 className="text-sm font-semibold text-ink">Logo and art</h2>
    </section>
  );
}
