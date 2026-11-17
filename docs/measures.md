# Measures

Each Feature's Measure is a query you can run against a SlideWorks database. There is no admin screen for them. Copy the
query, open the database read-only, and bind the parameters.

```bash
sqlite3 -readonly /data/slide-works.db
```

## Look adoption (SW-2)

**What it counts:** the share of new decks that are on a built-in template and a built-in palette, with no chrome
override and no logo. A high share means the built-in looks are good enough to present as they are. The target is set
after two weeks of use.

**Where it reads from:**

- *New decks in the window* come from the activity log. Each deck has exactly one creation entry,
  `created the deck from the <template> template with the <palette> palette`, whether the deck was made in the app
  (`POST /api/decks`) or by an agent (`create_deck`). `At` on that entry is the creation time.
- *The look* is the deck's current row in `Decks`:
  - `Template` and `Palette` hold names, such as `workshop` and `ember`.
  - `ChromeJson` is null when the deck overrides none of the template's chrome. Resetting every key to the template's
    default stores null again.
  - `LogoImageId` is null when the deck has no logo.
  - The draft mark isn't an override and isn't counted.
- *Relooked* comes from the look entries in the activity log: a deck counts once if any `PUT /api/decks/{id}/look` or
  `set_look` call changed its template or palette, such as `changed the template to workshop and the palette to Ember`.
  Every call records an entry, including one that changes nothing, so this counts attempts, not net changes. It is
  context for the share, not part of it.

**Parameters:** `@from` (inclusive) and `@to` (exclusive), as UTC date-times such as `2026-11-16 00:00:00`.

Times are stored as integers in EF Core's `DateTimeOffsetToBinaryConverter` encoding:
`((ticks / 1000) << 11) | offset minutes`, and every time SlideWorks writes is UTC. The query converts the parameters
into that encoding, so you never compute it by hand.

**Keep in step:** the lists of built-in template and palette names below must match
`src/contracts/templates.json` and `src/contracts/palettes.json`. `LookAdoptionQueryTests` fails when they drift. A template or
palette that isn't built in, such as an uploaded one, never counts as adopted.

```sql
WITH bounds AS (
  SELECT ((CAST(strftime('%s', @from) AS INTEGER) * 10000000 + 621355968000000000) / 1000) << 11 AS lo,
         ((CAST(strftime('%s', @to) AS INTEGER) * 10000000 + 621355968000000000) / 1000) << 11 AS hi
),
created AS (
  SELECT DISTINCT a.DeckId
  FROM ActivityEntries a, bounds b
  WHERE a.Summary LIKE 'created the deck from the % template with the % palette'
    AND a.At >= b.lo AND a.At < b.hi
),
relooked AS (
  SELECT DISTINCT a.DeckId
  FROM ActivityEntries a
  WHERE a.Summary LIKE 'changed %'
    AND (a.Summary LIKE '%the template to %' OR a.Summary LIKE '%the palette to %')
),
scored AS (
  SELECT CASE WHEN d.Template IN ('talk', 'workshop', 'update')
               AND d.Palette IN ('cobalt', 'ember', 'plum', 'midnight')
               AND d.ChromeJson IS NULL
               AND d.LogoImageId IS NULL
              THEN 1 ELSE 0 END AS adopted,
         CASE WHEN r.DeckId IS NULL THEN 0 ELSE 1 END AS relooked
  FROM created c
  JOIN Decks d ON d.Id = c.DeckId
  LEFT JOIN relooked r ON r.DeckId = c.DeckId
)
SELECT COUNT(*) AS new_decks,
       COALESCE(SUM(adopted), 0) AS adopted,
       ROUND(100.0 * SUM(adopted) / NULLIF(COUNT(*), 0), 1) AS adopted_pct,
       COALESCE(SUM(relooked), 0) AS relooked
FROM scored;
```

**Columns:** `new_decks` (created in the window), `adopted` (on a built-in look with no override), `adopted_pct` (the
Measure, as a percentage; null when no deck was created), and `relooked` (new decks whose template or palette was
changed after creation).

## Audience join (SW-5)

Two queries, one for each Measure of the audience join: how many people a talk reached, and how quickly they joined once
the room could see the join slide. Both read from the audience sessions SlideWorks keeps for each Go live.

**Where it reads from:**

- `AudienceSessions` has one row for each time a deck went live. `StartedAt` is when Go live ran, and `PeakJoined` is the
  most distinct guests connected at once during that session. `JoinSlideFirstShownAt` is the first time the live
  presentation was on a `join` slide, and is null when it never was.
- `AudienceJoins` has one row for each guest in a session. `JoinedAt` is when the guest joined.

**Parameters:** `@from` (inclusive) and `@to` (exclusive), as UTC date-times such as `2026-11-16 00:00:00`. A session is in
the window when it started in it. Times are stored in the encoding described under Look adoption, and both queries
convert the parameters themselves.

### Peak joined per session

**Columns:** `session_id`, `deck_id`, `started_at` (UTC), and `peak_joined`, the Measure. Rows are newest session first.

```sql
WITH bounds AS (
  SELECT ((CAST(strftime('%s', @from) AS INTEGER) * 10000000 + 621355968000000000) / 1000) << 11 AS lo,
         ((CAST(strftime('%s', @to) AS INTEGER) * 10000000 + 621355968000000000) / 1000) << 11 AS hi
)
SELECT s.Id AS session_id,
       s.DeckId AS deck_id,
       datetime((s.StartedAt >> 11) / 10000 - 62135596800, 'unixepoch') AS started_at,
       s.PeakJoined AS peak_joined
FROM AudienceSessions s, bounds b
WHERE s.StartedAt >= b.lo AND s.StartedAt < b.hi
ORDER BY s.StartedAt DESC, s.Id;
```

### Seconds from the join slide to each join

A guest who joined in the lobby is recorded as joining when Go live ran, and a guest can join before the join slide is
first shown, so some joins come before the slide. The query counts those as 0 seconds (clamped, not excluded).

**Columns:** `session_id`, `deck_id`, `started_at` (UTC), `joins` (guests counted, including the clamped ones),
`before_slide` (how many of those joined before the join slide was first shown, and so count as 0), and `median_seconds`,
the Measure. A session whose join slide was never shown has no row. Rows are newest session first.

```sql
WITH bounds AS (
  SELECT ((CAST(strftime('%s', @from) AS INTEGER) * 10000000 + 621355968000000000) / 1000) << 11 AS lo,
         ((CAST(strftime('%s', @to) AS INTEGER) * 10000000 + 621355968000000000) / 1000) << 11 AS hi
),
gaps AS (
  SELECT j.SessionId AS session_id,
         s.DeckId AS deck_id,
         s.StartedAt AS started,
         MAX(0, ((j.JoinedAt >> 11) - (s.JoinSlideFirstShownAt >> 11)) / 10000.0) AS seconds,
         CASE WHEN j.JoinedAt <= s.JoinSlideFirstShownAt THEN 1 ELSE 0 END AS before_join
  FROM AudienceJoins j
  JOIN AudienceSessions s ON s.Id = j.SessionId, bounds b
  WHERE s.JoinSlideFirstShownAt IS NOT NULL
    AND s.StartedAt >= b.lo AND s.StartedAt < b.hi
),
ranked AS (
  SELECT session_id,
         deck_id,
         started,
         seconds,
         ROW_NUMBER() OVER (PARTITION BY session_id ORDER BY seconds) AS rn,
         COUNT(*) OVER (PARTITION BY session_id) AS n,
         SUM(before_join) OVER (PARTITION BY session_id) AS before_slide
  FROM gaps
)
SELECT session_id,
       deck_id,
       datetime((started >> 11) / 10000 - 62135596800, 'unixepoch') AS started_at,
       n AS joins,
       before_slide,
       ROUND(AVG(seconds), 1) AS median_seconds
FROM ranked
WHERE rn IN ((n + 1) / 2, (n + 2) / 2)
GROUP BY session_id, deck_id, started, n, before_slide
ORDER BY started DESC, session_id;
```

Joins before the slide count as 0 s and pull the median toward 0, so for a room that mostly joined before the join slide
was shown, `median_seconds` says little about the join slide. Read it together with `before_slide`.

## Poll response rate (SW-6)

**What it counts:** for each closed poll round, the share of the guests in the room who answered. A high share means the
question was easy to answer and the room could see it. A low share on a round with many people joined means they didn't
get to it, or the poll wasn't worth answering.

**Where it reads from:**

- `PollRounds` has one row for each round of a poll slide in one audience session. A round is closed when `ClosedAt` is
  set and `OpenedAt` is set. In the same transaction, closing writes `JoinedAtClose`, the number of guests in the
  session's roster at that moment, and `Final`, a JSON snapshot of the result. `Final.answered` is how many guests had
  an answer in the round.
- A round with `ClosedAt` set and `OpenedAt` null is a draft that was discarded, never a round that ran, and has no
  `Final`. A round with `ClosedAt` null is still draft or open. The query leaves out both.
- `AudienceSessions` gives each round's session its deck.

**Parameters:** `@from` (inclusive) and `@to` (exclusive), as UTC date-times such as `2026-11-16 00:00:00`. A round is in
the window when it closed in it. Times are stored in the encoding described under Look adoption, and the query converts
the parameters itself.

**Rates over 1 and joined of 0:** guests can leave after answering, so `JoinedAtClose` can be smaller than
`Final.answered`. The query caps `rate` at 1, as the presenter's panel and strip and `get_poll_results` do, and keeps
`answered` and `joined` as they were stored, so a capped round can still be told apart. When `JoinedAtClose` is 0 or
null, `rate` is null, not 0, so a round nobody was in for doesn't pull an average down. The query never divides by zero.

**Columns:** `session_id`, `deck_id`, `slide_id`, `round_number`, `closed_at` (UTC), `answered`, `joined`, and `rate`, the
Measure, from 0 to 1. Rows are newest first.

```sql
WITH bounds AS (
  SELECT ((CAST(strftime('%s', @from) AS INTEGER) * 10000000 + 621355968000000000) / 1000) << 11 AS lo,
         ((CAST(strftime('%s', @to) AS INTEGER) * 10000000 + 621355968000000000) / 1000) << 11 AS hi
)
SELECT r.SessionId AS session_id,
       s.DeckId AS deck_id,
       r.SlideId AS slide_id,
       r.Number AS round_number,
       datetime((r.ClosedAt >> 11) / 10000 - 62135596800, 'unixepoch') AS closed_at,
       json_extract(r.Final, '$.answered') AS answered,
       r.JoinedAtClose AS joined,
       CASE WHEN COALESCE(r.JoinedAtClose, 0) = 0 THEN NULL
            ELSE MIN(1.0, json_extract(r.Final, '$.answered') * 1.0 / r.JoinedAtClose)
       END AS rate
FROM PollRounds r
JOIN AudienceSessions s ON s.Id = r.SessionId, bounds b
WHERE r.OpenedAt IS NOT NULL
  AND r.ClosedAt IS NOT NULL
  AND r.Final IS NOT NULL
  AND r.ClosedAt >= b.lo AND r.ClosedAt < b.hi
ORDER BY r.ClosedAt DESC, r.SessionId, r.Number;
```

**Keep in step:** the query reads these columns, so a change to them changes the query. From `PollRounds`: `SessionId`,
`SlideId`, `Number`, `OpenedAt`, `ClosedAt`, `JoinedAtClose` and `Final` (JSON with an `answered` key). From
`AudienceSessions`: `Id` and `DeckId`. `PollResponseRateQueryTests` runs this query as written and fails when they drift.
