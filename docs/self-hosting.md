# Self-hosting SlideWorks

This guide takes a fresh server from `docker compose up` to an agent building a deck. SlideWorks is one container: the ASP.NET Core server, the React SPA it serves, a SQLite database and the image files, all on one data volume.

## What you need

- A host with Docker and Docker Compose.
- A public URL for SlideWorks, such as `https://slides.example.com`, with a TLS proxy in front of the container (Caddy, nginx, Traefik or a cloud load balancer). For a quick trial on one machine, `http://localhost:8080` works without a proxy.
- A Google Cloud project, to create an OAuth client for "Sign in with Google". People sign in with Google only.

## Configuration

Every setting is an environment variable with the `SLIDEWORKS_` prefix, except `RenderTokens__Key`.

| Variable | Required | What it does |
|---|---|---|
| `SLIDEWORKS_PUBLIC_URL` | Yes, outside Development | The URL people and agents reach SlideWorks at, such as `https://slides.example.com`. It's the app's only source for its own scheme and host: deck links, the OAuth and MCP metadata, the sign-in callback, the `Secure` flag on the session cookie, and the cross-site write guard all use it. The server refuses to start without it outside Development, and refuses a value that isn't an absolute `http` or `https` URL. Serve SlideWorks at the root of its host, because a path in this URL isn't used as a base path. |
| `SLIDEWORKS_JOIN_URL` | No | A short domain for the join links an audience scans or types, such as `https://slideworks.live`. It must be an absolute `http` or `https` origin with no path, query or fragment, and the server refuses to start with anything else. Join links, the QR code and the address a join slide prints come from it, never from a request's `Host` header. Unset, join links are `<SLIDEWORKS_PUBLIC_URL>/j/<code>`, and slides print the public host with `/j`. |
| `SLIDEWORKS_GOOGLE_CLIENT_ID` | For sign-in | The Google OAuth client's ID. |
| `SLIDEWORKS_GOOGLE_CLIENT_SECRET` | For sign-in | The Google OAuth client's secret. Sign-in is on only when both Google values are set. With neither set, the server still runs, `/signin` says sign-in isn't set up, and `/signin/google` answers 503. With only one set, the server logs a warning and sign-in stays off. |
| `SLIDEWORKS_ADMIN_EMAIL` | Recommended | The Google email of the first admin. When that person signs in and no admin exists yet, they become an approved admin. It's honoured only while no admin exists, so it's harmless to leave it set. If it's unset and there is no admin, the server logs a warning at startup, because nobody can approve sign-ups. |
| `SLIDEWORKS_DB_PATH` | No | The SQLite file. Default `/data/slide-works.db`. The session-cookie keys are kept in `slide-works-keys/` next to it, so a restart or a new image doesn't sign everyone out. |
| `SLIDEWORKS_DATA_PATH` | No | The data folder for uploaded files. Default `/data`. Deck images are stored under `images/<deck id>/` and uploaded templates' files under `templates/<template id>/` (see [Uploaded templates](#uploaded-templates)) Profile photos are stored under `avatars/<user id>/`. |
| `SLIDEWORKS_SMTP_HOST` | For share emails | The SMTP server that sends the email a person gets when a deck is shared with them. Any SMTP server works. With no host set, no emails are sent: sharing still works and the person finds the deck under "Shared with you". See [Share email](#share-email). |
| `SLIDEWORKS_SMTP_PORT` | No | The SMTP port, with STARTTLS. Default `587`. |
| `SLIDEWORKS_SMTP_USERNAME` | No | The SMTP username, when the server needs one. |
| `SLIDEWORKS_SMTP_PASSWORD` | No | The SMTP password. `src/docker-compose.yml` fills it from `RESEND_KEY` when it isn't set. |
| `SLIDEWORKS_MAIL_FROM` | For share emails | The From of share emails, such as `SlideWorks <share@mail.example.com>`, on a domain your SMTP server may send from. |
| `SLIDEWORKS_SHARE_NEW_PER_HOUR` | No | How many distinct new people one person may add to their decks in an hour. Default `10`. Each email counts once per window, however many decks it is added to; changing a person's level or removing them doesn't count. |
| `SLIDEWORKS_SHARE_NEW_PER_DAY` | No | The same limit over 24 hours. Default `30`. |
| `SLIDEWORKS_SHARE_EMAILS_PER_RECIPIENT_PER_DAY` | No | How many share emails one address may get in 24 hours, from everyone together. Default `3`. |
| `SLIDEWORKS_SEQ_URL` | No | Also send logs to this Seq server. Without it, logs go to the console only (`docker logs`). |
| `SLIDEWORKS_SEQ_API_KEY` | No | The Seq API key, when your Seq server needs one. |
| `SLIDEWORKS_TRUSTED_PROXIES` | Behind a proxy | The proxy addresses whose `X-Forwarded-For` header SlideWorks trusts for the client IP, as a comma-separated list of CIDRs such as `172.18.0.0/16, fd00::/8`. A single address, such as `10.0.0.5`, means just that address. Unset by default, which trusts no proxy. The server refuses to start if an entry isn't a CIDR or an address. See [Behind a TLS proxy](#behind-a-tls-proxy). |
| `SLIDEWORKS_IDENTITY_PROXY` | No | Set `true` when an identity-aware proxy sits in front of SlideWorks and blocks calls that carry no proxy login, as the phone app's calls do. The server then answers `POST /api/auth/device-sessions/codes` with 400 `identity_proxy` and a message, and the app's confirm page and the app show that message instead of signing in. SlideWorks can't detect such a proxy, so this is an explicit setting. Default unset (off). See [Identity-aware proxies](#identity-aware-proxies). |
| `SLIDEWORKS_APPLE_APP_ID` | No | The iOS app id, `<Team ID>.<bundle id>`, such as `ABCDE12345.app.slideworks.mobile`. When set, `/.well-known/apple-app-site-association` serves it, claiming `/j/*` on the public host, and `/j/*` plus six-character paths on the join host, so a join link opens the app. Unset, that file answers 404 and join links open the browser. See [Phone app links](#phone-app-links). |
| `SLIDEWORKS_ANDROID_CERT_SHA256` | No | A comma-separated list of colon-hex SHA-256 fingerprints of the Android app's **Play app-signing** certificate (not the upload key). When set, `/.well-known/assetlinks.json` serves them for package `app.slideworks.mobile`. Unset, that file answers 404. See [Phone app links](#phone-app-links). |
| `RenderTokens__Key` | Yes, outside Development | Signs the short-lived tokens the share-card browser and agents' slide previews use. A random secret of at least 32 characters, such as `openssl rand -base64 32`. The server refuses to start without it outside Development. It's the one setting without the `SLIDEWORKS_` prefix; `docker-compose.yml` fills it from `SLIDEWORKS_RENDER_TOKEN_KEY`. Changing it only expires preview links already handed out. |
| `SLIDEWORKS_RENDER_URL` | No | The origin the share-card browser inside the container opens. Unset means the app's own loopback address. Set it only when the app can't reach itself that way. |
| `SLIDEWORKS_RENDER_TIMEOUT` | No | How long one share-card render may take, as a TimeSpan. Default `00:00:20`. |

The image listens on port 8080 over plain HTTP and runs as a non-root user. `GET /health` is anonymous and reports the commit the image was built from.

## 1. Create the Google OAuth client

1. In the Google Cloud console, open **APIs & Services → Credentials** and create an **OAuth client ID** of type **Web application**.
2. Under **Authorised redirect URIs**, add `<SLIDEWORKS_PUBLIC_URL>/signin-google`, such as `https://slides.example.com/signin-google`. For a local trial, add `http://localhost:8080/signin-google`. For the dev loop, add `http://localhost:3000/signin-google`.
3. Set up the OAuth consent screen. SlideWorks asks only for the person's email, name and picture.
4. Keep the client ID and secret for the next step.

## 2. Choose the first admin

New people who sign in are **pending**: they wait on `/pending` until an admin approves them on `/admin/users`. An admin can also reject a person, revoke their access, or make them an admin.

Set `SLIDEWORKS_ADMIN_EMAIL` to your own Google email. The first time you sign in with it, you become the approved admin, and from then on the setting does nothing. Rejected people get no session, and the sign-in page tells them an admin hasn't approved the account.

## 3. Start the container

From a clone of the repo:

```bash
cd src
cat > .env <<'EOF'
SLIDEWORKS_PUBLIC_URL=https://slides.example.com
SLIDEWORKS_GOOGLE_CLIENT_ID=<client id>
SLIDEWORKS_GOOGLE_CLIENT_SECRET=<client secret>
SLIDEWORKS_ADMIN_EMAIL=you@example.com
SLIDEWORKS_RENDER_TOKEN_KEY=<random secret, openssl rand -base64 32>
EOF
docker compose up -d --build
curl -fsS http://localhost:8080/health
```

`docker-compose.yml` builds the image from `src/Dockerfile`. To run a published image instead, replace `build: .` with `image: ghcr.io/<owner>/<repo>:latest`. CI pushes `:latest` and `:<commit sha>` on every push to `main`.

Keep `.env` out of version control, because it holds the client secret and the render token key.

Migrations run on startup, so a new image upgrades the database itself.

### Storage

- The database, the cookie keys, the images, the profile photos and the uploaded templates' files all live on the `/data` volume. Back it up as a unit. The runtime image has no `sqlite3`, so take a consistent copy while the app runs from a throwaway container that mounts the volume and a host folder. Compose prefixes the volume name with the project name, which is the folder name, so from `src/` the volume is `src_slide-works-data` (`docker volume ls` shows it). From `src/`:

  ```bash
  mkdir -p backup
  docker run --rm -v src_slide-works-data:/data -v "$PWD/backup":/backup alpine sh -c \
    "apk add --no-cache sqlite >/dev/null && sqlite3 /data/slide-works.db '.backup /backup/slide-works.db' && cp -r /data/slide-works-keys /backup/ && for d in images templates avatars; do if [ -d /data/$d ]; then cp -r /data/$d /backup/; fi; done"
  ```

  That writes `backup/slide-works.db` plus copies of the cookie keys, any images, profile photos and template files, owned by root. `.backup` is safe while SlideWorks is writing; copying the `.db` file directly is not.
- Keep `/data` on a local disk, such as a named Docker volume. Never bind-mount it onto a network share (NFS, SMB or 9p), because SQLite's locking corrupts the file there.
- SlideWorks is a single process. Its live updates and rate limits are held in memory, so don't run two instances against one volume.

### Share email

Sharing a deck with a person by email address sends them one notification: who shared it, the deck's title, a link and an unsubscribe link. It goes through any SMTP server you set with `SLIDEWORKS_SMTP_HOST`, `SLIDEWORKS_SMTP_PORT` (587, STARTTLS), `SLIDEWORKS_SMTP_USERNAME`, `SLIDEWORKS_SMTP_PASSWORD` and `SLIDEWORKS_MAIL_FROM`. With no SMTP host, no emails are sent, and sharing still works.

Resend, as a worked example (any SMTP server works the same way):

```
SLIDEWORKS_SMTP_HOST=smtp.resend.com
SLIDEWORKS_SMTP_PORT=587
SLIDEWORKS_SMTP_USERNAME=resend
SLIDEWORKS_SMTP_PASSWORD=<your Resend API key>
SLIDEWORKS_MAIL_FROM=SlideWorks <share@mail.example.com>
```

`src/docker-compose.yml` also accepts the API key as `RESEND_KEY` when `SLIDEWORKS_SMTP_PASSWORD` is unset. The sender domain must be verified in your Resend account.

The email's unsubscribe link opens a page with one button; pressing it stops further share emails to that address. Sharing limits stop the email being used to spam: one person may add at most `SLIDEWORKS_SHARE_NEW_PER_HOUR` distinct new people an hour and `SLIDEWORKS_SHARE_NEW_PER_DAY` a day (each email counts once per window, however many decks it is added to), and one address gets at most `SLIDEWORKS_SHARE_EMAILS_PER_RECIPIENT_PER_DAY` share emails a day from everyone together.

### Uploaded templates

People can upload templates: fonts, logos, title art, custom layouts and whole bundles. The files go to disk, and the
database holds each file's row.

- **Where they live.** `<SLIDEWORKS_DATA_PATH>/templates/<template id>/<asset id>`, with the default `/data` when the
  setting is unset. The asset id is the first 32 hex characters of the file's SHA-256, so a file is named by its content
  and is never replaced. Files being uploaded wait in `templates/.uploads/` until they pass their checks and are moved
  into place; a file left there by a crash is never cleaned up and can be deleted while the app is stopped.
- **Back them up with the database.** A deck draws with its template's files, so a database restored without the
  `templates` folder has decks whose fonts, logos and art are missing. The backup command under [Storage](#storage)
  copies both. Restore them together.
- **The app never deletes a stored file.** A published version's files must stay for as long as a deck is pinned to that
  version, so the only files removed are the ones of an upload that failed. An archived template keeps its files.
  Each template's use is bounded by the limits below, but total disk use has no cap: monitor it.
- **Disk use per template.** One template stores at most 256 files and 240,000,000 bytes (about 229 MiB) over its whole
  life, counting every version. The server refuses an upload past either with `template_storage_full`. The limit of 50
  templates counts only templates that aren't archived, and an archived template keeps its files, so archiving frees a
  place but no disk and the total across templates is not bounded. The usual template uses a few megabytes.
- **Per file.** A font is at most 2 MB (2 x 1024 x 1024 bytes) and a logo or title art file at most 10 MB (10 x 1024 x
  1024 bytes). A custom layout's upload document is at most 800,000 bytes.
- **A bundle** (`POST /api/templates/import`) is a zip of at most 60 MB (60 x 1024 x 1024 bytes) that may not unpack to
  more than that, with at most 64 files. The server refuses a file over 64 KiB that unpacks to more than 100 times its packed size.
  The request body is limited to the same 60 MB, so the proxy's limit has to allow it.
- **Serving.** Files come from `/api/templates/{id}/assets/{assetId}` for people who may see the template, anyone who can view a deck on the template, and a guest of a live session on such a deck, with
  `X-Content-Type-Options: nosniff`, and for every file the policy `default-src 'none'; sandbox`, so an SVG opened as a
  page runs no script. A custom layout's stored file is never served.
- **Chromium for fonts.** Each uploaded font is measured once, in the same headless Chromium that draws the share card,
  so the slide text fits to its width. The image includes it. If Chromium can't start (a host that runs the app outside
  the image, or a missing system library), a font upload is refused with `renderer_unavailable`, and the person sees
  "This file can't be checked right now because the renderer is not available. Try again in a minute." A font is never
  accepted unmeasured. Nothing else about templates needs Chromium, and the share card fails the same way.

## Behind a TLS proxy

Run SlideWorks behind a proxy that ends TLS and forwards plain HTTP to port 8080. The proxy needs to:

- Forward every path to the container, including `/mcp`, `/oauth/*`, `/.well-known/*`, `/signin-google` and `/hubs/deck`.
- Pass WebSocket upgrades on `/hubs/deck`, which carries live updates. See [Live audience join](#live-audience-join) for the transports and the Host header.
- Allow request bodies up to 60 MB (60 x 1024 x 1024 bytes), the template bundle import limit. The largest other upload is a 10 MB image. See [Uploaded templates](#uploaded-templates). A proxy limit below 60 MB stops the bundle import, which the person sees as a failed upload.

`SLIDEWORKS_PUBLIC_URL` is the source of truth for the app's scheme and host. SlideWorks never reads `X-Forwarded-Proto` or `X-Forwarded-Host` to decide its own address, and it reads the `Host` header only to choose between the two configured origins (the public URL and, when set, the join URL), so a forged header can't produce a link, OAuth metadata or sign-in callback for any other address. If the proxy serves `https://slides.example.com`, set exactly that, and the session cookie is `Secure` even though the container sees plain HTTP.

`SLIDEWORKS_TRUSTED_PROXIES` is the one exception, and it covers the client IP only. The IP is used only for the per-caller rate limits: 10 a minute on `/oauth/register`, 60 a minute shared by `/oauth/token` and `/oauth/revoke`, and the join-code guessing limit (see [Live audience join](#live-audience-join)). Behind a proxy, set it to the proxy's address, such as `172.18.0.0/16` for a proxy on the same Docker network. Otherwise every request arrives from the proxy, and every client shares one rate-limit budget.

When it's set:

- A request whose connection comes from an address inside one of the listed networks takes its client IP from the last entry of `X-Forwarded-For`, which is the one that proxy added. Only that one hop is read, so anything a client wrote earlier in the header is ignored. An IPv4 proxy that reaches a dual-stack socket as `::ffff:a.b.c.d` still matches its IPv4 network.
- A request from any other address keeps its own address, and its `X-Forwarded-For` is ignored.
- `X-Forwarded-Proto` and `X-Forwarded-Host` are never read. Scheme and host still come only from `SLIDEWORKS_PUBLIC_URL`.

The rate limits count an IPv4 client by its address and an IPv6 client by its /64, because one IPv6 host normally holds a whole /64. Make sure the proxy adds the client's address to `X-Forwarded-For` rather than passing a client's header through unchanged. Caddy and Traefik do by default, and nginx does with `proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;`.

```bash
# .env, with Caddy on the same Docker network as SlideWorks
SLIDEWORKS_TRUSTED_PROXIES=172.18.0.0/16
```

When you trust a whole Docker network like this, either don't publish port 8080 at all, or publish it on localhost only (`127.0.0.1:8080:8080`, as `docker-compose.yml` does). Never publish it on every interface. The proxy is then the only way in from outside the host. Under rootless Docker or Docker Desktop, a request sent straight to a published 8080 can arrive from an address inside the trusted network, and its own `X-Forwarded-For` would then pick its rate-limit key.

A Caddy example:

```
slides.example.com {
    reverse_proxy slide-works:8080
}
```

## Live audience join

An audience joins a live talk on their phones with a six-digit code or a QR code. This works with no extra setup, on the
public URL, at `<SLIDEWORKS_PUBLIC_URL>/j/<code>`. A separate short domain makes the link and the QR code shorter to read out
and scan. This section covers that domain and what the proxy must allow for guests.

### A join domain

1. Choose a short domain, such as `slideworks.live`. Add a DNS record for it that points at the same proxy as the public
   URL (an A or AAAA record, or a CNAME).
2. Get a TLS certificate for it. Caddy and most cloud load balancers do this for you once the proxy has a site block for
   the name; otherwise issue one as you would for the public URL.
3. Route the join domain to the same container, and **keep the `Host` header** the guest's browser sent. SlideWorks tells the
   two domains apart by `Host`, and `X-Forwarded-Host` is never trusted. A proxy that rewrites `Host` to the container's
   name (nginx's `proxy_set_header Host $proxy_host;`, or a load balancer that replaces it) makes every request look like the
   public domain.
4. Set `SLIDEWORKS_JOIN_URL=https://slideworks.live` (in `.env` with Docker Compose) and run `docker compose up -d`, which
   re-reads `.env` where `docker compose restart` does not. Join links, the QR code and the address a join slide prints
   now use it.

```
slides.example.com, slideworks.live {
    reverse_proxy slide-works:8080
}
```

Caddy keeps `Host` by default. With nginx, use `proxy_set_header Host $host;`.

### What the join domain serves

The join domain serves only what a guest needs: the code page (`/join`), the guest page (`/j/<code>` and `/<six digits>`),
the guest API (`/api/join/*` and `/api/images/*`), the realtime hub (`/hubs/deck`), the app's assets, `/favicon.svg`, `/health` and the two phone app-link files under `/.well-known` (see [Phone app links](#phone-app-links)). Its `/` redirects to `/join`. Every other path redirects (302) to the same path on the public URL, so the app, the
API and the agent endpoints are never reachable under the join domain. The public URL serves the guest pages as well.

### Guests need cookies

A guest is identified by the `sw_guest` cookie: 128 random bits, `HttpOnly`, `SameSite=Lax`, set for 12 hours (and `Secure` on https) and
scoped to the host that set it. SlideWorks sets it on the first lookup and uses it to count each guest once, so a guest who has
cookies blocked can't join. A guest who opens the public URL and the join domain is two guests.

### Code guessing limit

To stop people guessing codes, each caller address gets 10 wrong codes a minute. Once they are used up, every lookup from
that address gets a 429 `too_many_attempts` until the minute ends, including a correct one. A correct code never counts
towards the 10. A caller can also have at most 100 lookups in flight at once. The caller address is the client IP, grouped
by /64 for IPv6, and it comes from `X-Forwarded-For` only when `SLIDEWORKS_TRUSTED_PROXIES` covers your proxy (see above). A
venue whose guests all share one address, such as one office or conference network behind NAT, shares one budget, so a room
of guests who mistype codes can lock the whole venue out for up to a minute. A correct code never uses up the budget, but while
the address is locked out, guests with the right code are refused too. They get in once the minute ends. Phones already
following keep their screen and retry by themselves.

### Realtime through the proxy

Guests follow the presenter over the `/hubs/deck` hub. It uses WebSockets, and falls back to Server-Sent Events and then
long polling when a network blocks them. Your proxy must pass WebSocket upgrades. For the fallbacks it must also not
buffer responses on `/hubs/deck` and must allow long-lived requests: with nginx, `proxy_buffering off;` and a
`proxy_read_timeout` of at least 100 seconds.

### Phone app links

The SlideWorks phone app claims join links for the hosted instance only. A self-hosted instance that runs no build of the app can leave `SLIDEWORKS_APPLE_APP_ID` and `SLIDEWORKS_ANDROID_CERT_SHA256` unset, and its join links open the browser as before. Otherwise the server answers two anonymous files, each a plain 200 `application/json` with no redirect, on the public host and on the join host:

- `/.well-known/apple-app-site-association`, from `SLIDEWORKS_APPLE_APP_ID`.
- `/.well-known/assetlinks.json`, from `SLIDEWORKS_ANDROID_CERT_SHA256`.

Each is a 404 while its setting is empty. The join domain serves only these two paths under `/.well-known`; the OAuth metadata stays on the public URL. Your proxy must forward `/.well-known/*` on the join domain as well as the public one, with no redirect, caching rewrite or authentication in front of them: Apple and Google fetch these files directly.

### Identity-aware proxies

If an identity-aware proxy (one that requires its own login on every request) sits in front of SlideWorks, the phone app's bearer calls never reach the server. Set `SLIDEWORKS_IDENTITY_PROXY=true`: the server then refuses to hand out app sign-in codes (400 `identity_proxy`), and the confirm page and the app tell the person the app can't sign in to this server. Exempt `/api`, `/hubs` and the `.well-known` files from the proxy if you want the app to work instead.

### Reverse-proxy access logs

SlideWorks logs no request query strings, but a reverse proxy usually does. On `/hubs`, the phone app sends its device secret as the `access_token` query value, and a guest's id as `guest`, because a WebSocket can't carry a header. Both are credentials, so strip their values from your proxy's access logs for `/hubs`, or drop the query string from those log lines. For nginx, log `$uri` rather than `$request_uri` for `/hubs`; for Caddy, remove the `access_token` and `guest` query keys with a `query` filter in the log encoder.

## 4. Sign in

Open the public URL and sign in with Google using the admin email. You land on the decks library. Anyone else who signs in waits on `/pending` until you approve them on `/admin/users`. The **Admin** link in the header shows how many people are waiting.

## 5. Connect an agent

Agents use the MCP server at `<SLIDEWORKS_PUBLIC_URL>/mcp`. It has a built-in OAuth server, so an agent needs only that URL. There is no API key to copy. The agent registers itself, opens a browser for you to sign in and approve it on a consent page, and then acts as you. It can see and change only the decks you can. You must be an approved person to approve an agent.

The decks library shows a **Connect an agent** banner with the URL to copy.

**Claude Code:**

```bash
claude mcp add --transport http slideworks https://slides.example.com/mcp
```

Then run `/mcp` in Claude Code and choose **Authenticate** for `slideworks`.

**Claude Desktop and claude.ai:** in **Settings → Connectors**, choose **Add custom connector**, name it SlideWorks and enter the `/mcp` URL. Claude connects from Anthropic's servers, so the URL must be reachable from the internet over HTTPS.

**VS Code** (`.vscode/mcp.json` in a workspace, or the user `mcp.json`):

```json
{
  "servers": {
    "slideworks": { "type": "http", "url": "https://slides.example.com/mcp" }
  }
}
```

**Cursor** (`~/.cursor/mcp.json`, or `.cursor/mcp.json` in a project):

```json
{
  "mcpServers": {
    "slideworks": { "url": "https://slides.example.com/mcp" }
  }
}
```

Other MCP clients that support the streamable HTTP transport and OAuth discovery work the same way.

Agent tokens last an hour and refresh for 30 days. When an admin revokes a person, their agents' tokens stop working too.

## 6. Build a deck

Ask the agent for a deck, for example: "Make a 10-slide deck on why small batches beat big plans, with a chart and a closing call to action." The agent works with these tools:

| Tool | What it does |
|---|---|
| `list_layouts`, `list_blocks` | The 31 layouts, their slots and limits, and the inline blocks. `detail` `compact` leaves the schema out: each slot is a short `{name, kind, required, max}` entry, and the icon names and blocks are listed once. Icon, emphasis and callout schemas are shared in `$defs` on the full response |
| `create_deck` | Creates a deck with a title and optional parts. `like` copies another deck you own: its template, palette and colours, logo, chrome, draft mark and talk length |
| `add_slide`, `update_slide`, `move_slide`, `delete_slide`, `set_notes` | Change slides. The server checks each slide against its layout and names the field that broke. A poll slide can take its settings in the same call, including a free-text or word-cloud question (`answers` `text` or `cloud`, with no options). A poll slide with a round open can't be deleted or given another layout, and its options can't shrink or change order, until the poll is closed |
| `update_poll` | Changes a poll slide's settings: one pick or up to some, when the room sees the counts, the correct answer, when it opens, and whether answers are choices, free text or a word cloud. Refused while a round is open |
| `add_poll_option` | Appends an option to a poll slide, up to 8. Allowed while a round is open, and the phones show it at once |
| `open_poll`, `close_poll` | Opens or closes a poll slide's round on a deck a person has taken live. They need edit access; revealing counts, showing the answer and starting a new round stay with the presenter |
| `get_poll_results` | A poll slide's rounds for each time the deck went live: the option texts each round ran with (kept when the options change later), answered, joined, response rate (capped at 1) and the count per option. Each round says whether it is still open (then its numbers are live). Never who picked what |
| `update_qa` | Changes a deck's audience Q&A settings, live or not: questions open, closed or off, review first, names allowed, reactions, and where a shown question appears (any slide or the Q&A slide). With a slide number it sets that slide's own questions and reactions switches instead. Answers with the settings as a sentence and as fields |
| `get_questions` | The questions the room asked, for each time the deck went live, newest session first: the text, the name the asker chose to show, the state (waiting, open, answered or hidden), the votes and when it was asked, with the session's counts. `state` keeps one state. It reads stored questions, so past sessions still show when questions are off. Never who asked or upvoted. Approving, hiding, answering and showing stay with the presenter |
| `upload_image` | Returns a one-time upload URL for a PNG, JPEG, WebP or GIF of up to 10 MB |
| `upload_images` | Starts up to 30 of those uploads in one call. Each file is still PUT to its own URL |
| `set_sharing` | Private, anyone with the link, or named people |
| `list_decks`, `get_deck`, `get_deck_url` | Read decks back and get the link to present. `get_deck` also returns the Q&A settings as `qa` and each session's reaction totals as `reactions`. With `detail` `summary` the slides are cut down to number, layout, part and headline (120 characters at most); `qa` and `reactions` stay |

Open the link the agent returns, or find the deck in the library. The deck page shows the slides, the speaker notes and a log of every change, with the agent's name. **Present** opens the player, which works with the keyboard, clicks and swipes on a laptop or a phone.

## Development

See the README's "Run locally". In Development, `appsettings.Development.json` sets `SLIDEWORKS_PUBLIC_URL=http://localhost:3000`, the Vite dev server's origin, and points the database and images at local relative paths. Vite proxies `/api`, `/hubs`, `/mcp`, the OAuth endpoints, `/.well-known`, `/signin/*` and `/signin-google` to Kestrel on :5510. Always open http://localhost:3000, and connect a local agent to `http://localhost:3000/mcp`.

To develop the phone app against a local server, a phone on your network must reach Kestrel, so bind every interface and set the public URL to the address the phone uses:

```bash
ASPNETCORE_URLS=http://0.0.0.0:5510 SLIDEWORKS_PUBLIC_URL=http://192.168.1.20:5510 dotnet run --project src/SlideWorks.WebApi
```

Type exactly that address into the app's Sign in screen: the confirm page's origin is `SLIDEWORKS_PUBLIC_URL`, and the app accepts a sign-in only from the address it was given. An Android emulator reaches the host at `http://10.0.2.2:5510` and an iOS simulator at `http://localhost:5510`; use those as the public URL for the same reason. Add that address's `/signin-google` to the Google client's redirect URIs.
