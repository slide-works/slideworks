import { isChrome } from './canvas';
import { severityOf, type MeasureRule, type SlideFinding } from './types';

// Drawn wider or taller than this many times its natural size looks blurry.
const MAX_STRETCH = 1.5;

const whole = (value: number) => Math.round(value);

// The rule measures the element's box. That is right for the current layouts, whose images are object-fit cover or sit
// in a box sized to the image's ratio. An object-fit: contain box would need its own scale (the drawn image is
// smaller than its box).
// Images that failed to load, or are drawn much larger than their pixels.
export const images: MeasureRule = (frame, slide, canvas) => {
  const found: SlideFinding[] = [];
  const add = (rule: 'imageNotLoaded' | 'imageLowResolution', message: string) =>
    found.push({
      slide,
      rule,
      severity: severityOf(rule),
      source: 'measured',
      where: 'img',
      message,
    });

  for (const img of frame.querySelectorAll('img')) {
    if (isChrome(img)) continue;
    if (!img.complete || img.naturalWidth <= 0) {
      add('imageNotLoaded', "An image didn't load. Upload it again with upload_image.");
      continue;
    }
    const drawn = canvas.toCanvas(img.getBoundingClientRect());
    const width = drawn.right - drawn.left;
    const height = drawn.bottom - drawn.top;
    if (width > img.naturalWidth * MAX_STRETCH || height > img.naturalHeight * MAX_STRETCH) {
      const tooWide = width > img.naturalWidth * MAX_STRETCH;
      const [axis, natural, size] = tooWide
        ? ['wide', img.naturalWidth, width]
        : ['tall', img.naturalHeight, height];
      add(
        'imageLowResolution',
        `This image is ${natural} px ${axis} but drawn ${whole(size)} px ${axis}, so it will look blurry. Upload one at least ${whole(size)} px ${axis}.`,
      );
    }
  }
  return found;
};
