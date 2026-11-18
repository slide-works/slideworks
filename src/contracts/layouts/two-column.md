Surface: light. Steps: none.

## two-column

A headline over two columns. Each column holds a list of points, a paragraph or an image.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `left` | object | yes | exactly one of `items`, `text`, `image` | Exactly one of items, text or image |
| `left.title` | text | no | up to 50 chars |  |
| `left.items` | list of text or object | no | 1–6 items; each text up to 120 chars |  |
| `left.items[].lead` | text | yes | up to 40 chars |  |
| `left.items[].text` | text | yes | up to 120 chars |  |
| `left.text` | text | no | up to 400 chars |  |
| `left.image` | image `{image, alt}` | no |  |  |
| `left.image.image` | text | yes | up to 64 chars | The image id that upload_image returned |
| `left.image.alt` | text | yes | up to 500 chars | What the image shows, for screen readers |
| `right` | object | yes | exactly one of `items`, `text`, `image` | Exactly one of items, text or image |
| `right.title` | text | no | up to 50 chars |  |
| `right.items` | list of text or object | no | 1–6 items; each text up to 120 chars |  |
| `right.items[].lead` | text | yes | up to 40 chars |  |
| `right.items[].text` | text | yes | up to 120 chars |  |
| `right.text` | text | no | up to 400 chars |  |
| `right.image` | image `{image, alt}` | no |  |  |
| `right.image.image` | text | yes | up to 64 chars | The image id that upload_image returned |
| `right.image.alt` | text | yes | up to 500 chars | What the image shows, for screen readers |
