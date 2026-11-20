// Compile-time checks that a layout's key tables can't drift from its slots type (K5).

// Fails to compile unless T is exactly `true`.
export type AssertTrue<T extends true> = T;

// true when Listed names every key of Slots.
export type ListsEveryKey<Slots, Listed extends readonly PropertyKey[]> = [
  Exclude<keyof Slots, Listed[number]>,
] extends [never]
  ? true
  : false;

// The keys of Slots that aren't optional.
export type RequiredKey<Slots> = {
  [K in keyof Slots]-?: Record<never, never> extends Pick<Slots, K> ? never : K;
}[keyof Slots];

// true when Listed names every required key of Slots.
export type ListsEveryRequiredKey<Slots, Listed extends readonly PropertyKey[]> = [
  Exclude<RequiredKey<Slots>, Listed[number]>,
] extends [never]
  ? true
  : false;
