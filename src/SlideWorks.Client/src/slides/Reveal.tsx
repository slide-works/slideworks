import type { ReactNode } from 'react';

type RevealProps = {
  // The step at which this item appears (1-based); step 0 shows none of them.
  at: number;
  step: number;
  as?: 'div' | 'li' | 'span';
  className?: string;
  children: ReactNode;
};

// Hides an item until its step comes. It uses visibility and opacity, never
// display:none, so the item keeps its space and the layout never shifts.
export function Reveal({ at, step, as: Tag = 'div', className, children }: RevealProps) {
  const shown = step >= at;
  return (
    <Tag
      className={['sw-reveal', className].filter(Boolean).join(' ')}
      data-revealed={shown}
      aria-hidden={shown ? undefined : true}
      style={{ visibility: shown ? 'visible' : 'hidden', opacity: shown ? 1 : 0 }}
    >
      {children}
    </Tag>
  );
}
