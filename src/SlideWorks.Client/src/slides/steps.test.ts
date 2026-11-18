import stepCases from '@contracts/step-cases.json';
import { describe, expect, it } from 'vitest';
import type { BulletsSlots } from './slots';
import { calloutStep } from './steps';

type StepCase = { layout: string; slots: unknown; steps: number };

const bullets = (stepCases as StepCase[])
  .filter((c) => c.layout === 'bullets')
  .map((c) => ({ slots: c.slots as BulletsSlots, steps: c.steps }));

describe('calloutStep', () => {
  it('is 0 without a callout', () => {
    expect(calloutStep(3)).toBe(0);
    expect(calloutStep(0, undefined)).toBe(0);
  });

  it('is 0 when the callout shows with the slide or does not say', () => {
    expect(calloutStep(3, { tone: 'accent', text: 'a', reveal: 'with-slide' })).toBe(0);
    expect(calloutStep(3, { tone: 'accent', text: 'a' })).toBe(0);
  });

  it("the callout's step follows the others", () => {
    expect(calloutStep(3, { tone: 'accent', text: 'a', reveal: 'last' })).toBe(4);
    expect(calloutStep(0, { tone: 'accent', text: 'a', reveal: 'last' })).toBe(1);
  });

  it('has the shared bullets cases, with and without a last callout', () => {
    expect(bullets.length).toBeGreaterThanOrEqual(6);
    expect(bullets.filter((c) => c.slots.callout?.reveal === 'last').length).toBeGreaterThanOrEqual(
      2,
    );
  });

  it.each(bullets.map((c, i) => [i, c] as const))(
    'agrees with the server on shared bullets case %i',
    (_, { slots, steps }) => {
      const otherSteps = slots.reveal === 'one-by-one' ? slots.items.length : 0;
      expect(Math.max(otherSteps, calloutStep(otherSteps, slots.callout))).toBe(steps);
    },
  );
});
