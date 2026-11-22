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
  (`POST /api/decks`) or by an agent (`create_deck`). `At` on that entry is the creation time. A deck made like another
  deck carries `like "<title>" ` after `the deck`, and the pattern allows for it.
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
  WHERE a.Summary LIKE 'created the deck %from the % template with the % palette'
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

## Uploaded templates (SW-50)

**What it counts:** the share of new decks that use an uploaded template. A high share means people make decks on
templates they or a colleague uploaded, not only on the built-in ones. It is a separate Measure from Look adoption: the
SW-2 query is unchanged, and an uploaded template still never counts as adopted there.

**Where it reads from:**

- *New decks in the window* come from the activity log, as in Look adoption: each deck has exactly one creation entry,
  `created the deck from the <template> template with the <palette> palette`, and `At` on that entry is the creation
  time. A copy's entry also carries `like "<title>" ` after `the deck`, and the pattern allows for it.
- *The template* is the deck's current row in `Decks`. `Template` holds `tpl_<id>` for an uploaded template and a
  built-in's name, such as `workshop`, for a built-in. A deck counts as using an uploaded template when `Template`
  starts with `tpl_`. The activity text names the template by its label, never its id, so it cannot be used for this.
- Because the template is read from the row, not the creation entry, a deck made on a built-in and moved to an uploaded
  template later (`PUT /api/decks/{id}/look` or `set_look`) counts as using one, and a deck moved back to a built-in
  does not. This is how Look adoption reads the look too.

**Parameters:** `@from` (inclusive) and `@to` (exclusive), as UTC date-times such as `2026-11-16 00:00:00`. Times are
stored in the encoding described under Look adoption, and the query converts the parameters itself.

```sql
WITH bounds AS (
  SELECT ((CAST(strftime('%s', @from) AS INTEGER) * 10000000 + 621355968000000000) / 1000) << 11 AS lo,
         ((CAST(strftime('%s', @to) AS INTEGER) * 10000000 + 621355968000000000) / 1000) << 11 AS hi
),
created AS (
  SELECT DISTINCT a.DeckId
  FROM ActivityEntries a, bounds b
  WHERE a.Summary LIKE 'created the deck %from the % template with the % palette'
    AND a.At >= b.lo AND a.At < b.hi
),
scored AS (
  SELECT CASE WHEN substr(d.Template, 1, 4) = 'tpl_' THEN 1 ELSE 0 END AS on_uploaded
  FROM created c
  JOIN Decks d ON d.Id = c.DeckId
)
SELECT COUNT(*) AS new_decks,
       COALESCE(SUM(on_uploaded), 0) AS on_uploaded,
       ROUND(100.0 * SUM(on_uploaded) / NULLIF(COUNT(*), 0), 1) AS on_uploaded_pct
FROM scored;
```

**Columns:** `new_decks` (created in the window), `on_uploaded` (of those, on an uploaded template now), and
`on_uploaded_pct` (the Measure, as a percentage with one decimal; null when no deck was created).

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

- `PollRounds` has one row for each round of a poll slide in one audience session, and one for each of the session's Q&A
  rounds (SW-9). `Kind` tells them apart: it is stored as the enum's name, `Slide` or `Qa`, and a check constraint holds it
  to those. This measure is about poll slides, so the query keeps `Kind = 'Slide'` only; a Q&A round's `Final` counts
  upvotes, not answers. A round is closed when `ClosedAt` is set and `OpenedAt` is set. In the same transaction, closing
  writes `JoinedAtClose`, the number of guests in the session's roster at that moment, and `Final`, a JSON snapshot of
  the result. `Final.answered` is how many guests had an answer in the round.
- A round with `ClosedAt` set and `OpenedAt` null is a draft that was discarded, never a round that ran, and has no
  `Final`. A round with `ClosedAt` null is still draft or open. The query leaves out both.
- `AudienceSessions` gives each round's session its deck.

**Parameters:** `@from` (inclusive) and `@to` (exclusive), as UTC date-times such as `2026-11-16 00:00:00`. A round is in
the window when it closed in it. Times are stored in the encoding described under Look adoption, and the query converts
the parameters itself.

**Rates over 1 and joined of 0:** guests can leave after answering, so `JoinedAtClose` can be smaller than
`Final.answered`. The query caps `rate` at 1, as the presenter's panel and strip and `get_poll_results` do, and keeps
`answered` and `joined` as they were stored, so a capped round can still be told apart. When `JoinedAtClose` is 0 or
null, `rate` is null, not 0, so a round nobody was in for doesn't pull an average down. `get_poll_results` returns 0 for the same round; the query returns NULL because an average over rounds should skip an empty room, not count it as a round nobody answered. The query never divides by zero.

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
WHERE r.Kind = 'Slide'
  AND r.OpenedAt IS NOT NULL
  AND r.ClosedAt IS NOT NULL
  AND r.Final IS NOT NULL
  AND r.ClosedAt >= b.lo AND r.ClosedAt < b.hi
ORDER BY r.ClosedAt DESC, r.SessionId, r.Number;
```

**Keep in step:** the query reads these columns, so a change to them changes the query. From `PollRounds`: `Kind` (stored as
the enum's name, `Slide` or `Qa`), `SessionId`, `SlideId`, `Number`, `OpenedAt`, `ClosedAt`, `JoinedAtClose` and `Final`
(JSON with an `answered` key). From `AudienceSessions`: `Id` and `DeckId`. `PollResponseRateQueryTests` runs this query as written and fails when they drift.

## Q&A and reactions (SW-9)

Two queries, one for each Measure of Q&A and reactions: how many questions a talk drew, and how much of the room took part.
Both read from the audience sessions SlideWorks keeps for each Go live.

**Where it reads from:**

- `AudienceSessions` has one row for each time a deck went live, and `AudienceJoins` one row for each guest who joined it.
- `PollRounds` with `Kind = 'Qa'` is the session's Q&A round. Go live opens it unless questions are off for the deck, and turning questions on while live opens it then. With
  questions off there is no round. It is the only kind of round the questions live in.
- `Questions` has one row for each question asked, keyed by the round and a position. `GuestId` is the guest who asked.
  A question that is hidden, waiting for approval or answered is still a question asked, so the count includes them.
- `PollAnswers` on a Q&A round is one guest's upvotes: `Picks` is a JSON array of the positions they upvoted, and an empty
  array means they took every upvote back. A guest counts as having upvoted only when `Picks` is not empty.
- `ReactionGuests` has one row for each guest who tapped a reaction at all in the session. Never a row per tap.

**Parameters:** `@from` (inclusive) and `@to` (exclusive), as UTC date-times such as `2026-11-16 00:00:00`. A session is in
the window when it started in it. Times are stored in the encoding described under Look adoption, and both queries
convert the parameters themselves.

### Questions asked per session

**Columns:** `session_id`, `deck_id`, `started_at` (UTC) and `asked`, the Measure. A session with no questions has a row with
`asked` 0. Rows are newest session first.

```sql
WITH bounds AS (
  SELECT ((CAST(strftime('%s', @from) AS INTEGER) * 10000000 + 621355968000000000) / 1000) << 11 AS lo,
         ((CAST(strftime('%s', @to) AS INTEGER) * 10000000 + 621355968000000000) / 1000) << 11 AS hi
)
SELECT s.Id AS session_id,
       s.DeckId AS deck_id,
       datetime((s.StartedAt >> 11) / 10000 - 62135596800, 'unixepoch') AS started_at,
       COUNT(q.RoundId) AS asked
FROM AudienceSessions s
CROSS JOIN bounds b
LEFT JOIN PollRounds r ON r.SessionId = s.Id AND r.Kind = 'Qa'
LEFT JOIN Questions q ON q.RoundId = r.Id
WHERE s.StartedAt >= b.lo AND s.StartedAt < b.hi
GROUP BY s.Id
ORDER BY s.StartedAt DESC, s.Id;
```

### Share of joined guests who asked, upvoted or reacted

A guest counts once however many of the three they did, and only if they have a row in the session's `AudienceJoins`. A
`Question`, upvote or reaction from a guest who never joined is not counted.

**Columns:** `session_id`, `deck_id`, `started_at` (UTC), `joined` (guests who joined), `asked`, `upvoted` and `reacted` (how
many of them did each, so the three can add up to more than `took_part`), `took_part` (guests who did at least one) and
`share`, the Measure: `took_part` over `joined`, from 0 to 1. A session nobody joined has a row with `joined` 0 and `share` null. Rows are newest
session first.

```sql
WITH bounds AS (
  SELECT ((CAST(strftime('%s', @from) AS INTEGER) * 10000000 + 621355968000000000) / 1000) << 11 AS lo,
         ((CAST(strftime('%s', @to) AS INTEGER) * 10000000 + 621355968000000000) / 1000) << 11 AS hi
),
guests AS (
  SELECT s.Id AS session_id,
         s.DeckId AS deck_id,
         s.StartedAt AS started,
         j.GuestId AS guest_id,
         EXISTS (SELECT 1 FROM Questions q
                 JOIN PollRounds r ON r.Id = q.RoundId
                 WHERE r.SessionId = j.SessionId AND r.Kind = 'Qa' AND q.GuestId = j.GuestId) AS asked,
         EXISTS (SELECT 1 FROM PollAnswers a
                 JOIN PollRounds r ON r.Id = a.RoundId
                 WHERE r.SessionId = j.SessionId AND r.Kind = 'Qa' AND a.GuestId = j.GuestId
                   AND json_array_length(a.Picks) > 0) AS upvoted,
         EXISTS (SELECT 1 FROM ReactionGuests g
                 WHERE g.SessionId = j.SessionId AND g.GuestId = j.GuestId) AS reacted
  FROM AudienceSessions s
  CROSS JOIN bounds b
  LEFT JOIN AudienceJoins j ON j.SessionId = s.Id
  WHERE s.StartedAt >= b.lo AND s.StartedAt < b.hi
)
SELECT session_id,
       deck_id,
       datetime((started >> 11) / 10000 - 62135596800, 'unixepoch') AS started_at,
       COUNT(guest_id) AS joined,
       SUM(asked) AS asked,
       SUM(upvoted) AS upvoted,
       SUM(reacted) AS reacted,
       SUM(asked OR upvoted OR reacted) AS took_part,
       ROUND(1.0 * SUM(asked OR upvoted OR reacted) / NULLIF(COUNT(guest_id), 0), 4) AS share
FROM guests
GROUP BY session_id
ORDER BY started DESC, session_id;
```

**Keep in step:** the queries read these columns, so a change to them changes the queries. From `AudienceSessions`: `Id`,
`DeckId` and `StartedAt`. From `AudienceJoins`: `SessionId` and `GuestId`. From `PollRounds`: `Id`, `SessionId` and `Kind`
(stored as the enum's name, `Slide` or `Qa`). From `Questions`: `RoundId` and `GuestId`. From `PollAnswers`: `RoundId`,
`GuestId` and `Picks`. From `ReactionGuests`: `SessionId` and `GuestId`. `QaMeasuresSqlTests` runs both queries as written
and fails when they drift.
