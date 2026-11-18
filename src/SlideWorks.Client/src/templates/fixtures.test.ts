import { describe, expect, it } from 'vitest';
import type { LookView } from '../api/types';
import type { CustomLayoutView } from '../slides/custom/layoutTree';
import {
  layoutReview,
  templateChecklist,
  templateEditor,
  templatePublic,
  templateSharing,
  templates,
} from '../test-support/fixtures/templates';
import type {
  LayoutReviewView,
  TemplateAsset,
  TemplateAssetMeta,
  TemplateChecklistRow,
  TemplateChecklistView,
  TemplateEditorView,
  TemplateExtraColour,
  TemplateManifest,
  TemplateManifestFont,
  TemplatePublicView,
  TemplateRow,
  TemplateSharingView,
  TemplateTitleArt,
  TemplateVersion,
  TemplatesView,
} from './types';

// The run-time half of the tie between the Templates screen samples (src/contracts/screens) and
// the client's types: `satisfies` in fixtures/templates.ts checks field names and value kinds but
// lets an extra key in a sample pass and checks no literal value. Each table is a Record over the
// type's keys, so it can't miss or invent a key; `false` marks a key the type makes optional.
type Keys<T> = { [K in keyof Required<T>]: undefined extends T[K] ? false : true };

function expectKeys<T extends object>(value: T, table: Keys<T>, where: string) {
  const all = Object.keys(table).sort();
  const required = all.filter((key) => (table as Record<string, boolean>)[key]);
  const found = Object.keys(value).sort();
  expect(
    found.filter((key) => !all.includes(key)),
    `${where}: keys the type lacks`,
  ).toEqual([]);
  expect(
    required.filter((key) => !found.includes(key)),
    `${where}: keys the sample lacks`,
  ).toEqual([]);
}

const ROW: Keys<TemplateRow> = {
  id: true,
  name: true,
  shortName: true,
  purpose: true,
  type: true,
  palette: true,
  paletteLocked: true,
  version: true,
  status: true,
  deckCount: true,
  updatedLabel: true,
  sharing: true,
  owner: true,
};
const MANIFEST: Keys<TemplateManifest> = {
  name: true,
  label: true,
  purpose: true,
  basedOn: true,
  layouts: true,
  chrome: true,
  outline: true,
  houseStyle: true,
  type: true,
  shape: true,
  fonts: true,
  logos: true,
  titleArt: true,
  ornament: true,
  palette: false,
  lockPalette: true,
  customLayouts: true,
};

const STATUSES = ['draft', 'published', 'archived'];
const SHARING_LEVELS = ['private', 'named', 'link'];

describe('the templates page sample', () => {
  it('has exactly the lists and row keys of TemplatesView', () => {
    expectKeys<TemplatesView>(templates, { mine: true, shared: true, builtIn: true }, 'templates');
    const rows = [...templates.mine, ...templates.shared, ...templates.builtIn];
    expect(rows.length).toBeGreaterThan(2);
    for (const row of rows) {
      expectKeys(row, ROW, `row ${row.id}`);
      expect(STATUSES).toContain(row.status);
      expect(SHARING_LEVELS).toContain(row.sharing);
    }
  });
});

describe('the template editor sample', () => {
  it('has exactly the keys of TemplateEditorView and its manifest', () => {
    expectKeys<TemplateEditorView>(
      templateEditor,
      {
        id: true,
        name: true,
        revision: true,
        status: true,
        version: true,
        manifest: true,
        assets: true,
        checklist: true,
        versions: true,
        canPublish: true,
      },
      'editor',
    );
    expect(STATUSES).toContain(templateEditor.status);

    const { manifest } = templateEditor;
    expectKeys(manifest, MANIFEST, 'manifest');
    expectKeys<TemplateManifest['chrome']>(
      manifest.chrome,
      {
        tracker: true,
        footer: true,
        progress: true,
        deepDivesOnDivider: true,
        draft: true,
        topBar: true,
      },
      'manifest.chrome',
    );
    expect(['none', 'accent']).toContain(manifest.chrome.topBar);
    expectKeys<TemplateManifest['logos']>(
      manifest.logos,
      { light: false, dark: false, showOnDark: true },
      'manifest.logos',
    );
    expectKeys<TemplateTitleArt>(
      manifest.titleArt,
      { kind: true, generator: false, file: false },
      'manifest.titleArt',
    );
    expect(['none', 'generator', 'image']).toContain(manifest.titleArt.kind);
    expectKeys<TemplateManifest['ornament']>(manifest.ornament, { dots: true }, 'ornament');
  });

  it('has fonts, extra colours and custom layouts in the manifest shapes', () => {
    const { manifest } = templateEditor;
    expect(manifest.fonts.length).toBeGreaterThan(0);
    for (const font of manifest.fonts) {
      expectKeys<TemplateManifestFont>(
        font,
        { role: true, family: true, file: true, weight: true, style: true },
        `font ${font.file}`,
      );
      expect(['head', 'body', 'mono']).toContain(font.role);
      expect(['normal', 'italic']).toContain(font.style);
    }
    for (const extra of manifest.palette?.extras ?? []) {
      expectKeys<TemplateExtraColour>(extra, { name: true, colour: true, ink: true }, extra.name);
      expect(['extra-1', 'extra-2', 'extra-3', 'extra-4']).toContain(extra.name);
    }
    for (const layout of manifest.customLayouts)
      expectKeys(layout, { name: true, file: true }, `custom layout ${layout.name}`);
  });

  it('has assets, checklist rows and versions with exactly their keys', () => {
    expect(templateEditor.assets.length).toBeGreaterThan(0);
    for (const asset of templateEditor.assets) {
      expectKeys<TemplateAsset>(
        asset,
        {
          assetId: true,
          kind: true,
          fileName: true,
          contentType: true,
          bytes: true,
          meta: true,
          rightsConfirmed: true,
          url: true,
        },
        `asset ${asset.assetId}`,
      );
      expectKeys<TemplateAssetMeta>(
        asset.meta,
        { avgCharEm: false, width: false, height: false },
        `asset ${asset.assetId} meta`,
      );
      expect(['font', 'logo', 'art', 'layout']).toContain(asset.kind);
    }
    expect(templateEditor.checklist.length).toBeGreaterThan(0);
    for (const row of templateEditor.checklist) {
      expectKeys<TemplateChecklistRow>(
        row,
        { key: true, label: true, state: true, detail: true, tab: true },
        `checklist ${row.key}`,
      );
      expect(['pass', 'fail', 'todo']).toContain(row.state);
      expect(['basics', 'fonts', 'logo-art', 'palette', 'layouts']).toContain(row.tab);
    }
    for (const version of templateEditor.versions)
      expectKeys<TemplateVersion>(version, { version: true, publishedLabel: true }, 'version');
  });

  it('gives the checklist call the editor view’s checklist and canPublish', () => {
    expectKeys<TemplateChecklistView>(
      templateChecklist,
      { checklist: true, canPublish: true },
      'checklist view',
    );
    expect(templateChecklist.checklist).toBe(templateEditor.checklist);
    expect(templateChecklist.canPublish).toBe(templateEditor.canPublish);
  });
});

describe('the template sharing and public samples', () => {
  it('has exactly the keys of TemplateSharingView', () => {
    expectKeys<TemplateSharingView>(
      templateSharing,
      { level: true, viewers: true, link: true },
      'sharing',
    );
    expect(SHARING_LEVELS).toContain(templateSharing.level);
  });

  it('has exactly the keys of TemplatePublicView and its look', () => {
    expectKeys<TemplatePublicView>(
      templatePublic,
      { id: true, name: true, owner: true, purpose: true, look: true },
      'public',
    );
    expectKeys<LookView>(
      templatePublic.look,
      {
        template: true,
        palette: true,
        logo: true,
        joinBar: true,
        footerDateLabel: true,
        draft: true,
      },
      'public.look',
    );
  });
});

describe('the layout review sample', () => {
  it('has exactly the keys of LayoutReviewView and its compiled layout', () => {
    expectKeys<LayoutReviewView>(
      layoutReview,
      {
        name: true,
        asset: true,
        approved: true,
        surface: true,
        steps: true,
        layout: true,
        sample: true,
        maximum: true,
        safeArea: true,
        contrast: true,
      },
      'layout review',
    );
    expectKeys<CustomLayoutView>(
      layoutReview.layout,
      { name: true, surface: true, tree: true, css: true, slots: true },
      'layout review layout',
    );
    expectKeys<CustomLayoutView['tree']>(
      layoutReview.layout.tree,
      { version: true, scope: false, root: true },
      'layout review tree',
    );
    expectKeys<LayoutReviewView['safeArea']>(
      layoutReview.safeArea,
      { ok: true, overflow: true },
      'safeArea',
    );
    expectKeys<LayoutReviewView['contrast']>(
      layoutReview.contrast,
      { ok: true, failures: true },
      'contrast',
    );
    expect(['light', 'dark', 'either']).toContain(layoutReview.surface);
    expect(layoutReview.layout.surface).toBe(layoutReview.surface);
    expect(layoutReview.layout.name).toBe(layoutReview.name);
    expect(layoutReview.layout.tree.version).toBe(1);
    expect(layoutReview.layout.tree.root.length).toBeGreaterThan(0);
  });
});
