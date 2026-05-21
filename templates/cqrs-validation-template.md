# CQRS Validation Template

Custom validation only. Do not add FluentValidation or another third-party validation package.

```csharp
public interface IRequestValidator<in TRequest>
{
    Task<RequestValidationResult> ValidateAsync(TRequest request, CancellationToken ct = default);
}

public sealed class ValidationRequestHandlerDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    IEnumerable<IRequestValidator<TRequest>> validators)
    : IRequestHandler<TRequest, TResponse>
{
    public async Task<TResponse> HandleAsync(TRequest request, CancellationToken ct = default)
    {
        var errors = new List<string>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, ct);
            if (!result.IsValid) errors.AddRange(result.Errors);
        }

        if (errors.Count > 0)
            return ResultFailureMapper.ToResponse<TResponse>(errors);

        return await inner.HandleAsync(request, ct);
    }
}
```

Put cheap shape validation in validators when it removes repeated handler code. Keep aggregate/domain invariants in domain methods.
