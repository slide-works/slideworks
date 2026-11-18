// The Templates area's screen samples (src/contracts/screens), typed as the client's views, and
// the props a spec mounts one editor tab with. The server's shape tests check its real responses
// against the same files.
import layoutReviewJson from '@contracts/screens/layout-review.json' with { type: 'json' };
import templateEditorJson from '@contracts/screens/template-editor.json' with { type: 'json' };
import templatePublicJson from '@contracts/screens/template-public.json' with { type: 'json' };
import templateSharingJson from '@contracts/screens/template-sharing.json' with { type: 'json' };
import templatesJson from '@contracts/screens/templates.json' with { type: 'json' };
import type { EditorTabProps } from '../../templates/editor/tabProps';
import type {
  LayoutReviewView,
  TemplateChecklistView,
  TemplateEditorView,
  TemplatePublicView,
  TemplateSharingView,
  TemplatesView,
} from '../../templates/types';
import type { Widen } from './widen';

export const templates = templatesJson satisfies Widen<TemplatesView> as TemplatesView;
export const templateEditor =
  templateEditorJson satisfies Widen<TemplateEditorView> as TemplateEditorView;
export const templateSharing =
  templateSharingJson satisfies Widen<TemplateSharingView> as TemplateSharingView;
export const templatePublic =
  templatePublicJson satisfies Widen<TemplatePublicView> as TemplatePublicView;

// GET …/checklist answers the editor view's checklist and canPublish on their own.
export const templateChecklist: TemplateChecklistView = {
  checklist: templateEditor.checklist,
  canPublish: templateEditor.canPublish,
};

// layout-review.json itself: its `layout` is the compiled sample's tree, CSS and slots, so the
// fixture cannot drift from the contract.
export const layoutReview = layoutReviewJson satisfies Widen<LayoutReviewView> as LayoutReviewView;

// The props one editor tab (or the publish checklist) is mounted with on its own: the sample
// draft, a change that reports it saved, and a reload that does nothing. A spec passes its own to watch the calls.
export function editorTabProps(overrides: Partial<EditorTabProps> = {}): EditorTabProps {
  return {
    draft: templateEditor,
    change: () => Promise.resolve({ ok: true }),
    reload: () => Promise.resolve(),
    saving: false,
    ...overrides,
  };
}
