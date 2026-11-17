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
| `SLIDEWORKS_GOOGLE_CLIENT_ID` | For sign-in | The Google OAuth client's ID. |
| `SLIDEWORKS_GOOGLE_CLIENT_SECRET` | For sign-in | The Google OAuth client's secret. Sign-in is on only when both Google values are set. With neither set, the server still runs, `/signin` says sign-in isn't set up, and `/signin/google` answers 503. With only one set, the server logs a warning and sign-in stays off. |
| `SLIDEWORKS_ADMIN_EMAIL` | Recommended | The Google email of the first admin. When that person signs in and no admin exists yet, they become an approved admin. It's honoured only while no admin exists, so it's harmless to leave it set. If it's unset and there is no admin, the server logs a warning at startup, because nobody can approve sign-ups. |
| `SLIDEWORKS_DB_PATH` | No | The SQLite file. Default `/data/slide-works.db`. The session-cookie keys are kept in `slide-works-keys/` next to it, so a restart or a new image doesn't sign everyone out. |
| `SLIDEWORKS_DATA_PATH` | No | The data folder for uploaded images. Default `/data`. Images are stored under `images/<deck id>/`. |
| `SLIDEWORKS_SEQ_URL` | No | Also send logs to this Seq server. Without it, logs go to the console only (`docker logs`). |
| `SLIDEWORKS_SEQ_API_KEY` | No | The Seq API key, when your Seq server needs one. |
| `SLIDEWORKS_TRUSTED_PROXIES` | Behind a proxy | The proxy addresses whose `X-Forwarded-For` header SlideWorks trusts for the client IP, as a comma-separated list of CIDRs such as `172.18.0.0/16, fd00::/8`. A single address, such as `10.0.0.5`, means just that address. Unset by default, which trusts no proxy. The server refuses to start if an entry isn't a CIDR or an address. See [Behind a TLS proxy](#behind-a-tls-proxy). |
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

- The database, the cookie keys and the images all live on the `/data` volume. Back it up as a unit. The runtime image has no `sqlite3`, so take a consistent copy while the app runs from a throwaway container that mounts the volume and a host folder. Compose prefixes the volume name with the project name, which is the folder name, so from `src/` the volume is `src_slide-works-data` (`docker volume ls` shows it). From `src/`:

  ```bash
  mkdir -p backup
  docker run --rm -v src_slide-works-data:/data -v "$PWD/backup":/backup alpine sh -c \
    "apk add --no-cache sqlite >/dev/null && sqlite3 /data/slide-works.db '.backup /backup/slide-works.db' && cp -r /data/slide-works-keys /backup/ && if [ -d /data/images ]; then cp -r /data/images /backup/; fi"
  ```

  That writes `backup/slide-works.db` plus copies of the cookie keys and any images, owned by root. `.backup` is safe while SlideWorks is writing; copying the `.db` file directly is not.
- Keep `/data` on a local disk, such as a named Docker volume. Never bind-mount it onto a network share (NFS, SMB or 9p), because SQLite's locking corrupts the file there.
- SlideWorks is a single process. Its live updates and rate limits are held in memory, so don't run two instances against one volume.

## Behind a TLS proxy

Run SlideWorks behind a proxy that ends TLS and forwards plain HTTP to port 8080. The proxy needs to:

- Forward every path to the container, including `/mcp`, `/oauth/*`, `/.well-known/*`, `/signin-google` and `/hubs/deck`.
- Pass WebSocket upgrades on `/hubs/deck`, which carries live updates.
- Allow request bodies up to 10 MB, the image upload limit.

`SLIDEWORKS_PUBLIC_URL` is the source of truth for the app's scheme and host. SlideWorks never reads `X-Forwarded-Proto`, `X-Forwarded-Host` or the `Host` header to decide its own address, so a forged header can't change a link, the OAuth metadata or the sign-in callback. If the proxy serves `https://slides.example.com`, set exactly that, and the session cookie is `Secure` even though the container sees plain HTTP.

`SLIDEWORKS_TRUSTED_PROXIES` is the one exception, and it covers the client IP only. The IP is used only for the per-caller rate limits: 10 a minute on `/oauth/register`, and 60 a minute shared by `/oauth/token` and `/oauth/revoke`. Behind a proxy, set it to the proxy's address, such as `172.18.0.0/16` for a proxy on the same Docker network. Otherwise every request arrives from the proxy, and every client shares one rate-limit budget.

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
| `list_layouts`, `list_blocks` | The 16 layouts, their slots and limits, and the inline blocks |
| `create_deck` | Creates a deck with a title and optional parts |
| `add_slide`, `update_slide`, `move_slide`, `delete_slide`, `set_notes` | Change slides. The server checks each slide against its layout and names the field that broke |
| `upload_image` | Returns a one-time upload URL for a PNG, JPEG, WebP or GIF of up to 10 MB |
| `set_sharing` | Private, anyone with the link, or named people |
| `list_decks`, `get_deck`, `get_deck_url` | Read decks back and get the link to present |

Open the link the agent returns, or find the deck in the library. The deck page shows the slides, the speaker notes and a log of every change, with the agent's name. **Present** opens the player, which works with the keyboard, clicks and swipes on a laptop or a phone.

## Development

See the README's "Run locally". In Development, `appsettings.Development.json` sets `SLIDEWORKS_PUBLIC_URL=http://localhost:3000`, the Vite dev server's origin, and points the database and images at local relative paths. Vite proxies `/api`, `/hubs`, `/mcp`, the OAuth endpoints, `/.well-known`, `/signin/*` and `/signin-google` to Kestrel on :5510. Always open http://localhost:3000, and connect a local agent to `http://localhost:3000/mcp`.
