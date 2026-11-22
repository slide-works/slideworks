import { describe, expect, it } from 'vitest';
import { defaultMonoFamily } from './FontsTab';

describe('defaultMonoFamily', () => {
  it('is specific to each template, even with the same name', () => {
    const one = defaultMonoFamily('Northwind kickoff', 'tpl_Nw4kT8pQz2Lm');
    const two = defaultMonoFamily('Northwind kickoff', 'tpl_Ab9xK3rVw7Hd');
    expect(one).toBe('Northwind kickoff pQz2Lm Mono');
    expect(two).not.toBe(one);
  });

  it('uses only the characters a family name allows, within 60', () => {
    const family = defaultMonoFamily('--Ev\'il, "na;me\\ ' + 'x'.repeat(80), 'tpl_Zz99Yy88Xx77');
    expect(family).toMatch(/^[A-Za-z0-9]([A-Za-z0-9 -]*[A-Za-z0-9])?$/);
    expect(family.length).toBeLessThanOrEqual(60);
    expect(defaultMonoFamily('!!!', 'tpl_Zz99Yy88Xx77')).toBe(
      'Xx77 Mono'.replace('Xx77', 'Yy88Xx77'.slice(-6)),
    );
  });

  it.each([
    ['- Northwind -', 'Northwind pQz2Lm Mono'],
    ['  --  A   B  --  ', 'A B pQz2Lm Mono'],
    ['!!! ---', 'pQz2Lm Mono'],
    ['Café Zürich', 'Caf Zrich pQz2Lm Mono'],
    ['日本語', 'pQz2Lm Mono'],
  ])('keeps a valid family for the name %j', (name, expected) => {
    const family = defaultMonoFamily(name, 'tpl_Nw4kT8pQz2Lm');
    expect(family).toBe(expected);
    expect(family).toMatch(/^[A-Za-z0-9]([A-Za-z0-9 -]*[A-Za-z0-9])?$/);
  });
});
