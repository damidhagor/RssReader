# Roadmap

This document captures RssReader's product scope, technology stack decisions, and phased milestones as
decided during the initial roadmap-brainstorming session. It reflects a personal project's real constraints
and preferences, not a generic template — revisit and update it as decisions evolve.

## What RssReader Is

A synchronized RSS/Atom feed reader: a core server fetches, parses, and stores feeds; one or more client
applications let a user subscribe to feeds, read entries, and (later) organize/filter them. Feed content is
fetched once per feed and shared across users, while read/unread state, subscriptions, and filters are
tracked per user.

## Users

Single-user for now (the project owner is the only user), but the data model is user-scoped from day one
(see [Data Model Shape](#data-model-shape)) so multi-user support can be added later without a schema
rewrite. The app is publicly accessible, so real authentication is required even with one user (see
[Authentication](#authentication)).

## Clients

The web app ships first. Other clients are deliberately deferred until the web app and its API are solid:

- **Web app (Blazor WebAssembly)** — first client, see [Web Client](#web-client).
- **Browser extension** — later milestone; a quick-subscribe flow (add the current page's feed in one click)
  is a likely first feature for it.
- **Mobile (MAUI, Blazor Hybrid)** — later milestone; expected to reuse the web app's Razor
  components/`HttpClient`-based data access largely unchanged, which is one of the reasons Blazor
  WebAssembly (not Blazor Server) was chosen for the web client.

## Technology Stack

| Concern | Choice | Why |
| --- | --- | --- |
| Server language/runtime | .NET 11, C# (preview langversion) | See `AGENTS.md` |
| Client-facing API | ASP.NET Core Minimal APIs | Lightweight, fits a small REST surface, OpenAPI-friendly |
| Database | PostgreSQL | Relational fit for the domain, `jsonb` for feed-format variance/extensions, native full-text search (`tsvector`/GIN); an instance is already deployed |
| Feed parsing | `System.ServiceModel.Syndication` | Official, maintained NuGet package; covers RSS 2.0 + Atom 1.0 with `ElementExtensions`/`AttributeExtensions` for the rest |
| Web client | Blazor WebAssembly | Consumes the API like any other client (no shadow access path); reusable in a future MAUI Blazor Hybrid client |
| Authentication | OIDC via the user's existing Authentik instance | No local password/user store; API validates JWT bearer tokens, web client does Authorization Code + PKCE |
| Local orchestration/dev | .NET Aspire (AppHost + ServiceDefaults) | Wires up Postgres + Api + Worker + Web for local dev; used instead of hand-written docker-compose for that purpose |
| Local/integration testing | TUnit + `Aspire.Hosting.Testing` | Real Postgres via the AppHost rather than Testcontainers or fully mocked repositories |
| EF Core migrations | Aspire `AddEFMigrations` (`Aspire.Hosting.EntityFrameworkCore`) | `RunDatabaseUpdateOnStart()` for local dev; `PublishAsMigrationBundle(publishContainer: true)` for a standalone migration container image in production, avoiding both auto-migrate-on-startup races between Api/Worker and a bespoke admin migration endpoint |
| Production deployment | VPS + Docker Compose | Already available; whether `aspire publish` generates the Compose artifacts or they stay hand-maintained is still to be trialed (see [Open Questions](#open-questions)) |
| Real-time client push (v2) | SignalR (hosted by `Api`) + Redis Pub/Sub | SignalR is idiomatic for Blazor WebAssembly; Redis Pub/Sub bridges Worker's background sync events to Api's hub (ephemeral, at-most-once is acceptable) and doubles as a cache for rarely-changing feed/entry data |

## Backend Architecture

Two deployables share persistence-and-fetching class libraries rather than one monolithic host, since feed
polling (bursty, network/CPU-bound, needs its own scheduling) is a fundamentally different workload from
serving API requests — and the user already has trivial-cost deployment (VPS + Docker Compose) for running
more than one container, so there's no deployment-complexity reason to avoid the split:

- **`RssReader.Api`** — client-facing Minimal API host. Owns its own request/response DTOs; talks to
  `RssReader.Data` (and `RssReader.Rss` where needed) directly. No separate business-logic/service layer for
  now — the domain is simple enough that this would be premature layering.
- **`RssReader.Worker`** — background host that polls due feeds on their configured interval, using
  `RssReader.Rss` to fetch/parse and `RssReader.Data` to persist.
- **`RssReader.Data`** — EF Core entities, `DbContext`, and migrations, targeting PostgreSQL. Entities *are*
  the domain model; there is no separate `RssReader.Core` project splitting "pure domain" from persistence,
  since nothing so far justifies that extra layer. Introduce one later only if real domain/business logic
  emerges that genuinely doesn't belong in `Data`.
  - Also references `RssReader.ServiceDefaults`, since the Aspire PostgreSQL EF Core client integration
    (`Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`) is wired up here, not just in the hosts.
- **`RssReader.Rss`** — feed fetching and parsing (via `System.ServiceModel.Syndication`) plus "which feeds
  are due to be polled" logic. No dependency on `Data` or the hosts.
- **`RssReader.AppHost`** — Aspire orchestration for local dev: wires up Postgres, Api, Worker, Web, and EF
  Core migrations, and hosts `Aspire.Hosting.Testing`-based integration tests.
- **`RssReader.ServiceDefaults`** — shared OpenTelemetry/health-check/resilience wiring referenced by
  `Api`, `Worker`, `Web`, and `Data`.

Api and Worker coordinate through the shared PostgreSQL database for feed data — no job queue between them
is needed (see [Real-Time Updates and Hydration](#real-time-updates-and-hydration) for why subscribing to a
feed never needs one). Starting in v2, Redis Pub/Sub carries ephemeral live-update notifications from Worker
to Api's SignalR hub; it is not a work/job queue.

## Data Model Shape

> Entity/table names below (`Entry`, `EntryState`, `FilteredEntry`, ...) communicate the conceptual shape and
> relationships, not a literal schema spec — exact names, column types, indexes, and constraints are
> implementation details to be finalized when writing the actual EF Core entities/migrations.

Feeds and their entries are **global/shared**: one `Feed` row and one set of `Entry` rows per unique feed
URL, fetched and parsed once by the Worker regardless of how many users subscribe to it. Per-user data lives
in separate tables:

- **`Subscription`** (user + feed) — plus future per-subscription settings such as poll interval or folder.
- **`EntryState`** (user + entry) — **dense, but only for entries actually visible to the user**: every
  ingested entry that isn't filtered out gets exactly one row, tracking read/unread and (later) starred
  state. Chosen over a sparse "only deviations from default" table because it makes listing/counting a plain
  join with no `LEFT JOIN`/`COALESCE` for default state, which matters more than the extra storage.
- **`FilteredEntry`** (user + entry + `FilterRuleId` + timestamp) — audit-only record created instead of an
  `EntryState` row when a filter rule matches an entry at ingestion (see
  [Content Filtering](#content-filtering)). Never joined into normal feed-listing/read paths, so the hot
  read path carries no filtered-state checks at all.

This avoids duplicate fetching/storage when multiple users subscribe to the same feed, and keeps the
single-user-now/multi-user-later transition additive rather than a rewrite.

Feed-format variance and extensions (RSS 2.0 vs. Atom 1.0 structural differences, `ElementExtensions`,
namespaced/custom fields) are not modeled relationally — they're captured in a `jsonb` column on `Entry`
(and/or `Feed`) alongside the normalized core fields every entry has regardless of format (title, link,
published date, author, summary/content, guid, feed id).

## Feed Syncing

- **Scheduling**: per-feed configurable polling interval (tracked per subscription/feed), not one global
  fixed interval — avoids over-polling slow-updating feeds.
- **Conditional GET**: the Worker stores `ETag`/`Last-Modified` per feed and uses conditional HTTP GET
  (`If-None-Match`/`If-Modified-Since`), so an unchanged feed short-circuits to a `304` instead of a full
  re-download/re-parse.

## Real-Time Updates and Hydration

**Subscribing to a feed:**

- **Existing feed** (already tracked, has entries): needs to create the dense `EntryState` rows for the
  user's newly-subscribed feed (see [Data Model Shape](#data-model-shape) — now finalized). Exact hydration
  mechanics (bulk-insert scope, whether it stays synchronous for arbitrarily large backlogs, whether existing
  filter rules apply to the historical backfill given filtering is otherwise non-retroactive) are **not yet
  decided** — revisit as its own topic.
- **Brand-new feed** (nobody subscribed yet): the Api fetches and parses it synchronously as part of the
  add-feed request anyway, to validate the URL and surface errors (e.g. not-found, invalid XML) or a preview
  in the form — the first batch of `Entry`/`EntryState` rows is stored as part of that same call.

Whatever the hydration mechanics turn out to be, both cases are expected to stay within a normal
request/response cycle; no durable Api→Worker job queue is currently expected to be needed.

**Live client updates (v2)**: when the Worker's background polling finds new entries for a feed, connected
clients should reflect that without a manual refresh — new entries appearing in an open feed view, unread
counters updating, and a feed's sync-health status ("last synced", "sync failing") updating live.

- **Transport**: SignalR, hosted by `RssReader.Api`, consumed by `RssReader.Web` — idiomatic for Blazor
  WebAssembly, with automatic WebSocket/SSE/long-polling fallback.
- **Worker → Api bridge**: `RssReader.Worker` publishes one small event per feed per sync cycle (e.g.
  `{FeedId, NewEntryCount}`, not one per entry) over **Redis Pub/Sub**; `RssReader.Api` holds a dedicated
  listening connection and relays to the affected users' SignalR connections. At-most-once delivery is
  acceptable since a missed event is corrected by the client's next event or a reconnect-triggered refetch.
  - Postgres `LISTEN`/`NOTIFY` was considered and would technically have enough headroom at this volume (one
    event per feed per sync, not per entry), but was rejected to avoid giving the OLTP database a
    message-bus role.
  - RabbitMQ was considered and rejected: its durable-queue/routing guarantees aren't needed for ephemeral
    UI hints.
- **Cross-device/tab read-state sync** (e.g. marking an entry read on one device updates another open
  session) needs no cross-process messaging: the write happens via an `RssReader.Api` call, and
  `RssReader.Api` already hosts the SignalR hub, so it pushes to the user's other connections in-process.
- Redis is therefore added to the stack for both Pub/Sub and caching feed/entry data and derived API
  responses — both rarely change once ingested, so caching is a natural fit and gives the Redis dependency a
  second, non-real-time-only justification.

This is a v2 concern (see [Milestones](#milestones)) — v1 ships with manual refresh only. The hydration bound
above applies from v1, since it's a correctness concern (a new subscriber to an existing feed should see
recent history immediately), not a UX polish item.

## Authentication

Authentication is fully delegated to the user's existing **Authentik** instance via OIDC — no local
password/user store (ASP.NET Core Identity is not used):

- **`RssReader.Api`** validates JWT bearer tokens issued by Authentik (standard OIDC/JWT bearer middleware).
- **`RssReader.Web`** (Blazor WebAssembly) performs the OIDC Authorization Code + PKCE flow against Authentik
  and sends the resulting token as a bearer token to the API.
- Internal user records are keyed off the OIDC subject (`sub`) claim, not a locally managed credential.

## Content Filtering

Per-user content filtering rules (e.g. "hide entries whose title starts with `Unimportant:`") are evaluated
against each entry **once, at ingestion time** — never retroactively. A match produces a `FilteredEntry`
audit row (referencing which rule matched) instead of an `EntryState` row, so the entry is simply absent from
that user's visible feed. Editing or deleting a rule later never re-evaluates existing entries: an old
`FilteredEntry` row stays an honest snapshot of "this rule matched this entry at the time," which is useful
for auditing/debugging a rule's behavior even after the rule itself changes, and avoids the cost of
re-evaluating a feed's entire history every time a rule changes. A future explicit "recover" helper could
promote a specific `FilteredEntry` row into a real `EntryState` row on demand, but no automatic
re-evaluation/bulk-recovery is planned. Other users subscribed to the same feed, who may have different (or
no) filter rules, are entirely unaffected, since filtering only ever touches the affected user's own rows.
Filtering acts on ingested entries (effectively a server-side "don't add this to my feed" rule), not just
client-side search/UI filtering over an already-displayed list. Filter rules only hide entries — they do not
auto-star or auto-file entries into folders (see
[Inspiration and Explicit Non-Goals](#inspiration-and-explicit-non-goals)).

## Milestones

No time estimates — ordered by priority/dependency only.

1. **v1 — MVP**: add a feed by URL, fetch/sync it, list its entries, mark entries read/unread.
2. **v2**: folders/tags, search, per-user content filtering rules, starring/favorites (saving entries for
   later), and real-time live updates (SignalR + Redis Pub/Sub — new entries, unread counters, sync-health
   indicators) — prioritized above OPML, since filtering in particular is the feature most worth having
   early.
3. **v3**: OPML import/export.
4. **Client expansion** (after the web app is solid): browser extension (including a quick-subscribe flow),
   MAUI/Blazor Hybrid mobile client.

## Inspiration and Explicit Non-Goals

[Feeder.co](https://feeder.co) (a paid RSS reader the project owner currently uses) was reviewed purely for
feature-set inspiration, not to copy. Relevant overlap with the milestones above: keyword-based filtering,
folders, and starring. Explicitly **out of scope**:

- Notifications.
- A "simple" (vs. compact) reading-mode toggle.
- A multi-column live dashboard view.
- Auto-starring or auto-filing entries into folders via filter rules — rules only hide/show entries.
- Team/shared-workspace collaboration features.

## Open Questions

Left open deliberately — revisit when reached:

- Whether `aspire publish` will generate usable production Docker Compose deployment artifacts for the VPS,
  or whether `docker-compose.yml` ends up hand-maintained with Aspire used only for local dev orchestration.
  To be trialed once there's something real to deploy.
