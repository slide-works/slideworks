# Font fixtures

The only font files committed to this repository. Every font test reads them through `Support/FontFixtures.cs`; no test
reads a font from `node_modules` at run time. Do not add another font.

| File | Copied, byte for byte, from | Bytes |
|---|---|---|
| `normal.woff2` | `@fontsource/inter/files/inter-latin-400-normal.woff2` (Inter 400, the built-in body font) | 23,664 |
| `wide.woff2` | `@fontsource/inter/files/inter-latin-900-normal.woff2` (Inter 900, the widest font the client's packages hold) | 23,900 |

- Package: `@fontsource/inter`, version 5.3.0 (the version in the client's `pnpm-lock.yaml`).
- Licence: SIL Open Font License, Version 1.1 (OFL-1.1). Copyright 2016 The Inter Project Authors
  (https://github.com/rsms/inter). The licence text is the package's `LICENSE` file and https://openfontlicense.org.
