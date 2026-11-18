// One compiled custom layout for specs: the contract's own sample
// (src/contracts/custom-layout-compiled.sample.json), as the view the client draws and the two
// slides the review page shows. Every class in the tree and the CSS already carries the server's
// prefix; a spec mounts them as given.
import compiledJson from '@contracts/custom-layout-compiled.sample.json' with { type: 'json' };
import type { CustomLayoutView } from '../../slides/custom/layoutTree';
import type { Widen } from './widen';

export type CustomLayoutSample = {
  view: CustomLayoutView;
  sample: { [slot: string]: unknown };
  maximum: { [slot: string]: unknown };
};

// `view.slots` is the `slots` object of the sample's schema, and `view.tree` carries its scope.
export function customLayoutSample(): CustomLayoutSample {
  const view = {
    name: compiledJson.name,
    surface: compiledJson.surface,
    tree: compiledJson.tree,
    css: compiledJson.css,
    slots: compiledJson.schema.slots,
  } satisfies Widen<CustomLayoutView> as CustomLayoutView;
  return { view, sample: compiledJson.sample, maximum: compiledJson.maximum };
}
