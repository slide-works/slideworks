// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { emphasisFor, emphasisPieces } from './emphasis';
import type { Emphasis } from './slots/blocks';

const accent = (text: string, where?: string): Emphasis =>
  where === undefined ? { text, tone: 'accent' } : { text, tone: 'accent', in: where };

describe('emphasisFor', () => {
  it('gives no entries when the slide has no emphasis', () => {
    expect(emphasisFor({ headline: 'Small batches ship' }, 'headline')).toEqual([]);
  });

  it('gives the entries that name the target', () => {
    const slots = {
      headline: 'Small batches ship',
      sub: 'Small batches ship sooner',
      emphasis: [accent('ship', 'sub'), accent('Small', 'headline')],
    };
    expect(emphasisFor(slots, 'headline')).toEqual([accent('Small', 'headline')]);
    expect(emphasisFor(slots, 'sub')).toEqual([accent('ship', 'sub')]);
  });

  it('gives an entry with no target to the slot whose text holds its phrase', () => {
    const slots = { headline: 'Small batches', quote: 'Big ones slip', emphasis: [accent('slip')] };
    expect(emphasisFor(slots, 'quote')).toEqual([accent('slip')]);
    expect(emphasisFor(slots, 'headline')).toEqual([]);
  });

  it('reads a dotted path', () => {
    const slots = {
      headline: 'Two ways',
      failure: { label: 'Before', line: 'It slipped twice' },
      emphasis: [accent('twice'), accent('slipped', 'failure.line')],
    };
    expect(emphasisFor(slots, 'failure.line')).toEqual([
      accent('twice'),
      accent('slipped', 'failure.line'),
    ]);
    expect(emphasisFor(slots, 'failure.label')).toEqual([]);
  });

  it('gives nothing for a target that is not set or is not text', () => {
    const slots = { headline: 'Two ways', items: ['Two'], emphasis: [accent('Two')] };
    expect(emphasisFor(slots, 'sub')).toEqual([]);
    expect(emphasisFor(slots, 'items')).toEqual([]);
    expect(emphasisFor(slots, 'headline.length')).toEqual([]);
    expect(emphasisFor(slots, 'constructor.name')).toEqual([]);
  });
});

describe('emphasisPieces', () => {
  it('is the whole text when there are no spans', () => {
    expect(emphasisPieces('Small batches', [])).toEqual([{ text: 'Small batches' }]);
    expect(emphasisPieces('', [])).toEqual([{ text: '' }]);
  });

  it('wraps the phrase once, at its first place', () => {
    expect(emphasisPieces('ship it, then ship it', [accent('ship')])).toEqual([
      { text: 'ship', tone: 'accent' },
      { text: ' it, then ship it' },
    ]);
  });

  it('takes the spans in the order of the text', () => {
    const spans: Emphasis[] = [
      { text: 'slip', tone: 'negative' },
      { text: 'Small', tone: 'positive' },
    ];
    expect(emphasisPieces('Small ones never slip', spans)).toEqual([
      { text: 'Small', tone: 'positive' },
      { text: ' ones never ' },
      { text: 'slip', tone: 'negative' },
    ]);
  });

  it('finds a phrase of pattern characters as plain text', () => {
    const phrase = '.* (a+)+$ [x] \\d ^';
    expect(emphasisPieces(`cost ${phrase} more`, [accent(phrase)])).toEqual([
      { text: 'cost ' },
      { text: phrase, tone: 'accent' },
      { text: ' more' },
    ]);
    expect(emphasisPieces('abc', [accent('.')])).toEqual([{ text: 'abc' }]);
  });

  it('leaves a phrase that is not there, an empty one and an overlapping one as text', () => {
    expect(emphasisPieces('Small batches', [accent('big'), accent('')])).toEqual([
      { text: 'Small batches' },
    ]);
    expect(emphasisPieces('Small batches', [accent('Small b'), accent('batches')])).toEqual([
      { text: 'Small b', tone: 'accent' },
      { text: 'atches' },
    ]);
  });

  it('always joins back to the text', () => {
    const text = 'Every slip started as a big batch';
    const spans = [accent('slip'), accent('big batch'), accent('Every')];
    expect(
      emphasisPieces(text, spans)
        .map((piece) => piece.text)
        .join(''),
    ).toBe(text);
  });
});
