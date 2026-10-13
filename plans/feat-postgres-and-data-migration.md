# feat: PostgreSQL support, export/import, and move to prod1

Status: draft, revised 2026-10-12. Botrax is decommissioned; SelfMX is now a shared dependency of the other prod1 apps.

## 1. Goal (product view)

Move the production SelfMX instance from the internal TrueNAS host to
`prod1.dualconsult.net` with **no visible change for its API clients**. Any app
that sends through `https://selfmx.dualconsult.net` must keep working with its
**current API key and no config change**.

Known and planned clients (to be confirmed, see §5):

| Client | Today | Notes |
| --- | --- | --- |
| Hawk | **Live on prod1** since 2026-10-12 | Uses the **same API key** as its TrueNAS instance |
| prod1 host alerts (`dc-alert`) | Live | Key `prod1` (to be rotated) |
| Agora | Planned on prod1 | Needs a **new** key and sender on SelfMX |
| Ando server | Resend-compatible `BaseUrl` | Unknown whether it is pointed at this instance |
| Others | ? | Read off the API key list on TrueNAS before cutover |

Three pieces of work:

1. **PostgreSQL becomes the default database everywhere** (dev setup, E2E, install script, deploy example, docs). SQL Server stays supported as an opt-in second option.
2. **Admin export/import.** Download one file from the old instance and upload it to an empty new instance.
3. **prod1 platform readiness** (code only). Deployment and cutover come later; see "Out of scope".

### Confirmed decisions

| Topic | Decision |
| --- | --- |
| Default database | **PostgreSQL.** SQL Server is opt-in. Existing MSSQL installs are detected from their connection string. |
| Schema management | EF Core migrations, one set per provider. Existing MSSQL databases get a baseline. `EnsureCreated` and `SchemaUpdater` are removed. |
| Export scope | Domains, API keys + key↔domain links, revoked keys, audit logs. **No SentEmails.** No Hangfire state. |
| Mechanism | Admin-only API + admin UI buttons. gzipped JSON. Import only into an **empty** target. |
| Scope | **Code changes only.** Stop before deploying. |

### Constraints that shape the code

- **Export will run on TrueNAS** (SQL Server, recent version). TrueNAS must be upgraded
  to this release **on its existing database** before exporting, so the release has to
  baseline the existing MSSQL schema into EF migrations without losing data (Phase B).
- **Every client keeps its key.** Hawk is already live on prod1 using the TrueNAS key, so
  import must keep key IDs, hashes, and salts exactly as they are (Phase C).
- **prod1 app contract** (`DualConsultInfrastructure/templates/app/README.md`): own
  Postgres 17 (`postgres-walg`), `<app>_migrator` role, port 8080, `/live` + `/ready`,
  UID 1654, read-only fs, DataProtection keys, rolling file logs (Phase D).

## 2. User flows

**Operator migration (one time, later; shown so the code supports it):**
1. An empty SelfMX + Postgres runs on prod1.
2. Upgrade TrueNAS SelfMX to the new version, then Admin UI → Settings → **Export data** → `selfmx-export-<date>.json.gz`.
3. prod1 Admin UI → **Import data** → upload. The page shows counts per table.
4. Check that domains show Verified, then send a test email from prod1.
5. Switch DNS for `selfmx.dualconsult.net` to prod1, then send a test email through each client app.

**Import guardrails:** import is refused (409) if any table in scope already has
rows. The file format is versioned, and an unknown version is rejected. Export
and import are both written to the audit log.

## 3. Work breakdown

### Phase 0 — Clean baseline + .NET 10

What: commit the existing uncommitted work, then upgrade to .NET 10 / EF Core 10.
Why: the new migrations should be generated with EF 10 from the start, so they don't have to be regenerated after an upgrade.

- Run the tests, then commit the current working-tree changes as they are (conventional commit).
- `net9.0` → `net10.0` for the API and tests. EF Core, SqlServer, Sqlite, Design, and HealthChecks packages → 10.x.
  Update the Dockerfile base images, `mise.toml`, and the CLAUDE.md "(.NET 9)" note.
- Check: `dotnet build`, `dotnet test`, and the client lint/typecheck all pass. Commit.

### Phase A — Provider abstraction + PostgreSQL

What: run the app against either provider, chosen by config.
Why: Postgres is the prod1 standard (about 200–300 MB versus at least 2 GB for MSSQL), and MSSQL users must not break.

- Config `Database:Provider` = `Postgres` (**default**) | `SqlServer`. If it's unset, detect from
  the connection string: `Server=`/`Data Source=` → SqlServer, so existing MSSQL installs
  (such as TrueNAS) need no change. Anything else → Postgres.
- Packages: `Npgsql.EntityFrameworkCore.PostgreSQL` 10.x and `Hangfire.PostgreSql`.
- `Program.cs`: one `AddSelfMxDatabase(provider, cs)` extension that registers both
  DbContexts (same retry/timeout policy) and the matching Hangfire storage.
- `AppDbContext`: keep the `IsSqlServer()` `nvarchar(max)` branch. Postgres uses `text` by default.
- Postgres differences to check and fix (the same list as Hawk's plan, so share the findings):
  - **UTC DateTimes:** Npgsql rejects `Kind=Unspecified` for `timestamptz`.
    Check that every write path, especially import deserialisation, produces
    `Kind=Utc`. The existing `UtcDateTimeConverter` read path needs checking.
  - **Case sensitivity:** Postgres compares case-sensitively, while MSSQL's default
    collation does not. Check domain name lookups, uniqueness, and from-address
    → domain matching in `Services/` and `Endpoints/`. Normalise to lowercase
    on write and query.
  - Raw SQL: remove the bracketed MSSQL DDL in `SchemaUpdater` (replaced by migrations).
- Make the Hangfire worker count configurable (`App:HangfireWorkers`) so prod1 can run 2–4 instead of `ProcessorCount*2`.
- `/system/status` reports the active provider.
- **Postgres is the default for this repo's own tooling:**
  - `docker-compose.dev.yml`: Postgres 17 is the default service on port **17403**.
    MSSQL moves behind a `sqlserver` compose profile, still on 17402.
  - `appsettings.Development.json` points at Postgres.
  - `playwright.backend.config.ts` runs against Postgres by default, with a `DB_PROVIDER=sqlserver` switch.
  - `deploy/docker-compose.yml` + `install.sh` ship a Postgres container. The stale SQLite strings go.
  - README, website, `llms.txt`, and CLAUDE.md describe Postgres first and SQL Server as the
    alternative ("SQL Server only" is removed).
  - Provider integration tests (Phase B) run against both.

### Phase B — EF Core migrations per provider

What: replace `EnsureCreated` + `SchemaUpdater` with real migrations.
Why: two providers × hand-written DDL won't scale, and the baseline is needed for the TrueNAS upgrade.

- Layout: separate migration projects `src/SelfMX.Migrations.Postgres` and
  `src/SelfMX.Migrations.SqlServer`, wired with `MigrationsAssembly(...)` (the
  EF "Migrations with Multiple Providers" pattern). Each has an
  `IDesignTimeDbContextFactory` for both contexts.
- Initial migration per provider and per context (`AppDbContext`, `AuditDbContext`)
  that matches the current schema exactly, including `RevokedApiKeys`,
  `LastCheckedAt`, and `AuditLogs.ResourceId(100)`.
- **Baseline on startup (MSSQL only):** if `__EFMigrationsHistory` is missing
  but `Domains` exists, check that the live schema matches what the
  initial migration expects (run the existing `SchemaUpdater` steps one final
  time), then insert the initial migration row and call `Migrate()`. Do this
  separately for the audit context, which needs its own history table name, e.g.
  `__EFMigrationsHistory_Audit`, because both contexts share one database.
- Tests stay on SQLite + `EnsureCreated`. There is no third migration set.
- Add an integration test that migrates a fresh Postgres and a fresh MSSQL and runs a smoke CRUD.
- Add a baseline test: build the DB the way the current release does
  (`EnsureCreated` + `SchemaUpdater`), start the new version, check there is no data loss and the history row exists.

### Phase C — Export / import

What: admin endpoints + UI for moving config and audit data between instances.
Why: makes the TrueNAS → prod1 move (and moves between providers) a single, repeatable operation.

- `GET /admin/export` (admin only) streams `application/gzip`:
  ```json
  { "formatVersion": 1, "sourceVersion": "0.9.x", "exportedAt": "…Z",
    "domains": [...], "apiKeys": [...], "apiKeyDomains": [...],
    "revokedApiKeys": [...], "auditLogs": [...] }
  ```
  Entities are exported field for field. `KeyHash`/`KeySalt`/IDs are kept as
  they are, which is what keeps every client's existing key working.
- `POST /admin/import` (admin only, multipart, higher body-size limit on this
  route only): checks that the target is empty, then inserts in FK order
  Domains → ApiKeys → ApiKeyDomains → RevokedApiKeys, then AuditLogs, all in one
  transaction shared across both contexts (same connection). Returns counts per table.
  After import, Hangfire picks domains up again on the next `VerifyDomainsJob` run.
- `ImportExportService` holds the logic, with thin endpoints. DTOs are separate
  from entities so the file format stays stable.
- The export file contains key hashes and salts, not plaintext keys. Still treat it as
  a secret: `0600`, transfer over SSH, delete after cutover.
- UI: "Data" section in admin settings with Export button, Import file picker,
  confirmation dialog, and a result summary. Zod schema for the import result.
- Tests: round-trip (seed → export → import into a fresh DB → compare), a
  non-empty target returns 409, a bad `formatVersion` returns 400, an imported API
  key still authenticates, and non-admins get 403.
- Update `llms.txt`, the README, and the website docs.

### Phase D — prod1 platform readiness (code only)

What: make SelfMX meet the prod1 app contract in
`~/Source/DualConsultInfrastructure/templates/app/README.md`.
Why: these are hard requirements of `deploy-app.sh` and the hardened container.

| Requirement | Today | Change |
| --- | --- | --- |
| HTTP on :8080 | image default `:17400` | none; prod1 sets `ASPNETCORE_URLS=http://+:8080`. The image default stays 17400 |
| `/live` + `/ready` | only `/health` | add `/live` (no checks) and `/ready` (DB check); keep `/health` for existing installs |
| Non-root UID **1654** | custom `selfmx` user, UID 1001 | switch to the base image's built-in `app` user (1654) |
| Read-only root fs, `/tmp` only | untested | audit for disk writes; test with `read_only: true` |
| DataProtection keys in `/var/lib/selfmx/keys` | not persisted | `PersistKeysToFileSystem`, path from `App:DataProtectionKeysPath` |
| Logs to stdout + daily files, 90 days | in-memory sink + console | **Serilog**: console + daily rolling file `selfmx-.log` (`App:Logging:File:{Enabled,Path,RetainedDays}`, off by default); keep the in-memory provider for `/system/logs` |
| Honors `X-Forwarded-*` | not configured | `UseForwardedHeaders` with known proxies/networks from config |
| Postgres as the `<app>_migrator` role, migrate at startup, Hangfire creates schema `hangfire` | — | covered by Phases A/B; Hangfire.PostgreSql schema name `hangfire` |
| Image pushed to `registry.dualconsult.net/dualconsult/selfmx` | ghcr only | add the push step to `build.csando` (don't run it in this effort) |

## Out of scope for this effort (stop before deploying)

The following are deliberately left out. **Re-read the current prod1 docs and
`DualConsultInfrastructure` before planning them**, because the platform is changing quickly:

- Releasing, and upgrading TrueNAS (SQL Server, recent version) to the release
  with export (the baseline in Phase B makes this safe).
- `scripts/new-app.sh selfmx selfmx.dualconsult.net`, deploy, backups, restore drill.
- Cutover: TrueNAS is reached through a **direct A record + port forward**. Lower the TTL,
  freeze key changes, export → import, repoint the A record to prod1, remove the port forward,
  and test each client (Hawk, prod1 `dc-alert`, Agora, …).

## 4. Housekeeping

- The working tree has uncommitted changes (`TokenEndpoints.cs`, `RevokedApiKey.cs`,
  `CleanupRevokedApiKeysJob.cs`, `docker-compose.dev.yml`, …). Commit or stash them before Phase A.
- CLAUDE.md says "SQL Server only". Update it, along with README, website,
  `~/Source/PORTS.md`, and the port table (17403).
- `deploy/docker-compose.yml` still has SQLite connection strings. Replace them with a
  Postgres-based example.

## 5. Delivery

- One conventional commit per phase (0, A, B, C, D) on the current branch, each with
  `dotnet test`, client lint and typecheck passing. No push, no release.
- Changing the image UID 1001 → 1654 is a breaking change for self-hosters with mounted volumes.
  Call it out in the release notes.
- After Phase D, a more capable reviewer independently reviews the whole change.

## 6. Open questions

- Which other clients (besides Hawk, prod1 `dc-alert`, Agora) use SelfMX? Read them off the
  API key list at cutover time. This doesn't block the code work.
