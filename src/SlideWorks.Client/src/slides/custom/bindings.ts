// The pure rules of drawing a custom layout (T10): what an element's names read, which variables and
// variant classes it takes, when a step shows it, and the fail-closed check of the whole tree. The
// server writes every class; nothing here builds, prefixes or rewrites one. A tree that fails
// checkLayout is never drawn.
import allow from '@contracts/custom-layout-allow.json' with { type: 'json' };
import {
  variantClass,
  type CustomLayoutView,
  type CustomListSlotType,
  type CustomSlotType,
  type TreeNode,
} from './layoutTree';

// The most steps a layout takes (the server's MAX_STEPS) and the deepest nesting drawn.
export const MAX_STEP = 20;
const MAX_DEPTH = 32;

const NAME = /^[a-z][a-z0-9-]{1,30}$/;
const SCOPE = /^[0-9a-f]{8}$/;
const VAR_NAME = /^--[a-z][a-z0-9-]*$/;
const CLASS_REST = /^[A-Za-z0-9_-]+$/;
const BLOCKS = ['callout', 'chip', 'badge', 'levels', 'facts'] as const;
const NODE_KEYS = ['tag', 'classes', 'slot', 'each', 'if', 'step', 'vars', 'variants', 'children'];

// The values a name reads: the slide's slots, or the innermost item's fields.
export type Values = { [name: string]: unknown };
// The slot types of the same scope.
export type TypeScope = { [name: string]: CustomSlotType };
// Which item of which list an element stands in, outermost first.
export type ItemPath = { list: string; index: number }[];

function isRecord(value: unknown): value is { [key: string]: unknown } {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

// An own property only: a slot named constructor or __proto__ reads nothing.
export function ownValue(values: Values, name: string): unknown {
  return Object.prototype.hasOwnProperty.call(values, name) ? values[name] : undefined;
}

// What names read inside the item at the end of the path: that item's fields only, or `item` for
// an item that is one scalar. An outer item is never visible. A path that leads nowhere reads nothing.
export function scopeOf(slots: Values, itemPath: ItemPath): Values {
  let current: Values = slots;
  for (const { list, index } of itemPath) {
    const items = ownValue(current, list);
    if (!Array.isArray(items) || !Number.isInteger(index) || index < 0 || index >= items.length) {
      return {};
    }
    const entry: unknown = items[index];
    current = isRecord(entry) ? entry : { item: entry };
  }
  return current;
}

// An optional slot or a list is present when it has a value that is not empty: not missing, not
// null, not an empty text or an empty list. Zero is a value.
export function isPresent(value: unknown): boolean {
  if (value === undefined || value === null) return false;
  if (typeof value === 'string') return value.length > 0;
  if (Array.isArray(value)) return value.length > 0;
  return true;
}

// The variables an element sets, name to number: a finite number only, under a name that is
// `--[a-z][a-z0-9-]*` and not one of the frame's `--sw-` tokens.
export function varsOf(node: TreeNode, scope: Values): { [name: string]: number } {
  const vars: { [name: string]: number } = {};
  for (const { name, slot } of node.vars) {
    const value = ownValue(scope, slot);
    if (
      VAR_NAME.test(name) &&
      !name.startsWith('--sw-') &&
      typeof value === 'number' &&
      Number.isFinite(value)
    ) {
      vars[name] = value;
    }
  }
  return vars;
}

// The classes an element's variants add: the server's class for the value, and only when the value
// is one the enum names.
export function variantClassesOf(node: TreeNode, scope: Values, slotTypes: TypeScope): string[] {
  const added: string[] = [];
  for (const variant of node.variants) {
    const type = ownType(slotTypes, variant.slot);
    const value = ownValue(scope, variant.slot);
    if (type?.type !== 'enum' || typeof value !== 'string' || !type.values.includes(value))
      continue;
    const name = variantClass(variant, value);
    if (name !== undefined) added.push(name);
  }
  return added;
}

// The classes of an element as the server wrote them.
export function classNameOf(node: TreeNode): string {
  return node.classes.join(' ');
}

function ownType(types: TypeScope, name: string): CustomSlotType | undefined {
  return Object.prototype.hasOwnProperty.call(types, name) ? types[name] : undefined;
}

// Whether a step shows the element. With reveal off, or on an element with no step, always. Step N
// shows from click N; step 0 (on a repeated element) shows item i, 1-based, from click i.
export function visibleAt(
  node: TreeNode,
  itemIndex: number | undefined,
  step: number,
  revealOn: boolean,
): boolean {
  if (!revealOn || node.step === undefined) return true;
  if (node.step === 0) return itemIndex === undefined ? true : step >= itemIndex + 1;
  return step >= node.step;
}

// The prefix the server gave this layout's classes, cl-<scope>-<name>__, or null when the tree
// carries no scope to name it exactly (then any 8 hex characters stand for it) or the name is bad.
export function prefixOf(layout: CustomLayoutView): string | null {
  const scope = layout.tree.scope;
  return NAME.test(layout.name) && typeof scope === 'string' && SCOPE.test(scope)
    ? `cl-${scope}-${layout.name}__`
    : null;
}

// Fail-closed check of one class: it starts with the layout's prefix and then holds one plain class
// name, so a value cannot smuggle a second class in.
export function isOwnClass(layout: CustomLayoutView, value: unknown): boolean {
  if (typeof value !== 'string' || !NAME.test(layout.name)) return false;
  const prefix = prefixOf(layout);
  let rest: string;
  if (prefix !== null) {
    if (!value.startsWith(prefix)) return false;
    rest = value.slice(prefix.length);
  } else {
    const head = `cl-`;
    const tail = `-${layout.name}__`;
    if (!value.startsWith(head) || !SCOPE.test(value.slice(3, 11))) return false;
    if (!value.slice(11).startsWith(tail)) return false;
    rest = value.slice(11 + tail.length);
  }
  return CLASS_REST.test(rest);
}

// The slot types of the items of a list.
export function itemTypes(list: CustomListSlotType): TypeScope {
  const of: unknown = list.of;
  if (isRecord(of) && typeof of.type === 'string') return { item: of as CustomSlotType };
  return of as TypeScope;
}

function isBlock(type: string): boolean {
  return (BLOCKS as readonly string[]).includes(type);
}

// The slot types an element's own binding may show.
export function isShowable(type: CustomSlotType): boolean {
  return (
    type.type === 'text' ||
    type.type === 'number' ||
    type.type === 'enum' ||
    type.type === 'image' ||
    isBlock(type.type)
  );
}

function checkNode(
  layout: CustomLayoutView,
  node: unknown,
  types: TypeScope,
  depth: number,
): boolean {
  if (!isRecord(node) || depth > MAX_DEPTH) return false;
  // A key the contract does not know (a style, an href, an event handler) is never drawn.
  if (Object.keys(node).some((key) => !NODE_KEYS.includes(key))) return false;
  const n = node as unknown as TreeNode;
  if (typeof n.tag !== 'string' || !allow.tags.includes(n.tag)) return false;
  if (!Array.isArray(n.classes) || !n.classes.every((c) => isOwnClass(layout, c))) return false;
  if (!Array.isArray(n.vars) || !Array.isArray(n.variants) || !Array.isArray(n.children)) {
    return false;
  }
  let inner = types;
  if (n.each !== undefined) {
    const list = typeof n.each === 'string' ? ownType(types, n.each) : undefined;
    if (list?.type !== 'list') return false;
    inner = itemTypes(list);
  }
  if (n.step !== undefined) {
    if (!Number.isInteger(n.step) || n.step < 0 || n.step > MAX_STEP) return false;
    if (n.step === 0 && n.each === undefined) return false;
  }
  if (n.if !== undefined && (typeof n.if !== 'string' || ownType(inner, n.if) === undefined)) {
    return false;
  }
  for (const v of n.vars) {
    if (!isRecord(v) || typeof v.name !== 'string' || typeof v.slot !== 'string') return false;
    if (ownType(inner, v.slot)?.type !== 'number') return false;
  }
  for (const v of n.variants) {
    if (!isRecord(v) || typeof v.slot !== 'string' || !isRecord(v.classes)) return false;
    if (ownType(inner, v.slot)?.type !== 'enum') return false;
    if (!Object.values(v.classes).every((c) => isOwnClass(layout, c))) return false;
  }
  if (n.slot !== undefined) {
    const type = typeof n.slot === 'string' ? ownType(inner, n.slot) : undefined;
    if (type === undefined || !isShowable(type) || n.children.length > 0) return false;
  }
  return n.children.every((child) => checkNode(layout, child, inner, depth + 1));
}

// Whether the whole layout may be drawn: a sane name and css, version 1, only allowed tags, only
// the layout's own classes, and every name bound to a slot that is there in its scope.
export function checkLayout(layout: CustomLayoutView): boolean {
  const { tree } = layout;
  return (
    NAME.test(layout.name) &&
    typeof layout.css === 'string' &&
    isRecord(layout.slots) &&
    isRecord(tree) &&
    tree.version === 1 &&
    Array.isArray(tree.root) &&
    tree.root.every((node) => checkNode(layout, node, layout.slots, 0))
  );
}

// The latest step the layout's own elements wait for, for a callout that shows after them.
export function lastStepOf(nodes: TreeNode[], scope: Values): number {
  let last = 0;
  for (const node of nodes) {
    let inner = scope;
    if (node.each !== undefined) {
      const items = ownValue(scope, node.each);
      const count = Array.isArray(items) ? items.length : 0;
      if (node.step === 0) last = Math.max(last, count);
      for (let i = 0; i < count; i++) {
        const entry: unknown = (items as unknown[])[i];
        inner = isRecord(entry) ? entry : { item: entry };
        last = Math.max(last, node.step ?? 0, lastStepOf(node.children, inner));
      }
    } else {
      last = Math.max(last, node.step ?? 0, lastStepOf(node.children, inner));
    }
  }
  return last;
}
