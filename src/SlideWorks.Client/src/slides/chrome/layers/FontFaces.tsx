import { useEffect } from 'react';
import { addFontFaces } from '../../../look/fontsSettled';
import { useSlideLook } from '../lookContext';

// A look layer of the slide frame (T8): the template's uploaded fonts, one FontFace each, added to document.fonts
// once per file however many frames mount. It draws nothing. A file that fails to load is dropped and the slide
// falls back to the built-in stack, with no message. It draws from useSlideLook() alone.
export function FontFaces() {
  const { fonts } = useSlideLook();
  useEffect(() => {
    void addFontFaces(fonts);
  }, [fonts]);
  return null;
}
