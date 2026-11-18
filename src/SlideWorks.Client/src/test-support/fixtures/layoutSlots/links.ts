import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { LinksSlots } from '../../../slides/slots/links';

// One valid links slide's slots, with every optional slot set.
export const SLOTS: LinksSlots = {
  kicker: 'The cost',
  headline: 'Every slip we had last year started as a big batch',
  links: [
    {
      kind: 'Talk',
      title: 'Small batches, big wins',
      source: 'Conference keynote',
      url: 'https://example.com/talks/small-batches',
    },
    { title: 'The cost of a big batch', url: 'https://example.com/articles/big-batch' },
  ],
  qr: { url: 'https://example.com/small-batches', caption: 'example.com/small-batches' },
  emphasis: [{ text: 'big batch', tone: 'accent' }],
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = ['headline', 'links'] as const satisfies readonly RequiredKey<LinksSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<LinksSlots, typeof REQUIRED>>;
