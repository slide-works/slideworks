import { createElement, type CSSProperties, type ReactNode } from 'react';
import { Badge } from '../blocks/Badge';
import { Callout } from '../blocks/Callout';
import { Chip } from '../blocks/Chip';
import { Facts } from '../blocks/Facts';
import { Levels } from '../blocks/Levels';
import type { CalloutBlock, FactsBlock, ImageRef, LevelsBlock } from '../slots';
import { calloutStep } from '../steps';
import {
  checkLayout,
  classNameOf,
  isPresent,
  itemTypes,
  ownValue,
  varsOf,
  variantClassesOf,
  visibleAt,
  type TypeScope,
  type Values,
} from './bindings';
import styles from './CustomLayout.module.css';
import type { CustomLayoutView, TreeNode } from './layoutTree';

type CustomLayoutProps = {
  layout: CustomLayoutView;
  // The slide's slots, as stored; the layout's own slots.json says what they hold.
  slots: { [name: string]: unknown };
  // The slide's current step.
  step: number;
  // The slide's step count, as the server counted it: the layout's own steps and a last-revealing callout's.
  steps: number;
};

// A value the slots should not hold: the whole slide is drawn as the error state.
class CannotDraw extends Error {}

type Context = { step: number; revealOn: boolean; lastStep: number; key: { n: number } };

// The state for a tree or slot that cannot be drawn. Nothing here takes focus.
export function CustomLayoutError() {
  return (
    <div className="sw-layout">
      <p data-testid="custom-layout-error" role="alert" className={styles.message}>
        This slide&apos;s layout could not be drawn.
      </p>
    </div>
  );
}

// The state for a layout name that is neither a built-in nor in the deck's template.
export function UnknownLayout() {
  return (
    <div className="sw-layout">
      <p data-testid="unknown-layout" className={styles.message}>
        This slide&apos;s layout is not in the deck&apos;s template.
      </p>
    </div>
  );
}

// A slide on a custom layout, drawn from its checked tree. Every element is made with createElement
// from a node, with only the props this file names; slot values are children as text, so none is
// ever markup. The css is the server's compiled, sanitised sheet, a text node in its own <style>.
// A layout that fails the check, or a slot that does not hold what its type says, draws the error.
export function CustomLayout({ layout, slots, step, steps }: CustomLayoutProps) {
  if (!checkLayout(layout)) return <CustomLayoutError />;
  const context: Context = {
    step,
    revealOn: slots.reveal === 'one-by-one',
    lastStep: Math.max(0, steps - calloutStep(0, slots.callout as CalloutBlock | undefined)),
    key: { n: 0 },
  };
  let body: ReactNode[];
  try {
    body = drawNodes(layout.tree.root, slots, layout.slots, context, false);
  } catch (error) {
    if (error instanceof CannotDraw) return <CustomLayoutError />;
    throw error;
  }
  return (
    <div
      className={`sw-layout ${styles.root}`}
      data-custom-layout={layout.name}
      data-testid="custom-layout"
    >
      <style>{layout.css}</style>
      <div className={styles.body} data-custom-layout-body="">
        {body}
      </div>
    </div>
  );
}

function drawNodes(
  nodes: TreeNode[],
  values: Values,
  types: TypeScope,
  context: Context,
  hiddenAbove: boolean,
): ReactNode[] {
  return nodes.flatMap((node) => {
    if (node.each === undefined) {
      return [drawNode(node, values, types, undefined, context, hiddenAbove)];
    }
    const list = ownValue(values, node.each);
    const listType = types[node.each];
    if (listType?.type !== 'list') throw new CannotDraw();
    // An optional list that is not there draws nothing; a value that is not a list cannot be drawn.
    if (list === undefined || list === null) return [];
    if (!Array.isArray(list)) throw new CannotDraw();
    const inner = itemTypes(listType);
    const scalarItems = typeof (listType.of as { type?: unknown }).type === 'string';
    if (!scalarItems && !list.every(isObject)) throw new CannotDraw();
    return list.map((entry: unknown, index) =>
      drawNode(node, itemValues(entry), inner, index, context, hiddenAbove),
    );
  });
}

function itemValues(entry: unknown): Values {
  return typeof entry === 'object' && entry !== null && !Array.isArray(entry)
    ? (entry as Values)
    : { item: entry };
}

function drawNode(
  node: TreeNode,
  values: Values,
  types: TypeScope,
  itemIndex: number | undefined,
  context: Context,
  hiddenAbove: boolean,
): ReactNode {
  if (node.if !== undefined && !isPresent(ownValue(values, node.if))) return null;
  const slotValue = node.slot === undefined ? undefined : ownValue(values, node.slot);
  // An absent slot draws no element.
  if (node.slot !== undefined && (slotValue === undefined || slotValue === null)) return null;

  const slotType = node.slot === undefined ? undefined : types[node.slot];
  const block =
    slotType !== undefined &&
    slotType.type !== 'text' &&
    slotType.type !== 'number' &&
    slotType.type !== 'enum';

  const style: { [name: string]: string | number } = { ...varsOf(node, values) };
  const props: { [name: string]: unknown } = { key: context.key.n++ };
  const classes = [classNameOf(node), ...variantClassesOf(node, values, types)].filter(Boolean);
  if (block) {
    props['data-sw-block'] = '';
    props['data-testid'] = `custom-slot-${node.slot}`;
  }
  if (classes.length > 0) props.className = classes.join(' ');

  // A waiting element keeps its box: inline visibility and opacity, never display: none.
  let at: number | undefined;
  let shown = true;
  if (node.step !== undefined) {
    shown = visibleAt(node, itemIndex, context.step, context.revealOn);
    at = node.step;
  } else if (slotType?.type === 'callout' && context.revealOn) {
    const callout = slotValue as CalloutBlock;
    const when = calloutStep(context.lastStep, callout);
    if (when > 0) {
      shown = context.step >= when;
      at = when;
    }
  }
  if (at !== undefined && context.revealOn) {
    const hidden = hiddenAbove || !shown;
    style.visibility = hidden ? 'hidden' : 'visible';
    style.opacity = hidden ? 0 : 1;
    if (hidden) props['aria-hidden'] = true;
    props['data-revealed'] = !hidden;
    hiddenAbove = hidden;
  }
  if (Object.keys(style).length > 0) props.style = style as CSSProperties;

  if (node.slot !== undefined && slotType !== undefined) {
    return createElement(block ? 'div' : node.tag, props, slotContent(slotType.type, slotValue));
  }
  return createElement(
    node.tag,
    props,
    ...drawNodes(node.children, values, types, context, hiddenAbove),
  );
}

function isObject(value: unknown): value is { [key: string]: unknown } {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

const BLOCK_TONES = ['accent', 'positive', 'negative', 'inverse', 'good', 'bad', 'info'];

// What a slot shows: text as text, one of SlideWorks' own blocks, or an uploaded image.
function slotContent(type: string, value: unknown): ReactNode {
  switch (type) {
    case 'text':
    case 'enum':
      if (typeof value !== 'string') throw new CannotDraw();
      return value;
    case 'number':
      if (typeof value !== 'number' || !Number.isFinite(value)) throw new CannotDraw();
      return String(value);
    case 'chip':
      if (typeof value !== 'string') throw new CannotDraw();
      return <Chip text={value} />;
    case 'badge':
      if (typeof value !== 'string') throw new CannotDraw();
      return <Badge text={value} />;
    case 'callout':
      if (
        !isObject(value) ||
        typeof value.text !== 'string' ||
        typeof value.tone !== 'string' ||
        !BLOCK_TONES.includes(value.tone) ||
        (value.fill !== undefined && value.fill !== 'soft' && value.fill !== 'solid') ||
        (value.reveal !== undefined && value.reveal !== 'with-slide' && value.reveal !== 'last')
      ) {
        throw new CannotDraw();
      }
      return <Callout {...(value as CalloutBlock)} />;
    case 'levels':
      if (!isObject(value) || !Array.isArray(value.items)) throw new CannotDraw();
      if (value.title !== undefined && typeof value.title !== 'string') throw new CannotDraw();
      for (const item of value.items) {
        if (!isObject(item) || typeof item.text !== 'string') throw new CannotDraw();
        if (item.mark !== undefined && typeof item.mark !== 'string') throw new CannotDraw();
        if (item.state !== undefined && item.state !== 'plain' && item.state !== 'current') {
          throw new CannotDraw();
        }
      }
      return <Levels levels={value as LevelsBlock} />;
    case 'facts':
      if (!Array.isArray(value)) throw new CannotDraw();
      for (const fact of value) {
        if (!isObject(fact) || typeof fact.label !== 'string' || typeof fact.value !== 'string') {
          throw new CannotDraw();
        }
        if (fact.tone !== undefined && !BLOCK_TONES.includes(fact.tone as string)) {
          throw new CannotDraw();
        }
      }
      return <Facts facts={value as FactsBlock} />;
    case 'image': {
      if (!isObject(value) || typeof value.image !== 'string' || typeof value.alt !== 'string') {
        throw new CannotDraw();
      }
      const image = value as ImageRef;
      return (
        <img
          className={styles.image}
          src={`/api/images/${encodeURIComponent(image.image)}`}
          alt={image.alt}
        />
      );
    }
    default:
      throw new CannotDraw();
  }
}
