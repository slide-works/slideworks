import type { CSSProperties } from 'react';
import { panelTone, toneRule, toneStyle } from '../blocks/tone';
import { Reveal } from '../Reveal';
import type { FlowNode as FlowNodeSlot } from '../slots/flow';
import styles from './FlowNode.module.css';

type FlowNodeProps = {
  node: FlowNodeSlot;
  // The step it comes at; 0 shows it from the start.
  at: number;
  step: number;
};

// One node: a card with an 8 px rule on its left, an optional label, the title
// and text. A node with items is a box of tiles. inverse and accent fill the
// card; positive and negative colour the rule. The slot keeps its space while
// hidden, and the card fills the slot, so every node is as tall as the tallest.
export function FlowNode({ node, at, step }: FlowNodeProps) {
  const { title, label, text, items, tone } = node;
  const filled = tone !== undefined && ['inverse', 'accent'].includes(panelTone(tone));
  const style = {
    ...toneStyle(tone ?? 'accent', filled ? 'solid' : 'plain'),
    '--node-rule': `var(--sw-${toneRule(tone ?? 'accent')})`,
  } as CSSProperties;
  return (
    <Reveal at={at} step={step} className={styles.slot}>
      <div
        data-testid="flow-node"
        data-group={items !== undefined ? 'true' : undefined}
        data-filled={filled}
        className={styles.node}
        style={style}
      >
        {label !== undefined ? <p className={styles.label}>{label}</p> : null}
        <h3 className={styles.title}>{title}</h3>
        {text !== undefined ? <p className={styles.text}>{text}</p> : null}
        {items !== undefined ? (
          <ul data-testid="flow-tiles" className={styles.tiles}>
            {items.map((item, i) => (
              <li key={i} data-testid="flow-tile" className={styles.tile}>
                {item}
              </li>
            ))}
          </ul>
        ) : null}
      </div>
    </Reveal>
  );
}
