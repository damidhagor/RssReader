# CI/CD

This document describes the automated GitHub Actions workflows that run on this repository.

## Guiding Principle

Every diagnostic fails the job: **compiler errors**, **Warning-severity build/analyzer diagnostics**
(promoted to build errors via `-warnaserror`), **any `dotnet format` drift** (style/formatting, checked
at `--severity info` — the broadest threshold, so it also catches suggestion-level rules that never
appear in build output), **test failures**, and **High/Critical severity vulnerabilities**. Only
**Moderate/Low severity vulnerabilities** remain non-blocking (`::warning::` annotation only).

This intentionally trades "warnings are fine, just don't get lost" for a much simpler and stricter rule:
a failing job blocks merging, shows a red X in the compact checks list without any extra click, and both
`dotnet build` and `dotnet format` already emit native `##[warning]`/`##[error]` GitHub Actions annotations
for every diagnostic when run under `GITHUB_ACTIONS=true`.

## `.github/workflows/ci.yml`

Validates the solution (`src/RssReader.slnx`).

- **Triggers:** `pull_request`, scoped via `paths` to `src/**`, the workflow file itself,
  `.github/scripts/format-check.sh`, `global.json` and `Directory.Packages.props` — so doc-only PRs don't
  trigger it. `push` to `main` has **no** path filter — every merge to `main` always runs the full
  build/test/coverage pipeline regardless of what changed.
- **Runner:** `ubuntu-latest`, with `actions/setup-dotnet@v6` installing the `11.0.x` SDK.
- **Steps:**
  1. Restore.
  2. **Build** — `dotnet build -c Release --no-restore -p:EnforceCodeStyleInBuild=true -warnaserror`.
     Fails on compiler errors and on any Warning-severity analyzer/style diagnostic (promoted to an error
     by `-warnaserror`). Diagnostics are surfaced via the .NET SDK's own native GitHub Actions
     annotations — no custom parsing.
  3. **Format check** — runs `.github/scripts/format-check.sh <solution>`, which invokes `dotnet format
     --verify-no-changes --severity info --no-restore` (no rules excluded by default — see the script's
     header comment for how to add an exclusion later if a specific rule needs it), runs with
     `if: always()` so it still executes (and reports its own findings) even if the Build step failed.
     Fails on any formatting/style drift at `info` severity or above — i.e. everything `dotnet format`
     recognizes, since `info` is its lowest severity. The script also converts `dotnet format`'s
     plain-text output into GitHub Actions `::error`/`::warning` annotations and a deduplicated, grouped
     (by severity, rule, message) job-summary table (Rule | Severity | Message | Locations), since
     `dotnet format` itself only prints plain text.
  4. **Test + coverage** — runs the TUnit test suite via `dotnet test -- --report-trx --coverage
     --coverage-output-format cobertura` (TRX + Cobertura output). Fails on test failures; runs with
     `if: always()` so it still executes even if the Format check step already failed.
  5. **Reporting** — each step only runs when its required input files actually exist (guarded via
     `hashFiles(...)` in its `if:` condition), so a real build failure doesn't cascade into a wall of
     unrelated report-generation failures: `dorny/test-reporter` publishes pass/fail results from the TRX
     files as PR check annotations (`fail-on-empty: false`), `danielpalme/ReportGenerator-GitHub-Action`
     turns the Cobertura files into a markdown coverage summary posted to the job summary, and the full
     HTML coverage report is uploaded as a workflow artifact.

> **Known limitation:** `dorny/test-reporter` needs a `GITHUB_TOKEN` with `checks: write`, which forked
> `pull_request` runs don't receive. For a PR opened from a fork, the test-reporter step may silently
> no-op instead of publishing a check — not currently an issue since this repo doesn't receive external
> fork PRs. Revisit if that changes (standard fix is a two-workflow `workflow_run` split).

## `.github/workflows/dependency-check.yml`

Scans the solution's (`src/RssReader.slnx`) NuGet dependencies (direct + transitive) for known
vulnerabilities.

- **Triggers:**
  - `schedule` — nightly at 03:00 UTC, so CVEs published against unchanged dependencies are still
    caught even with no new commits.
  - `workflow_dispatch` — manual on-demand run.
  - `pull_request` — scoped via `paths` to `src/**` and the workflow's own files.
  - `push` to `main` — **no** path filter, so every merge to `main` always gets a full scan.
- **Runner:** `ubuntu-latest`, single SDK (only `dotnet list package` runs here, no test execution).
- **Steps:** restore `src/RssReader.slnx`, then run `.github/scripts/check-vulnerabilities.sh
  src/RssReader.slnx`, which:
  1. Runs `dotnet list package --vulnerable --include-transitive --format json` and parses the JSON
     output with `jq`.
  2. **High or Critical** severity findings emit a `::error::` annotation and fail the job.
  3. **Moderate or Low** severity findings emit a `::warning::` annotation only — the job still
     succeeds. This is the one non-blocking exception to the Guiding Principle above, deliberate
     because low-severity transitive-dependency findings are frequently not actionable on a short
     timeline and a hard fail there would block unrelated PRs too often.
  4. All findings (regardless of severity) are written as a markdown table to the job summary.

## Shared Scripts (`.github/scripts/`)

- **`format-check.sh <solution-path>`** — runs `dotnet format --verify-no-changes` and converts its
  plain-text diagnostic output into GitHub Actions `::error`/`::warning` annotations plus a job-summary
  table, grouped by `(severity, rule, message)` and sorted by severity then rule, with deduplicated
  file:line locations. Supports an optional `FORMAT_CHECK_EXCLUDED_DIAGNOSTICS` environment variable
  (space-separated rule IDs) if a specific rule ever needs excluding.
- **`check-vulnerabilities.sh <solution-path>`** — runs and parses `dotnet list package --vulnerable`
  for one solution, applying the High/Critical-fails vs. Moderate/Low-warns severity policy described
  above and writing a findings table to `$GITHUB_STEP_SUMMARY`.

## `global.json`

Adds an opt-in to the native Microsoft.Testing.Platform `dotnet test` runner (`{"test": {"runner":
"Microsoft.Testing.Platform"}}`), required for TUnit on modern .NET SDKs. This does **not** pin a
specific SDK version; it only selects the test runner.

## Not (yet) present

- **No NuGet publish workflow** — RssReader is an app/server + clients, not a published package. If a
  reusable library ever splits out of this repo, revisit adding one (see `DiscogsApiClient`'s
  `publish-nuget.yml` for a template).
- **No version-branch/release ceremony** — see `AGENTS.md`'s Branching & PR Workflow section. Revisit
  once the project actually ships versioned releases.
