// A JSON import types its strings as `string`, not as literal unions such as
// 'link' | 'named', so it can't satisfy a view type directly. Widen<T> turns
// every literal union back into its primitive while keeping the shape: field
// names, nesting, arrays and value kinds. A JSON import is not a fresh object
// literal, so extra properties still pass; fixtures.test.ts checks exact key
// sets and the literal values at run time.
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

// tsc can't check a JSON slide's slots: the slides are an array, so a slide's
// type isn't tied to its `layout`, and widening the per-layout union would let
// any slots object pass as some layout's. So the JSON view types its slides'
// slots honestly as an opaque object, and checks every other field. The slots
// themselves are checked at run time against the sample deck, whose slides
// pass the server's SlideValidator (fixtures.test.ts).
type JsonSlide<S> = Widen<Omit<S, 'slots'>> & { slots: object };

export type JsonView<T> = {
  [K in keyof T]: K extends 'slides'
    ? T[K] extends readonly (infer S)[]
      ? JsonSlide<S>[]
      : never
    : Widen<T[K]>;
};
