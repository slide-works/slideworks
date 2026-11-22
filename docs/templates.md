# Templates

This page is for the person who makes a template, and for the agent that helps. A template is a deck's house style: the layouts it allows, its fonts, logos, title art, corner shape, colours and any layouts of its own. The built-in templates are `talk`, `workshop` and `update`. You can make as many of your own as you need, up to 50. Archived templates are not counted. For the new built-in layouts, see [`docs/layouts.md`](layouts.md).

Northwind, a made-up company, is the example brand on this page.

## Making a template

Open **Templates** in the top bar. The page lists three groups: **Yours**, **Shared with you** and **Built-in**.

- **New template** asks for a name (up to 60 characters) and a built-in template to start from. It opens the editor on a draft that is a copy of that built-in.
- **Upload a bundle…** makes a draft from a `.zip` file instead. See [Bundles](#bundles).

The editor has five tabs. Your changes save as you make them, and the header says **Saved** or **Not saved**.

| Tab | What it holds |
|---|---|
| **Basics** | The name, the short name, what the template is for, the layouts allowed, the chrome defaults, the outline and house style that agents write to, and the corner shape |
| **Fonts** | The font files and the confirmation that you may serve them |
| **Logo and art** | The logo for light slides and for dark slides, the title art and the ornament dots |
| **Palette** | The template's own colours |
| **Custom layouts** | Layouts you upload yourself. See [Custom layouts](#custom-layouts) |

A preview of three sample slides sits beside the tabs.

### Basics

- **Name** is what people see. **Short name** is shown to agents in `list_templates`. It uses lower-case letters, digits and hyphens, up to 40 characters. It is never an address: a template is always named by its `tpl_…` id.
- **Chrome defaults** are the footer (author, date or none), the part tracker, the progress bar, whether dividers list deep dives, the draft stamp on new decks, and **Accent bar across the top**, an 8 px bar of the accent colour across every slide.
- **Shape** sets the corner radius of cards and panels: **Square**, **8 px** or **16 px**.

### Fonts

A font file must be WOFF2. A template holds at most 8 font files, each up to 2 MB (2,097,152 bytes). A file has a role: heading, body or mono. The mono font is the monospace font of custom layouts and code.

Tick **I have the right to serve these fonts on the web** before you add a file. Fonts are sent to every person who sees a deck made with the template, so only a person can confirm this. An agent cannot. The tick covers every font in the template.

When you add a font, the server measures its average character width. A font must come out between 0.45 em and 0.70 em, or it is refused as too narrow or too wide for slide text. The server then uses that width when it checks that text fits. So the same text can fit on one template and be refused on another.

A role that has a regular weight and no bold one shows a to-do on the publish checklist.

### Logo and art

A logo and a title art image may be SVG, PNG or WebP, up to 10 MB each (10,485,760 bytes). An SVG is refused if it contains a script, a link to another file or a `foreignObject`, or if it can't be read. Export it again with everything inside, or use a PNG.

**Show the dark logo on title, divider and close slides** draws the dark logo on those slides. **Title art** is **None**, **Waves** or **An image**. **Ornament dots** are up to 6 colour dots, each named by a palette colour.

### Palette

A template can bring a palette of its own, or none. With none, a deck keeps the palette it picked. The palette tab edits every colour, in groups, with `accent-2` beside the accent. **Lock this palette** stops a deck from using another palette.

Every colour pair that text sits on is checked for contrast, and the Palette tab says **Every contrast pair passes** when they do. A pair needs 4.5 to 1, except the accent headline on paper, which needs 3.0 to 1 (the pairs and their bars are in the file below). A palette that fails still saves as a draft, but its checklist row fails and names the pair, and the template can't be published until you fix it. The list of pairs is [`src/contracts/contrast-pairs.json`](../src/contracts/contrast-pairs.json).

A palette can hold up to 4 **extra colours**, named `extra-1` to `extra-4`. Each has a colour and an `ink`, the text colour on it, and the ink must have 4.5 to 1 against the colour. Extra colours are used only by the ornament dots and by custom layouts.

### Publishing

The **Publish checklist** is on the editor. A row passes, fails or is still to do, and a row that is not passing has a **Fix** link to its tab.

| Row | It passes when |
|---|---|
| Fonts fit slide text | Every font the manifest names is an uploaded font file, and each role that has a regular weight (400) also has a bold one (700) |
| Font rights confirmed | A person ticked the confirmation |
| Logo and art are safe to serve | Every logo and art file is an uploaded SVG, PNG or WebP |
| Palette passes contrast | Every pair passes |
| Custom layouts are approved | Every layout named in the template was approved by its owner |
| Within the limits | The template is inside every limit below |

The button says **Publish version N**. It stays off until every row passes.

## Versions

Publishing adds a version, a whole number that starts at 1. A published version never changes. Editing a published template changes its draft only, until you publish again.

A deck stays on the version it was made with. When a newer version exists, the deck's owner sees an offer in the deck page's Look panel: **Update this deck**. The deck then says **Now on version N**. Stored slides are not rewritten.

After you publish, the page says how many of your decks are on an older version and offers **Update my N decks**, which moves every deck you own on this template to the latest version in one go. **Not now** leaves them. Other people's decks are left for their owners. Moving a deck back to an older version is allowed from the Look panel or `set_look`.

## Archiving

**Archive** on the template's row hides it. Nothing is deleted. Decks on an archived template keep rendering, and **Update my decks** still works. An archived template's draft can't be changed, and it no longer counts toward the limit of 50.

## Sharing

**Share** on the editor opens the same three choices as a deck:

- **Private**: only you can make decks with it.
- **Named people**: people you list by email can use it.
- **Anyone with the link**: anyone signed in who opens the link can add it to their templates.

The link is the template's page, `/t/{id}`. It shows two sample slides and a **Use this template** button, and a person who is signed out is asked to sign in first. A template that is shared with someone appears under **Shared with you**, with **View** on its row.

A person who opens a deck sees it in the template's fonts and logos, and does not need access to the template.

## Bundles

A bundle is a `.zip` with a `template.json` at the top and the files it names. **Upload a bundle…** on the Templates page makes a new draft from one. **Download** on one of your template rows writes one. Only the owner can download.

A download holds `template.json` and these files:

- `fonts/<file>` for each font
- `logo-light.<ext>`, `logo-dark.<ext>` and `art.<ext>` for the logos and art
- `layouts/<name>.html`, `.css` and `.json` for each custom layout

The manifest names files by their path in the zip. Its shape is [`src/contracts/template-manifest.schema.json`](../src/contracts/template-manifest.schema.json), and [`src/contracts/template-manifest.sample.json`](../src/contracts/template-manifest.sample.json) is a full Northwind example. An import checks everything before it writes anything. A bundle holds at most 64 files and 60 MB once unpacked.

Font rights are not part of a bundle. A person still ticks the confirmation in the Fonts tab before the template can be published.

## Limits

| Limit | Value |
|---|---|
| Templates a person can have | 50 (archived ones are not counted) |
| Font files in a template | 8 files, each up to 2 MB |
| A logo or a title art file | 10 MB |
| Custom layouts in a template | 12 |
| Extra colours | 4 |
| Ornament dots | 6 |
| A bundle once unpacked | 64 files, 60 MB |
| Everything one template has ever stored | 256 files, 240,000,000 bytes |

A stored file is never removed, because a published version's files must stay. If a template is full, start a new one.

## For agents

An agent can make, change and publish a template, and can upload a template's files. It cannot do three things. Each needs a person:

- confirm the right to serve fonts (the **Fonts** tab),
- approve a custom layout (its review page),
- archive a template (the **Archive** button on its row).

An agent can share a template with `set_sharing`, giving `template` instead of `deck`.

### The tools, in the order an agent calls them

1. `list_templates` returns the built-in templates and your own. Each entry has a `ref`: the built-in's name, or an uploaded template's `tpl_…` id. Use the `ref` wherever a tool takes a template.
2. `create_template` makes a draft: a `name` (up to 60 characters; the short name is made from it, so "Northwind kickoff" becomes `northwind-kickoff`), `from` (a built-in's name) and an optional `manifest` of fields that change. It returns the `template` id, the editor's `url` and the checklist.
3. `upload_asset` starts an upload for a `font`, `logo`, `art` or `layout`. It returns an `upload_url` that works once and a `curl` line. Run the line. The answer to the upload has the `assetId`.
4. `update_template` changes the draft. Give only the fields that change: each top-level key replaces the stored value, and a key set to `null` is refused. Name files by the `assetId`. Pass the `revision` you last read, and a stale one is refused. It returns the new revision and the checklist.
5. `get_template` reads the draft: its manifest, files, checklist, sharing and the number of decks on it.
6. `publish_template` publishes the next version once every check passes. A failed check is refused with what to fix, such as `Can't publish yet: extra-1 needs 4.5:1; these colours give 1.1:1.`
7. Then, for a deck: `create_deck` with `template`, `set_look` with `template_version` (a whole number, or `"latest"`), and `list_layouts` with a `template` or a `deck` to see the layouts, custom ones included.

A checklist entry is `{ "check": "palette", "ok": true, "detail": "" }`. The checks are `fonts`, `fontRights`, `logoArt`, `palette`, `layouts` and `limits`.

### A worked session

An agent makes a Northwind template, gives it a logo and a second accent colour, publishes it and moves a deck onto it.

Start from `talk`, with the top bar on:

```json create_template
{
  "name": "Northwind kickoff",
  "from": "talk",
  "manifest": {
    "purpose": "Northwind project kickoffs.",
    "chrome": {
      "tracker": true,
      "footer": "author",
      "progress": true,
      "deepDivesOnDivider": true,
      "draft": true,
      "topBar": "accent"
    }
  }
}
```

Start the logo upload, then run the `curl` line it returns:

```json upload_asset
{
  "template": "tpl_Kx3mQ9",
  "kind": "logo",
  "filename": "northwind-logo.svg"
}
```

Name the logo by the `assetId` the upload answered. `logos` needs `showOnDark` too:

```json update_template
{
  "template": "tpl_Kx3mQ9",
  "manifest": {
    "logos": { "light": "a1b2c3d4e5f60718293a4b5c6d7e8f90", "showOnDark": false }
  }
}
```

A palette needs every colour token, and `list_palettes` returns a built-in palette's tokens to start from. Send the whole palette back with your changes. This is the part that changes: a second accent, and one extra colour with its ink.

```json palette-fragment
{
  "accent-2": "#0e7490",
  "extras": [{ "name": "extra-1", "colour": "#b45309", "ink": "#ffffff" }]
}
```

Check the draft, then publish it:

```json get_template
{ "template": "tpl_Kx3mQ9" }
```

```json publish_template
{ "template": "tpl_Kx3mQ9" }
```

The answer is the template, `"version": 1` and the editor's url. Later, after publishing version 2, a deck on version 1 moves with:

```json set_look
{ "deck": "DECK_ID", "template_version": "latest" }
```

## Custom layouts

A custom layout is a slide layout you write yourself, for one template. It has three files:

- the markup (`layout.html`): the boxes and where each slot goes,
- the CSS (`layout.css`): how they look,
- the slots (`slots.json`): the name, a one-line description, the surface, the slots and their limits, and the steps.

They are sent as one JSON document, `{ "markup": …, "css": …, "slots": … }`, uploaded with `upload_asset` (or the Custom layouts tab), then listed in the template's `customLayouts` as `{ "name", "file" }`, where `file` is the asset id. In a bundle the three files are `layouts/<name>.html`, `.css` and `.json`.

The server reads a layout strictly. It compiles the markup and the CSS to a safe form and refuses anything outside the rules below. A layout can't run script, load a file, write a colour or name a font.

### The markup

- Tags: `div`, `span`, `p`, `h2`, `h3`, `ul`, `ol`, `li`, `b`, `i`, `em` and `strong`.
- Attributes: `class` and six bindings (below). Every value is in double quotes.
- No text, no comments and no `<style>` in the markup. Text goes in a slot.
- A class name uses lower-case letters, digits and hyphens. A layout's classes are private to it: the server renames each one, so two layouts never clash.
- Elements nest up to 12 deep, there are at most 400 elements as written and 2,000 once every list is drawn, and an element has at most 8 classes.

| Binding | Does |
|---|---|
| `data-slot="name"` | Puts a slot's text, or a block slot, in this element |
| `data-each="list"` | Repeats this element once for each item of a list slot. Inside it, bindings name the item's fields |
| `data-if="name"` | Draws this element only when that optional slot is set |
| `data-var="--p: progress"` | Sets a CSS variable (a name that starts with `--`, never `--sw-`) from a number slot |
| `data-variant="state"` | Adds the class `is-<value>` for the value of an enum slot |
| `data-step="each"` | On the repeated element of the layout's `steps` list, reveals one item per step |
| `data-step="2"` | A number from 1 to 20 reveals this element on that step. Not inside a `data-each` |

### The CSS

- Class selectors only.
- Properties, functions, units and keywords only from [`src/contracts/custom-layout-allow.json`](../src/contracts/custom-layout-allow.json). It has no `transition` and no `animation`, so a layout has no motion beyond step reveals. Motion comes later.
- Colours and fonts only from the template's tokens, the `--sw-…` names in the same file: `var(--sw-accent)`, `var(--sw-font-mono)`. A colour written as a value is refused.
- No comments, no at-rules and no `!important`.
- At most 300 rules, and 40 declarations in a rule.

### The slots

`slots.json` has a `name` (2 to 31 characters: lower-case letters, digits and single hyphens), a `description` (up to 200 characters), a `surface` (`light`, `dark` or `either`), optional `steps` and the `slots`. A slot is one of:

- `text`, with a `max` of 1 to 500 characters,
- `number`, with a `min` and a `max`,
- `enum`, with up to 8 `values`,
- `list`, of 1 to 12 items, with a `min`, a `max` and `of`, the fields of one item,
- `image`, which must be `"optional": true`,
- a block: `callout`, `chip`, `badge`, `levels` or `facts`, as in [`list_blocks`](../src/contracts/layouts/_blocks.md).

A slot is required unless it says `"optional": true`. Slot names use lower-case letters, digits and underscores. There are at most 24 slots, counting the fields of a list. Lists nest twice at most.

A layout has no steps, list steps or fixed steps, never both kinds. For list steps, `steps` names a top-level list slot, and the markup puts `data-step="each"` on the outermost repeated element of that list, once. For fixed steps, leave `steps` out and number the elements with `data-step`, from 1 with no gaps. The steps in the markup and in `slots.json` must agree. A layout with steps gets a `reveal` slot added for you, so don't write one. A slide on it reveals one step at a time when `reveal` is `"one-by-one"`.

A layout's whole upload is up to 800,000 bytes, and each of the three parts up to 262,144 bytes.

### An example

This layout, `launch-lanes`, draws a headline over 2 to 4 lanes, each with a progress bar. It is a complete layout. It was uploaded, approved, published and used in a deck.

`layout.html`:

```html
<div class="board">
  <div class="headline" data-slot="headline"></div>
  <div class="lanes">
    <div class="lane" data-each="lanes" data-step="each">
      <span class="name" data-slot="name"></span>
      <div class="track">
        <div class="fill" data-var="--p: progress" data-variant="state"></div>
      </div>
    </div>
  </div>
  <p class="note" data-if="note" data-slot="note"></p>
</div>
```

`layout.css`:

```css
.board {
  display: flex;
  flex-direction: column;
  gap: var(--sw-space-5);
  color: var(--sw-ink);
}
.headline {
  font-size: 64px;
  font-weight: 700;
}
.lanes {
  display: flex;
  flex-direction: column;
  gap: var(--sw-space-4);
}
.lane {
  display: flex;
  align-items: center;
  gap: var(--sw-space-4);
}
.name {
  width: 360px;
  font-size: 32px;
}
.track {
  flex-grow: 1;
  height: 48px;
  border-radius: var(--sw-radius-sm);
  background: var(--sw-chip);
}
.fill {
  width: calc(var(--p) * 1%);
  height: 100%;
  border-radius: var(--sw-radius-sm);
  background: var(--sw-accent);
}
.fill.is-late {
  background: var(--sw-bad);
}
.note {
  font-size: 28px;
  color: var(--sw-muted);
}
```

`slots.json`:

```json
{
  "name": "launch-lanes",
  "description": "A headline over 2 to 4 launch lanes, each with a progress bar. Reveals one lane per step.",
  "surface": "light",
  "steps": "lanes",
  "slots": {
    "headline": { "type": "text", "max": 80 },
    "lanes": {
      "type": "list",
      "min": 2,
      "max": 4,
      "of": {
        "name": { "type": "text", "max": 30 },
        "progress": { "type": "number", "min": 0, "max": 100 },
        "state": { "type": "enum", "values": ["on-track", "late"] }
      }
    },
    "note": { "type": "text", "max": 120, "optional": true }
  }
}
```

Put the three into one document and upload it:

```sh
jq -n --rawfile markup layout.html --rawfile css layout.css --slurpfile slots slots.json \
  '{markup: $markup, css: $css, slots: $slots[0]}' > layout.json
```

Ask `upload_asset` for `kind: "layout"` and run its `curl` line on `layout.json`. Then list the layout in the template:

```json update_template
{
  "template": "tpl_Kx3mQ9",
  "manifest": {
    "customLayouts": [{ "name": "launch-lanes", "file": "a1b2c3d4e5f60718293a4b5c6d7e8f90" }]
  }
}
```

After the owner approves it and the template is published, a deck on that version can use it:

```json launch-lanes
{
  "headline": "Three launches, one is late",
  "lanes": [
    { "name": "Payments", "progress": 100, "state": "on-track" },
    { "name": "Search", "progress": 60, "state": "on-track" },
    { "name": "Mobile", "progress": 25, "state": "late" }
  ],
  "note": "Mobile waits on app store review.",
  "reveal": "one-by-one"
}
```

That is the `slots` argument of `add_slide` with `layout: "launch-lanes"`. `list_layouts` lists the layout with `custom: true`, after the built-ins.

### Review and approval

A layout can't be used until its owner has approved it in the app. Open the template's **Custom layouts** tab, choose **Upload a custom layout** and pick the one `.html`, one `.css` and one `.json` file. The tab lists each layout as **Needs review** or **Approved**. **Review** opens the review page, `/templates/{id}/layouts/{name}`.

The page draws the layout in the template's own fonts and colours:

- a sample slide with typical content, and on the dark surface too when the layout takes it,
- for a layout with steps, the slide halfway through them,
- a slide with every slot at its maximum, inside a dashed safe area.

It then checks the layout:

- **Safe area.** Everything the layout draws must sit inside x 120 to 1800 and y 150 to 996, in whole pixels on the 1920 by 1080 slide. It lists each box that doesn't.
- **Contrast.** Text must have 4.5 to 1 against what is behind it, or 3.0 to 1 from 28 px. It checks light slides and, where the layout takes it, dark ones.

**Approve layout** is on only when every check is clear. An approved layout is tied to its files: change one and the layout needs review again. An agent can't approve, and `publish_template` refuses a template with a layout that isn't approved.

The contrast check reads the backgrounds of a text's ancestors only. Text that sits over a separately positioned box, such as a badge placed on top of a picture, is not measured. Check those by eye.

## What a refusal means

A refusal comes back with a code and the message below. `{…}` is filled in with the details.

### Templates, files and publishing

| Message | What it means |
|---|---|
| Fonts must be woff2 files. This one is {found}. | The file is not WOFF2. `{found}` says what it looks like: TrueType, OpenType, woff 1, damaged or not a font. Convert it to WOFF2 |
| This font is {problem} for slide text ({width} em; the limit is {limit}). | The measured width is outside 0.45 to 0.70 em. Pick a font with ordinary proportions |
| This SVG contains a script. Export it again without scripts, or upload a PNG. | A script in an SVG is never served |
| This SVG links to another file. Export it again with everything inside the file, or upload a PNG. | An SVG can't load an outside file |
| This SVG contains a foreignObject. Export it again without it, or upload a PNG. | `foreignObject` can hold HTML, so it is refused |
| This SVG can't be read. Export it again, or upload a PNG. | The file isn't valid SVG |
| Logos and art must be SVG, PNG or WebP files. | The file is another type |
| {pair} needs {needed}; these colours give {ratio}. | A palette pair fails contrast, for example `body needs 4.5:1; these colours give 1.0:1.` It shows on the checklist and stops publishing. Change one of the two colours |
| This layout has not been approved by the template's owner. | Publishing needs every layout approved first |
| this layout was refused; see errors | The upload broke a rule. The `errors` list gives each finding, with its path |
| A template can hold at most 8 font files. | The font limit |
| A template can hold at most 12 custom layouts. | The layout limit |
| A palette can hold at most 4 extra colours. | The extra colour limit |
| You can have at most 50 templates. Archive one to make room. | The template limit |
| This file is too large. The limit is {limit}. | A font is over 2 MB, a logo or art file over 10 MB, a layout over 800,000 bytes, or a bundle over its limit |
| Confirm you can serve these fonts. Only a person can confirm this, in the template editor's Fonts tab. | An agent tried to publish with fonts whose rights nobody confirmed |
| This template has changed since you read it. Read it again, then retry. | The `revision` is stale. Call `get_template`, then change the new draft |
| The template manifest is not valid: {reason} | A field is missing, has the wrong shape or is `null` |
| This template has no such file. Upload it first. | The manifest names an asset id that isn't in this template |
| This bundle can't be read: {reason} | The zip, its manifest or one of its files is invalid |
| This bundle is too large. A bundle can hold at most 64 files and 60 MB once unpacked. | The bundle limit |
| This file can't be checked right now because the renderer is not available. Try again in a minute. | Fonts are measured in a browser. A font is never accepted unmeasured |
| This upload has already been used. Upload the file again. | An `upload_url` works once. Ask `upload_asset` for a new one |
| This template has not been published yet. | The action needs a published version |
| This template has stored too many files. Start a new template, or ask for unused files to be removed. | The 256-file or 240,000,000-byte limit |
| Too many uploads are in progress for this template. Finish or wait for them to expire. | Finish or wait out the open uploads |
| This template is archived, so its draft can't be changed. | Archived templates are read-only |
| This layout was not found. | No custom layout has this name in the template |
| This layout has changed since it was reviewed. Review it again. | The files changed after the page measured them |
| This layout's content does not fit inside the slide. | A box is outside the safe area |
| This layout's text does not have enough contrast with what is behind it. | A text run fails contrast |

### Custom layout files

Each finding has a path to the file and the place that broke the rule.

| Message | What it means |
|---|---|
| The markup is too large. / The CSS is too large. / The slots file is too large. | A part is over 262,144 bytes |
| The markup can't be read. Check that every tag is closed and every attribute value is in double quotes. | Close every tag and quote every value |
| This tag is not allowed in a layout. | Use only the twelve tags |
| This attribute is not allowed here. | Use only `class` and the six bindings |
| Text can't be written in the markup. Put it in a slot. | Remove the text between tags |
| Comments are not allowed in the markup. | Remove the comment |
| This class name is not allowed. Use lower-case letters, digits and hyphens. | Rename the class |
| This binding can't be read. | A `data-` value is not in its exact form |
| The markup is nested too deeply. | More than 12 levels |
| The markup has too many elements. | More than 400, or 2,000 drawn |
| The CSS can't be read. | A syntax error |
| Comments are not allowed in the CSS. | Remove the comment |
| This at-rule is not allowed. | No `@media`, `@import` or `@font-face` |
| This selector is not allowed. Use class selectors only. | Write `.name`, not a tag, an id or an attribute selector |
| This property is not allowed. | Not in the allow file |
| This value is not allowed for this property. | The property can't take this value or unit |
| This function is not allowed. | Not in the allow file |
| Colours must come from the template's tokens. | Use `var(--sw-…)` |
| Fonts must come from the template's tokens. | Use `var(--sw-font-head)`, `-body` or `-mono` |
| This variable is neither a token nor a variable the markup binds. | A `var()` names something that isn't a token or a `data-var` |
| !important is not allowed. | Remove it |
| The CSS has too many rules. | More than 300 rules, or 40 declarations in one |
| The slots file can't be read. It must be one JSON object. | Not valid JSON |
| This name is not allowed. Layout names use lower-case letters, digits and single hyphens; slot names use lower-case letters, digits and underscores. | Rename it |
| The layout needs a one-line description. | Add `description`, up to 200 characters |
| The surface must be light, dark or either. | Fix `surface` |
| This key is not known here. | A key the slot type doesn't take |
| This slot type is not known or not allowed here. | An unknown type, or a type in a place it can't go |
| This limit is missing or out of range. | A `max`, `min` or `values` is missing or outside its range |
| Steps must name a top-level list slot. | `steps` names something else |
| Lists are nested too deeply. | More than two levels |
| The layout has too many slots. | More than 24 |
| This binding names a slot the layout does not have. | A typo, or a field used outside its list |
| This binding needs a slot of another type. | For example `data-var` on a text slot |
| This slot is not used in the markup. | Every top-level slot needs a binding |
| The steps in the markup do not match the steps in the slots file. | `steps` and `data-step` must agree |
