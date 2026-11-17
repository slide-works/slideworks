Dark: no. Steps: none.

## image

A headline over one large image, with optional points beside it and a caption.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `image` | image `{image, alt}` | yes |  |  |
| `image.image` | text | yes | up to 64 chars | The image id that upload_image returned |
| `image.alt` | text | yes | up to 200 chars | What the image shows, for screen readers |
| `points` | list of text | no | 1–4 items; each up to 80 chars |  |
| `caption` | text | no | up to 140 chars |  |
