# Agent Guidelines

**This document defines code styling conventions and project knowledge for AI agents working on this repository.**
**It is not user-facing documentation — it exists to prevent agents from producing inconsistent code.**

**Keep this document up to date.** When the user makes explicit styling decisions, updates framework/language versions,
or establishes new patterns, update this document to reflect those decisions. When upgrading to newer .NET versions
with newer C# language features, ensure the style guidelines are updated to prefer the latest modern idioms.

## General

- **Target framework: .NET 11, single-targeted, C# 15 (via explicit `LangVersion=preview`).** Shared MSBuild
  properties (`TargetFramework`, `LangVersion`, `Nullable`, `ImplicitUsings`, `AnalysisMode`) live in the root
  `Directory.Build.props`, auto-imported by every project — don't duplicate them per-`.csproj`; only override
  there when a specific project genuinely needs to differ.
  - **Verified against the actually installed SDK** (`11.0.100-preview.7...`): `net11.0` currently defaults to
    `LangVersion=14.0`, not 15.0 — MS's published "TFM → default LangVersion" table already lists .NET 11.x
    → C# 15, but that's the eventual/GA mapping, not what this preview SDK does today. `LangVersion=15.0` is
    outright rejected as an unrecognized value by this SDK; only the `preview` value currently unlocks C# 15
    features (union types, closed hierarchies, collection expression arguments, extension indexers, labeled
    `break`/`continue`) — confirmed by compiling minimal repros of each. `Directory.Build.props` sets
    `LangVersion=preview` for exactly this reason, with a comment to revisit once a .NET 11 SDK makes C# 15
    the TFM's own default.
- Use the latest stable C# language features (primary constructors, collection expressions, file-scoped namespaces, etc.).
- Prefer modern idioms over legacy patterns. Always use the newest C# features available for the target language version.
- Actively look for and remove redundancy that a newer language feature eliminates — e.g. a redundant constructor
  type name in a `new TypeName(...)` expression where the target type is already clear from context should use
  target-typed `new()` instead (see the Target-typed new rule below). Don't just apply new features to new code;
  when touching existing code, simplify it to the modern idiom if it's trivial to do so.
- Code should be self-explanatory. **Assume an experienced senior .NET dev is reading it** — don't add comments
  that just restate what the code already says, explain common language/SDK/tooling idioms and quirks, or
  narrate mechanics a senior dev already knows. No per-class/per-method boilerplate comment headers, no comment
  on every non-trivial line. The large majority of code needs no comment at all.
  - A comment is only warranted for something a senior dev genuinely couldn't infer from the code itself: a
    non-obvious business rule/invariant, a deliberate deviation from the approach a reader would expect, or a
    project-specific decision with real ambiguity. If in doubt, leave it out — rationale for decisions belongs
    in the commit/PR description, not narrated inline.
  - Prefer a clear name/type/structure over a comment that explains an unclear one. If a comment is only
    needed because the code is confusing, fix the code first.
  - Avoid verbose XML documentation on internal types — keep doc comments brief or omit them when the implementation is clear from reading.
- **XML documentation is for the public API surface only.** Only document `public`/`protected` members that
  are part of a project's external contract (e.g. a library consumed by other projects in the solution). Do not
  add XML doc comments to `internal`/`private` members — add a brief inline comment only if the implementation
  genuinely isn't self-explanatory from reading it.
- **Member accessibility modifiers should reflect the member's intended accessibility on its own terms, not be
  downgraded just because the containing type happens to be `internal`.** A `public` member on an `internal` class
  is not a mistake — its effective visibility is still capped to the assembly by the containing type, but declaring
  it `public` means promoting the containing type to `public` later requires no member-by-member audit. Apply the
  "public API surface only" XML-doc rule based on the member's own declared accessibility (`public`/`protected`),
  regardless of whether the containing type is `public` or `internal`.
- Attribution comments (crediting authors of referenced implementations) must always be preserved.

## Diagnostics and Warnings

- All diagnostic messages must be checked for a correct implementation — this includes compiler warnings,
  analyzer warnings, and IDE-level diagnostics (e.g. suggestions, refactoring proposals, code-style hints).
- This does **not** mean every diagnostic must be auto-fixed. Where it isn't clear whether a diagnostic is
  critical, or whether fixing it would introduce other problems or contradict existing code/design
  decisions, at minimum triage it and surface it to the user for their evaluation rather than silently
  fixing or silently ignoring it.

### Full Diagnostics Check Procedure

A normal `dotnet build` (even incremental, even with `AnalysisMode=All`) does **not** surface everything —
IDE-only diagnostics (`IDE00xx`) and anything below `warning` severity (`suggestion`/`silent`, e.g. many
`csharp_style_*` rules in `.editorconfig`) are invisible to it. Before considering a non-trivial change
(or a dedicated diagnostics pass) complete, run **both** of the following steps, and evaluate **every**
diagnostic they report — including `message`/`suggestion`-level ones, not just `warning`/`error`:

1. **Clean, non-cached rebuild** (picks up compiler + analyzer warnings/errors at their full configured
   severity, forced to run instead of relying on incremental/cached state):
   ```powershell
   Get-ChildItem -Recurse -Directory -Include bin,obj | Remove-Item -Recurse -Force
   dotnet build src\RssReader.slnx -v normal /p:EnforceCodeStyleInBuild=true -t:Rebuild > build.log 2>&1
   ```
2. **IDE/style diagnostics pass** (surfaces `IDE00xx` and suggestion/silent-severity style rules that step 1
   still won't show, even with `EnforceCodeStyleInBuild=true`):
   ```powershell
   dotnet format src\RssReader.slnx --verify-no-changes --severity info --no-restore > format.log 2>&1
   ```

Repeat both steps against any other solution file added to the repo later (e.g. if a client app ends up in
its own `.slnx`) — each solution is only diagnosed by explicitly running these commands against it.

**Run this procedure before every `git push`/PR creation on a branch with code changes, not just once at
the end of a large task.** CI runs an equivalent format/analyzer check and will fail the build if this is
skipped — treat "push" and "open/update a PR" as the trigger, the same as "task considered complete".
If CI fails anyway (e.g. a diagnostic wasn't reproduced locally — different OS, stale local `obj`/`bin`,
etc.), fetch and read the actual failing CI job log (e.g. `gh run view --job <id> --log-failed`) rather
than only re-running the local procedure and assuming it will surface the same errors.

Notes for parsing results correctly:
- Redirect output to a file rather than reading the console directly — PowerShell's console width wraps
  long diagnostic lines and breaks line-based parsing; `Tee-Object` to a real console still wraps.
- If a solution multi-targets several TFMs, MSBuild prints each build diagnostic once per targeted TFM,
  and again in the end-of-build summary — so raw line counts overstate real occurrences. Deduplicate by
  stripping the trailing `[project::TargetFramework=X]` suffix and the `CoreCompile`-vs-summary
  duplication before treating a count as authoritative.

Once both passes are run, **triage every finding with the user** per the policy above: fix, suppress with
rationale (e.g. `.editorconfig` `dotnet_diagnostic.<CODE>.severity`), or explicitly accept as a known
tradeoff — but do not silently resolve or silently ignore any of them, regardless of severity tier.

## Formatting

### Line Endings

- This repository uses **LF** line endings, enforced by `.editorconfig` (`end_of_line = lf`) and `.gitattributes`
  (`* text=auto`, normalizing to LF in the repo regardless of local `core.autocrlf`).
- When creating or editing files, always use LF — never introduce CRLF, and never mix line-ending styles within
  the same document. Verify line endings after edits if there's any doubt (e.g. after tool-based file creation/edits
  that may default to the platform's native line ending).

### Braces

- **Always** use curly braces `{}` around `if`, `else`, `foreach`, `for`, `while`, and `using` blocks — even for single-line bodies.
- This means single-line/brace-less forms like `if (x) return;` or `if (x) throw new ...;` are **never** allowed, no matter how short the body is — always write the braced multi-line form.
- This rule does **not** apply to expression-bodied members (`=>`), which are a different construct.
- Early return conditions must not be squashed into one-liners.

### Expression Bodies

- Single-expression methods, properties, and operators **should** use expression bodies (`=>`).
- Multi-statement methods **must** use block bodies with explicit `return`.
- When an expression body breaks across lines, the `=>` goes on the **new line**, not at the end of the signature:
  ```csharp
  // Correct
  public bool IsActive(DateTimeOffset now)
      => Start <= now && now < End;

  // Incorrect
  public bool IsActive(DateTimeOffset now) =>
      Start <= now && now < End;
  ```

### Line Length and Wrapping

- A code line is allowed to be moderately long — don't break simple expressions unnecessarily.
- When function call arguments are split across lines, split **all** arguments — one per line. Never mix inline and split arguments.
- LINQ method chains should be split across lines when the chain exceeds ~2 calls or is hard to scan at a glance.
- Primary constructor parameters should be split across lines when there are 3+ parameters.

### Blank Lines

- One blank line between members.
- No excessive blank lines within method bodies.

### Return Statements

- When two simple return branches differ only by a condition, prefer a ternary over `if`/`else`:
  ```csharp
  // Preferred
  return items.Count > 0
      ? Result<T>.Success(items)
      : Result<T>.Empty();

  // Avoid
  if (items.Count > 0)
  {
      return Result<T>.Success(items);
  }

  return Result<T>.Empty();
  ```

## Types and Records

- **Records**: Prefer `sealed record` for immutable model types. Records auto-implement `IEquatable<T>` — never declare it explicitly.
- **Primary constructors**: Use for records, classes, and structs where the constructor initializes properties/fields. For structs that need to access fields on `other` instances (e.g. in `Equals`), declare the field explicitly and initialize it from the primary constructor parameter.
- **Readonly structs**: Use `readonly struct` for small, immutable value types.
- **The `field` keyword (C# 14)**: For a property that needs a small amount of accessor logic (validation,
  normalization, change notification) but not a fully independent backing field, use `field` instead of
  declaring an explicit private field:
  ```csharp
  // Preferred
  public string Title
  {
      get;
      set => field = value ?? throw new ArgumentNullException(nameof(value));
  }

  // Avoid (explicit backing field no longer needed for this case)
  private string _title;
  public string Title
  {
      get => _title;
      set => _title = value ?? throw new ArgumentNullException(nameof(value));
  }
  ```
  Only fall back to an explicit backing field when the property genuinely needs to expose or share the field
  itself (e.g. passing it by `ref`). Watch for an existing field literally named `field` in the same type — it
  is shadowed by the keyword within an accessor; disambiguate with `@field`/`this.field` or rename it.
- **Closed hierarchies (C# 15)**: When a type represents a fixed, known set of alternatives that must never
  gain a case outside this assembly (e.g. a state machine's states), mark the base `closed` instead of
  relying on convention/`sealed` leaf types with an unsealed base:
  ```csharp
  public closed record class SyncState;
  public sealed record class Idle : SyncState;
  public sealed record class Syncing(int PercentComplete) : SyncState;
  public sealed record class Failed(string Error) : SyncState;
  ```
  A `switch` over a `closed` hierarchy that handles every direct descendant is exhaustive and needs no
  `default`/discard arm — prefer this over a manual exhaustiveness comment. Mark intermediate descendants
  `closed` too if the hierarchy is more than one level deep and every level needs exhaustiveness.
- **Union types (C# 15)**: For a value that's genuinely "one of these unrelated types" (not a hierarchy you
  own/control, e.g. modeling a result as "one of several existing DTOs"), prefer a `union` over a hand-rolled
  discriminated union wrapper:
  ```csharp
  public union FetchResult(FeedItems, NotModified, FetchError);
  ```
  Prefer a `closed` hierarchy instead when you're defining the case types yourself and they share behavior —
  `union` is for combining otherwise-unrelated existing types.

## Error Handling: Result Pattern Over Exceptions

- **Prefer explicit result-returning types over throwing for expected/domain failure outcomes.** Reserve
  exceptions for genuinely exceptional, unexpected conditions (bugs, infrastructure failures, violated
  invariants) — not for anticipated outcomes like "feed URL unreachable", "feed not modified since last
  fetch", or "item already marked read". Exceptions are for the caller *not* to have to handle case-by-case;
  a Result-style return forces the caller to consider every outcome at compile time.
- Model a method's possible outcomes as a **closed hierarchy** (when you own/define every case and they share
  a shape) or a **union** (when combining otherwise-unrelated existing types) rather than a boolean +
  nullable-output pair or a generic `bool TryX(out T value)`:
  ```csharp
  public closed record class FetchResult;
  public sealed record class Fetched(FeedItems Items) : FetchResult;
  public sealed record class NotModified : FetchResult;
  public sealed record class FetchFailed(string Reason) : FetchResult;

  public async Task<FetchResult> FetchAsync(Uri feedUrl, CancellationToken cancellationToken)
  {
      // ...
  }
  ```
  Callers then get an exhaustive `switch` at every call site — a new case added later causes every existing
  `switch` expression without a fallback arm to fail to compile, which is the point.
- This doesn't ban exceptions outright: framework/BCL calls that only throw for genuine bugs or unrecoverable
  conditions (e.g. `ArgumentNullException` for a violated precondition) are still appropriate as exceptions,
  and guard-clause validation (see the Types and Records / general C# conventions above) still throws.
- This is an early, general project convention — revisit and refine it once real server/service boundaries
  exist (see `docs/ROADMAP.md`/`docs/ARCHITECTURE.md`), e.g. whether a shared `Result<T>`/`Result<TSuccess,
  TFailure>` helper type is worth introducing versus a bespoke closed hierarchy per operation.

## Collections and Expressions

- **Target-typed new (`new()`)**: Use only when the target type is explicitly declared on the left (e.g., fields, properties, or explicitly typed variables) or in constructor/method arguments where the parameter type is clear. Otherwise, prefer using `var` with the explicit constructor on the right (e.g., `var options = new FeedOptions();`).
  - **Diagnostics gap**: `IDE0090`/`csharp_style_implicit_object_creation_when_type_is_apparent` (configured in `.editorconfig`) only fires for explicit-typed variable declarations, field initializers, and similar — **not** for `new TypeName(...)` in method-call-argument positions, even though the parameter type is just as apparent there. `dotnet format` cannot auto-detect or auto-fix that case; it must be applied and checked manually during review.
- Use **collection expressions** (`[]`) for empty collections and short initializers wherever the target type supports it.
- **Collection expression arguments (C# 15)**: When a collection's constructor needs a capacity/comparer/other
  argument, pass it via a leading `with(...)` element in the collection expression instead of dropping to the
  full constructor + range-add pattern:
  ```csharp
  // Preferred
  List<string> titles = [with(capacity: items.Length), ..items.Select(i => i.Title)];
  HashSet<string> tags = [with(StringComparer.OrdinalIgnoreCase), "rss", "atom"];

  // Avoid
  var titles = new List<string>(items.Length);
  titles.AddRange(items.Select(i => i.Title));
  ```
- Use `default` only when the target type doesn't support collection expressions.
- **`params` collections (C# 13)**: `params` is no longer array-only — it also supports `Span<T>`,
  `ReadOnlySpan<T>`, and any `IEnumerable<T>`-shaped collection with an `Add` method. Prefer `params
  ReadOnlySpan<T>` for hot-path APIs instead of avoiding `params` altogether — it doesn't allocate the way
  `params T[]` does. Reserve collection expressions/explicit `List<T>` construction at the call site for cases
  where the callee genuinely needs an owned, heap-allocated collection (e.g. it stores or mutates it).

## Using Directives

- Use `global using` in `Usings.cs` for namespaces used across many files.
- **Never** re-declare a global using as a local using — this produces redundant imports.
- Remove unused using directives.
- **No automated check catches unused usings by default** (`IDE0005` cannot be enforced by `dotnet build`, and
  is easy to accidentally exclude from `dotnet format` checks too — see `docs/CI_CD.md` if this repo ever
  excludes it for a specific reason). Agents must proactively double-check changed files for unused usings
  after editing — don't rely on any build/CI output to catch them unless it's confirmed to cover `IDE0005`.

## Extension Members (C# 14)

- Prefer the new `extension` block syntax over classic static-method extensions when adding **extension
  properties** or **static extension members**, since only the block syntax supports those:
  ```csharp
  public static class FeedItemExtensions
  {
      extension(FeedItem item)
      {
          public bool IsUnread => item.ReadAt is null;
      }
  }
  ```
- Classic `public static T Method(this T item, ...)` extension methods are still fine and idiomatic for plain
  extension methods with no property/static-member counterpart — don't migrate existing simple extension
  methods to `extension` blocks just for the sake of it.
- **Extension indexers (C# 15)**: available for adding indexer-style access to a type you don't own (e.g.
  `sequence[index]` on an `IEnumerable<T>`). Use sparingly — an extension indexer can make call sites read as
  if the indexer were a real member of the type, which is convenient but can obscure where the behavior
  actually lives; prefer a clearly-named extension method when the indexer semantics aren't obvious.

## Null-Conditional Assignment (C# 14)

- Use `?.`/`?[]` on the left-hand side of an assignment (including compound assignment like `+=`) instead of
  an explicit null-check-then-assign:
  ```csharp
  // Preferred
  subscriber?.LastSeenAt = DateTimeOffset.UtcNow;

  // Avoid
  if (subscriber is not null)
  {
      subscriber.LastSeenAt = DateTimeOffset.UtcNow;
  }
  ```
- The right-hand side is only evaluated when the left side isn't null — don't rely on a right-hand side with
  side effects always running.
- Not valid for `++`/`--`; keep the explicit null-check form for those.

## Concurrency

- **`System.Threading.Lock` (C# 13 / .NET 9+)**: For new mutual-exclusion code, declare the lock field as
  `System.Threading.Lock` rather than a plain `object`. The `lock` statement recognizes the `Lock` type and
  emits the more efficient `Lock.EnterScope()`-based code automatically — no call-site changes needed beyond
  the field's declared type:
  ```csharp
  private readonly Lock _gate = new();

  public void Update()
  {
      lock (_gate)
      {
          // ...
      }
  }
  ```
  Only keep `lock (object)` where the lock target is intentionally something other than a dedicated field
  (e.g. locking on a passed-in instance) or where a pre-existing `object` field is shared with other API
  surface that requires it stay `object`.

## Control Flow

- **Labeled `break`/`continue` (C# 15)**: When steering control flow out of or across a nested loop, use a
  labeled `break`/`continue` targeting the outer loop instead of a boolean sentinel flag or a `goto`:
  ```csharp
  // Preferred
  outer: foreach (var feed in feeds)
  {
      foreach (var item in feed.Items)
      {
          if (item.IsDuplicate)
          {
              continue outer;
          }
      }
  }
  ```
  `IDE0410` flags the boolean-flag/`goto` patterns this replaces — treat it like any other flagged diagnostic
  under the Diagnostics and Warnings policy above.

## Other Notable Modern Features (Lower Priority / Situational)

These are available with the C# 15 target but come up less often than the items above — apply them when the
situation genuinely calls for it rather than looking for excuses to use them:

- **Partial properties/indexers (C# 13) and partial constructors/events (C# 14)**: extend the existing
  `partial` toolkit (already used for `partial` classes/methods). Relevant mainly for generated-code split
  scenarios (declaring/implementing halves) — not a default pattern for ordinary hand-written types.
- **Implicit `Span<T>`/`ReadOnlySpan<T>` conversions (C# 14)**: APIs can now accept/return spans more
  naturally without explicit `.AsSpan()` calls at every call site. Worth knowing about when designing
  performance-sensitive parsing/formatting code (e.g. feed content parsing); not relevant for typical
  higher-level application code.
- **User-defined compound assignment operators (C# 14)** and **overload resolution priority attribute (C#
  13)**: both are library-author tools for fine-tuning operator/overload behavior. Only relevant if/when this
  repo grows a shared library project consumed by multiple other projects in the solution.
- **Memory safety pointer relaxations (C# 15, preview-gated)**: only relevant if the codebase ever needs
  `unsafe` code (e.g. low-level performance work). Not expected to come up in normal application/server code.

## Naming

- When renaming a method/type/endpoint, grep the whole solution (interfaces, implementations, and especially
  test file/class names) for the old name afterward — partial renames that leave stale naming on test classes
  are easy to miss since they still compile and pass.

## Testing

- Use TUnit as the test framework (see `docs/TESTING.md` for naming conventions and patterns).
- Keep test infrastructure consistent with existing patterns in the project.
- **Exception assertions must be precise, not just type-checked.** Never assert a bare `.Throws<Exception>()`
  or a supertype when a specific derived exception type is actually thrown — always assert the exact concrete
  type. Additionally:
  - Always assert the exception's message via `.WithMessage(...)` (exact) or `.WithMessageContaining(...)`
    (stable substring, only when the full message is dynamic/volatile).
  - For `ArgumentException`/`ArgumentOutOfRangeException` (and other types exposing `ParamName`), always
    assert `exception.ParamName` equals the exact validated parameter name — capture the exception from
    `Throws<T>()` into a variable and add a follow-up assertion rather than relying on the type check alone.
    Watch for guard clauses on nullable value parameters (e.g. `int?`) that validate the unwrapped `.Value`
    — `ParamName` in that case is `"paramName.Value"`, not `"paramName"`.
  - These precise assertions exist to document the real API's (error) behavior as regression protection —
    a type-only check doesn't catch the code changing which argument it validates first, or changing/removing
    a message.
- **Tests must assert the actual returned data, not merely that a call succeeded.** A "happy path" test that
  only checks a response was returned (with no assertions on its properties/contents) does not verify
  correct behavior — assert the specific fields relevant to the scenario under test.

## Git Workflow

- **NEVER commit changes without explicit user approval first.**
- **NEVER push changes (e.g. `git push`) without explicit user approval first**, even if a commit was already approved earlier — pushing is a separate approval step.
- **NEVER run `git add`/`git stage` (or any other staging/unstaging command) on your own initiative** — some
  users keep the index deliberately curated for their own review and handle staging themselves. Only stage
  files as the immediate, same-step precursor to a `git commit` that the user has already explicitly
  approved (e.g. `git add -A; git commit ...`); never stage speculatively "for later" or to preview a diff,
  and never unstage/reset the index either. If staged changes are already present when you arrive at a
  commit step, assume the user staged them deliberately and leave that selection alone.
- Always present changes to the user for review before running `git commit` or `git push`.
- When presenting changes for review, **propose a suitable commit message** following conventional commit format.
- The user must approve both the changes **and** the commit message before proceeding.
- This applies to all commits and pushes, including code changes, test recordings, documentation updates, etc.
- After making changes, inform the user what was changed and wait for their approval to commit and/or push.

## Branching & PR Workflow

- **`main` is protected.** All changes — features, fixes, docs, CI tweaks — go through a dedicated branch and
  a pull request. Direct commits to `main` are not possible.
- **Branch naming:** short, kebab-case, prefixed by intent where it helps scanning history (e.g.
  `feature/...`, `fix/...`, `chore/...`, `docs/...`) — no strict enforcement, but keep it descriptive.
- **Every PR is merged via a squash commit.** This keeps `main`'s history one commit per merged change,
  regardless of how many commits accumulated on the branch during review.
- No version-branch layering or release ceremony for now — that's deferred until the project actually ships
  versioned releases (at which point this section should be revisited and expanded, similar in spirit to
  how `DiscogsApiClient` handles it once it became a published package).
- CI (build, format check, tests, dependency/vulnerability scan — see `docs/CI_CD.md`) runs on every PR and
  must be green before merging; it applies the same "every diagnostic fails the job" policy regardless of
  how informal the branching model is.

## Project Structure

Not yet finalized — the concrete solution/project layout (server, client(s), shared libraries) depends on the
architecture decisions from the roadmap-brainstorming pass (see `docs/ROADMAP.md`). Update this section once
that structure exists.
