# CQRS Test Templates

## Handler Unit Test

```csharp
[TestMethod]
public async Task Given_InvalidCommand_When_HandlerRuns_Then_ReturnsFailure()
{
    var command = new Create{Entity}Command(new DefaultRequest<{Entity}Dto>
    {
        Item = new {Entity}Dto()
    });

    var result = await _handler.HandleAsync(command);

    Assert.IsTrue(result.IsFailure);
}
```

## Validation Decorator Test

Test that invalid commands return a failed `Result` and do not call the inner handler.

## Architecture Test

- CQRS project has no Host dependency.
- CQRS project has no Infrastructure implementation dependency.
- No central request dispatcher, request bus, or generic `Send()` entrypoint.
- Reason: endpoint -> request -> handler wiring stays explicit and testable.
- No CQRS type implements `I{Entity}Service`.
- One command/query has one handler registration.
