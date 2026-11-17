import { describe, expect, it } from 'vitest';
import { partsOf, type DeckPart } from './parts';

describe('partsOf', () => {
  it('a string reads as a part with its name as the short name', () => {
    expect(partsOf(['The cost', 'Slice it'])).toEqual([
      { name: 'The cost', short: 'The cost', caption: null, mark: null },
      { name: 'Slice it', short: 'Slice it', caption: null, mark: null },
    ]);
  });

  it('an object part reads as it is, among strings', () => {
    const appendix: DeckPart = { name: 'Appendix', short: 'More', caption: 'Numbers', mark: 'A' };
    expect(partsOf(['The cost', appendix])).toEqual([
      { name: 'The cost', short: 'The cost', caption: null, mark: null },
      appendix,
    ]);
  });

  it('no parts read as none', () => {
    expect(partsOf([])).toEqual([]);
  });
});
