// The types of a custom layout as the server sends it (T10). The tree is the checked form of the
// layout's markup: the client draws these nodes and never parses HTML. Types only.

// A CSS variable an element sets from a number slot.
export type TreeVar = { name: string; slot: string };

// One element. `classes` are final: the server has already written each with the layout's prefix
// (cl-<scope>-<layout name>__<class>), the same names its CSS uses. The client puts them on the element
// exactly as given and never builds or prefixes a class name. `step` is a numbered step from 1, or 0 for one step per repeated item. There is no text field:
// text comes only from the slot named in `slot`.
export type TreeNode = {
  tag: string;
  classes: string[];
  slot?: string;
  each?: string;
  if?: string;
  step?: number;
  vars: TreeVar[];
  variants: string[];
  children: TreeNode[];
};

// `scope` is the 8 hex characters inside the class prefix the server wrote. It is information only:
// the client does not use it to make class names.
export type LayoutTree = { version: 1; scope?: string; root: TreeNode[] };

export type CustomBlockSlotName = 'callout' | 'chip' | 'badge' | 'levels' | 'facts';

// A scalar slot type: allowed at the top level, as a field of a list's items, or as a list's `of`.
export type CustomScalarSlotType =
  | { type: 'text'; max: number; optional?: boolean }
  | { type: 'number'; min: number; max: number; optional?: boolean }
  | { type: 'enum'; values: string[]; optional?: boolean };

// A list's items: one scalar type (the value is `item`), or an object of fields, one of which may
// be one more list.
export type CustomListSlotType = {
  type: 'list';
  min: number;
  max: number;
  of: CustomScalarSlotType | { [field: string]: CustomScalarSlotType | CustomListSlotType };
};

// One slot of slots.json. Images and blocks are top-level only; an image is always optional.
export type CustomSlotType =
  | CustomScalarSlotType
  | CustomListSlotType
  | { type: 'image'; optional: true }
  | { type: CustomBlockSlotName; optional?: boolean };

export type CustomLayoutSurface = 'light' | 'dark' | 'either';

// One custom layout of a deck's template: `slots` is the `slots` object of its slots.json.
export type CustomLayoutView = {
  name: string;
  surface: CustomLayoutSurface;
  tree: LayoutTree;
  css: string;
  slots: { [name: string]: CustomSlotType };
};
