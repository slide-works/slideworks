// The Templates area's views and requests, as the server sends and takes them. Each is the shape of
// its sample under src/contracts/screens; fixtures/templates.ts ties the samples to these types.
import type {
  FooterMode,
  LookView,
  PaletteTokens,
  SharingLevel,
  TemplateShape,
  TemplateType,
} from '../api/types';
import type { CustomLayoutSurface, CustomLayoutView } from '../slides/custom/layoutTree';

export type TemplateStatus = 'draft' | 'published' | 'archived';

// One template on the templates page. `type` is "Head family / Body family"; `palette` is the
// palette's label, or "any". `version` is 0 while only a draft exists. `owner` is a name only on a
// row shared with the caller: it is null on the caller's own rows and on built-in rows.
export type TemplateRow = {
  id: string;
  name: string;
  shortName: string;
  purpose: string;
  type: string;
  palette: string;
  paletteLocked: boolean;
  version: number;
  status: TemplateStatus;
  deckCount: number;
  updatedLabel: string;
  sharing: SharingLevel;
  owner: string | null;
};

// GET /api/templates (templates.json).
export type TemplatesView = { mine: TemplateRow[]; shared: TemplateRow[]; builtIn: TemplateRow[] };

export type TemplateFontRole = 'head' | 'body' | 'mono';

// One font file of a manifest. `file` is an asset id the editor view's `assets` also lists.
export type TemplateManifestFont = {
  role: TemplateFontRole;
  family: string;
  file: string;
  weight: number;
  style: 'normal' | 'italic';
};

export type TemplateExtraColour = {
  name: 'extra-1' | 'extra-2' | 'extra-3' | 'extra-4';
  colour: string;
  ink: string;
};

// A template's own palette: every token, plus up to four extra colours.
export type TemplateManifestPalette = PaletteTokens & { extras?: TemplateExtraColour[] };

export type TemplateTitleArt = {
  kind: 'none' | 'generator' | 'image';
  generator?: string;
  file?: string;
};

// A template draft's manifest (src/contracts/template-manifest.schema.json). Every file field is
// an asset id. `palette` is left out when the template brings no palette of its own.
export type TemplateManifest = {
  name: string;
  label: string;
  purpose: string;
  basedOn: string;
  layouts: string[];
  chrome: {
    tracker: boolean;
    footer: FooterMode;
    progress: boolean;
    deepDivesOnDivider: boolean;
    draft: boolean;
    topBar: 'none' | 'accent';
  };
  outline: string[];
  houseStyle: string[];
  type: TemplateType;
  shape: TemplateShape;
  fonts: TemplateManifestFont[];
  logos: { light?: string; dark?: string; showOnDark: boolean };
  titleArt: TemplateTitleArt;
  ornament: { dots: string[] };
  palette?: TemplateManifestPalette;
  lockPalette: boolean;
  customLayouts: { name: string; file: string }[];
};

export type TemplateAssetKind = 'font' | 'logo' | 'art' | 'layout';

// What the upload check stored for a file: a font's average character width in em, a PNG's or
// WebP's size in pixels, and nothing for an SVG.
export type TemplateAssetMeta = { avgCharEm?: number; width?: number; height?: number };

// One stored file of a draft. `url` serves it.
export type TemplateAsset = {
  assetId: string;
  kind: TemplateAssetKind;
  fileName: string;
  contentType: string;
  bytes: number;
  meta: TemplateAssetMeta;
  rightsConfirmed: boolean;
  url: string;
};

// The editor's tabs, as the `tab` search parameter and a checklist row's `tab` name them.
export type EditorTab = 'basics' | 'fonts' | 'logo-art' | 'palette' | 'layouts';

export type TemplateCheckState = 'pass' | 'fail' | 'todo';

// One row of the publish checklist. `tab` is the editor tab that fixes it; `detail` may be empty.
export type TemplateChecklistRow = {
  key: string;
  label: string;
  state: TemplateCheckState;
  detail: string;
  tab: EditorTab;
};

export type TemplateVersion = { version: number; publishedLabel: string };

// GET and PUT /api/templates/{id} (template-editor.json).
export type TemplateEditorView = {
  id: string;
  name: string;
  revision: number;
  status: TemplateStatus;
  version: number;
  manifest: TemplateManifest;
  assets: TemplateAsset[];
  checklist: TemplateChecklistRow[];
  versions: TemplateVersion[];
  canPublish: boolean;
};

// GET /api/templates/{id}/checklist: the editor view's checklist and canPublish on their own.
export type TemplateChecklistView = { checklist: TemplateChecklistRow[]; canPublish: boolean };

// POST /api/templates: the template the draft starts from, and the new template's label.
export type CreateTemplateRequest = { basedOn: string; name: string };
export type TemplateCreated = { id: string };

// POST /api/templates/{id}/publish: the new version, and how many of the caller's decks are behind it.
export type TemplatePublished = { version: number; decksBehind: number };

// POST /api/templates/{id}/update-my-decks: how many decks moved to the latest version.
export type DecksUpdated = { count: number };

// GET and PUT /api/templates/{id}/sharing (template-sharing.json). `link` is the template's page.
export type TemplateSharingView = { level: SharingLevel; viewers: string[]; link: string };
export type TemplateSharingUpdate = { level: SharingLevel; viewers: string[] };

// GET /api/templates/{id}/public (template-public.json): the template's page at /t/{id}.
export type TemplatePublicView = {
  id: string;
  name: string;
  owner: string;
  purpose: string;
  look: LookView;
};

// GET /api/templates/{id}/layouts/{name}/review (layout-review.json): what the owner looks at
// before approving a custom layout. `asset` is the layout file under review. `sample` and
// `maximum` are slots: a typical slide and the fullest one the layout allows.
export type LayoutReviewView = {
  name: string;
  asset: string;
  approved: boolean;
  surface: CustomLayoutSurface;
  steps: number;
  layout: CustomLayoutView;
  sample: { [slot: string]: unknown };
  maximum: { [slot: string]: unknown };
  safeArea: { ok: boolean; overflow: string[] };
  contrast: { ok: boolean; failures: string[] };
};

// POST /api/templates/{id}/layouts/{name}/approve: the layout file the owner saw, and the overflow
// and contrast findings they accept.
export type ApproveLayoutRequest = { asset: string; overflow: string[]; contrast: string[] };
