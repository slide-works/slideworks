import type { TemplateShape } from '../../../api/types';

// The three corner choices on the Basics tab. A choice is the medium radius in px; the other
// radii follow it in the steps the built-in templates use (small 2 less, large 4 more, extra
// large 6 more), and never go below square.
export const SHAPE_CHOICES: readonly { radius: number; label: string }[] = [
  { radius: 0, label: 'Square' },
  { radius: 8, label: '8 px' },
  { radius: 16, label: '16 px' },
];

// The shape a choice sets: the radii of that choice, the shadow as it was.
export function shapeFor(radius: number, shadow: string): TemplateShape {
  const step = (by: number) => (radius === 0 ? 0 : radius + by);
  return { radius: { sm: step(-2), md: radius, lg: step(4), xl: step(6) }, shadow };
}

// The choice a shape is on, or null when its radii are none of the three (an imported template).
export function shapeChoiceOf(shape: TemplateShape): number | null {
  const choice = SHAPE_CHOICES.find(({ radius }) => radius === shape.radius.md);
  return choice ? choice.radius : null;
}
