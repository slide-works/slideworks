// A JSON import types its strings as `string`, not as literal unions such as 'pending' | 'approved', so it can't
// satisfy a view type directly. Widen<T> turns every literal union back into its primitive while keeping the shape
// (the same pattern as the SPA's test-support/fixtures/widen.ts). builders.test.ts checks the literal values at run time.
export type Widen<T> = T extends string
  ? string
  : T extends number
    ? number
    : T extends boolean
      ? boolean
      : T extends null
        ? null
        : T extends readonly (infer U)[]
          ? Widen<U>[]
          : T extends object
            ? { [K in keyof T]: Widen<T[K]> }
            : T;
