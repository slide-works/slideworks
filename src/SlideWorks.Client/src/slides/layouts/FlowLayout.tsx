import { Fragment, type CSSProperties } from 'react';
import { BodyCallout } from '../blocks/BodyCallout';
import { Chip } from '../blocks/Chip';
import { SlideHeading } from '../blocks/SlideHeading';
import { describeFlow } from '../diagrams/describeFlow';
import { emphasisFor } from '../emphasis';
import type { LayoutProps } from '../slots';
import { calloutStep } from '../steps';
import { FlowArrow } from './FlowArrow';
import { FlowNode } from './FlowNode';
import styles from './FlowLayout.module.css';

// 2–4 nodes joined by arrows, left to right, in one grid row: a column per
// node and a 72 px column per arrow. A plain node is 1 fr and a node with items
// 2 fr, so a node is (1680 - 72 x (n - 1)) / total fr x its fr wide, as the
// server's FlowLayout.cs counts (it also has the heights, number for number).
// With reveal 'one-by-one' node n comes at step n, with the arrow that points
// to it; a callout that reveals 'last' is the step after them. The figure
// clips its overflow.
const ARROW_COLUMN = 72;

export function FlowLayout({ slots, step }: LayoutProps<'flow'>) {
  const { kicker, headline, nodes, caption, chips, reveal, callout } = slots;
  const oneByOne = reveal === 'one-by-one';
  const columns = nodes
    .map(
      (n, i) => `${i > 0 ? `${ARROW_COLUMN}px ` : ''}minmax(0, ${n.items !== undefined ? 2 : 1}fr)`,
    )
    .join(' ');
  const row = { gridTemplateColumns: columns } as CSSProperties;
  return (
    <div data-testid="layout-flow" className="sw-layout">
      <SlideHeading
        layout="flow"
        kicker={kicker}
        headline={headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
      <div data-testid="flow-body" className={`sw-body ${styles.body}`}>
        <figure
          data-testid="flow-figure"
          aria-label={describeFlow({ nodes })}
          className={styles.figure}
        >
          <div className={styles.nodes} style={row}>
            {nodes.map((node, i) => {
              const at = oneByOne ? i + 1 : 0;
              return (
                <Fragment key={i}>
                  {i > 0 ? <FlowArrow at={at} step={step} /> : null}
                  <FlowNode node={node} at={at} step={step} />
                </Fragment>
              );
            })}
          </div>
          {caption !== undefined ? (
            <figcaption data-testid="flow-caption" className={styles.caption}>
              {caption}
            </figcaption>
          ) : null}
          {chips !== undefined ? (
            <div
              data-testid="flow-chips"
              className={`${styles.chips} ${caption === undefined ? styles.chipsAlone : ''}`}
            >
              {chips.map((chip, i) => (
                <Chip key={i} text={chip} />
              ))}
            </div>
          ) : null}
        </figure>
        {callout !== undefined ? (
          <BodyCallout
            callout={callout}
            step={step}
            at={calloutStep(oneByOne ? nodes.length : 0, callout)}
          />
        ) : null}
      </div>
    </div>
  );
}
