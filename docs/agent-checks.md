# Checking a deck as an agent

`check_deck` looks at slides the way a viewer will: it renders each one in a browser and measures what is drawn. `preview_deck` returns the same findings for the slides it rendered, next to their images. Fix every `error` and `warning`, then check again. A `note` asks you to look at the image; it needs no fix.

## The call

`check_deck` takes `deck` and, optionally, `slides` (slide numbers; all of them when left out). It returns:

```json
{ "deck": "...", "slides": [1, 2], "findings": [{ "slide": 2, "rule": "overflow", "severity": "error", "source": "measured", "where": "...", "message": "..." }], "measured": true, "error": null }
```

- `measured: true` means every requested slide was rendered, or reported as `renderFailed`. When it is false, read `error`: the renderer was unavailable or timed out. The findings then hold only the stored (write-time) checks, so call again.
- Enums are camelCase. Findings are sorted by severity, then by rule in the order of the table below.

## Rules and what to do

| Rule | Severity | Meaning | What to do |
| --- | --- | --- | --- |
| `overflow` | error | Content is drawn past the slide, or below the footer. | Shorten the text, or split the slide. |
| `fit` | error | The text no longer fits the layout, for example after `set_look` chose wider fonts. | Shorten it, or pick a layout with more room. |
| `imageNotLoaded` | error | An image did not load in the browser. | Upload it again and update the slide. |
| `renderFailed` | error | The slide's page failed to render, for example an image that does not decode. The other slides are still checked. | Replace the broken image or content, then check again. |
| `smallText` | warning | Text is drawn under 20 px on the 1920x1080 canvas. | Use less text or a layout with larger type. |
| `lowContrast` | warning | Text fails WCAG AA against its solid background (4.5:1 under 28 px, 3.0:1 from 28 px). | Pick a palette with more contrast, or another layout. |
| `imageLowResolution` | warning | An image is drawn more than 1.5 times its natural size. | Upload a larger image. |
| `genericAlt` | warning | The alt text is generic: "image", "photo", a file name or the image id. | Describe what the image shows. |
| `layoutOutsideTemplate` | warning | The slide's layout is not one of its template's layouts. | Use a layout from `create_deck`'s list. |
| `textOnImage` | note | Text sits over an image or gradient, which is not measured. | Look at the PNG from `preview_deck`. |

## A fix pass

1. `check_deck` once the deck is built.
2. Fix the errors first, then the warnings, with `update_slide`.
3. `preview_deck` for the slides you changed, and look at every PNG.
4. `check_deck` again, until there are no errors or warnings.

The deck page shows the same problems to the deck's owners and editors: a count on each thumbnail and a Checks list under the selected slide.
