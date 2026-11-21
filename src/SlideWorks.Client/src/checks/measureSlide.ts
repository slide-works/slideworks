import { canvasOf } from './canvas';
import { contrast } from './contrast';
import { images } from './images';
import { overflow } from './overflow';
import { textSize } from './textSize';
import type { SlideFinding } from './types';

// All measured findings for one slide frame, in rule order.
export function measureSlide(frame: HTMLElement, slide: number): SlideFinding[] {
  const canvas = canvasOf(frame);
  return [
    ...overflow(frame, slide, canvas),
    ...images(frame, slide, canvas),
    ...contrast(frame, slide, canvas),
    ...textSize(frame, slide, canvas),
  ];
}
