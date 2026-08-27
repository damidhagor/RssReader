# Testing Guidelines

This document outlines the testing strategy, frameworks, and conventions for the RssReader test suite.
Follow these guidelines when adding or modifying tests.

## Test Framework: TUnit

The project uses **TUnit** as its testing framework. Key features and patterns to use:

- **Async/Await**: Make all test methods asynchronous (`public async Task`). Avoid synchronous `.Result` or `.Wait()` calls.
- **Fluent Assertions**: Use TUnit's built-in assertions:
  ```csharp
  await Assert.That(result).IsNotNull();
  await Assert.That(result.Id).IsEqualTo(expectedId);
  ```
- **Data-Driven Tests**: Use `[Arguments]` for parameterized tests:
  ```csharp
  [Test]
  [Arguments(-1)]
  [Arguments(0)]
  public async Task GetFeed_ShouldThrowArgumentOutOfRangeException_WhenIdIsInvalid(int feedId, CancellationToken cancellationToken)
  ```

---

## Test Naming Convention

To keep tests readable and maintain consistency, use the following naming convention:

`[MethodName]_Should[ExpectedBehavior]_When[StateUnderTest]`

### Examples:
- `GetFeed_ShouldReturnFeed_WhenIdIsValid`
- `GetFeed_ShouldThrowResourceNotFoundException_WhenFeedDoesNotExist`
- `MarkAsRead_ShouldNotChangeState_WhenAlreadyRead`

---

## Mocking External Dependencies

Tests should run deterministically and fast without relying on live external services (e.g. remote RSS/Atom
feed endpoints). Prefer recorded/fixture-based HTTP responses or fakes over hitting real network endpoints
in the default test run. Once the server's feed-fetching layer exists, add a concrete convention here for
how those fixtures are organized and refreshed (`DiscogsApiClient`'s response-recording setup is one
possible model, but not a decision made yet).

---

## Running Tests

From the repository root, once real test projects exist:

```powershell
dotnet test src\RssReader.slnx
```

---

## End-to-End (E2E) Testing

No decision has been made yet on whether/how to maintain E2E tests against a real feed-aggregation
pipeline. Revisit once the server/client architecture is settled (see `docs/ROADMAP.md`).
