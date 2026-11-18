import { describe, expect, it } from 'vitest';
import { describeFlow } from './describeFlow';

describe('describeFlow', () => {
  it("names the nodes in order and the group's items", () => {
    expect(
      describeFlow({
        nodes: [
          { title: 'Collect', label: 'Step one' },
          { title: 'Clean', text: 'Fix each row', items: ['Verify', 'Strip'] },
          { title: 'Ship' },
        ],
      }),
    ).toBe(
      'Flow of 3 steps, left to right: Collect (Step one); then Clean, Fix each row, holding Verify, Strip; then Ship.',
    );
  });
});
