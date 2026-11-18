import { createElement } from 'react';
import type { LucideProps } from 'lucide-react';
import { iconFor } from './icons';
import styles from './Icon.module.css';

type IconProps = {
  // One of the built-in names (icons.json). A slot value, so any string may arrive.
  name: string;
  // The side of its square box, in whole canvas px.
  size: number;
};

// A line icon from the built-in set (K11), drawn in the text colour of the
// element that places it and hidden from screen readers: the text beside it
// says what it means. An unknown name draws nothing.
export function Icon({ name, size }: IconProps) {
  const drawing = iconFor(name);
  if (drawing === undefined) return null;
  // createElement, not JSX: the drawing is one of the 48 fixed components of
  // the set, looked up by name, never a component made while rendering.
  return createElement(drawing, {
    'data-testid': 'block-icon',
    'data-icon': name,
    className: styles.icon,
    size,
    color: 'currentColor',
    'aria-hidden': true,
    focusable: false,
  } as LucideProps);
}
