Surface: dark. Steps: none.

## divider

Section divider (dark) that opens a part. Its number and the journey come from the deck's parts and the slide's part. When the template lists deep dives on dividers (workshop does), the divider lists that part's deep dives.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `title` | text | yes | up to 44 chars | Set at 104 px: two lines at most, shrinking to 64 px to keep them |
| `sub` | text | no | up to 110 chars |  |
| `image` | image `{image, alt}` | no |  | A full-bleed photo behind the title. The title stays on top |
| `image.image` | text | yes | up to 64 chars | The image id that upload_image returned |
| `image.alt` | text | yes | up to 500 chars | What the image shows, for screen readers |
