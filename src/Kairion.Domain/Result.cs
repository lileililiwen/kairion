namespace Kairion.Domain;

/// <summary>
/// A discriminated result type used to surface validation and not-found outcomes from
/// the application layer without exceptions. Errors are intentionally simple strings;
/// richer validation belongs in <see cref="DomainValidationException"/>.
/// </summary>
public readonly struct Result<T>
{
    public Result(T value)
    {
        Value = value;
        IsSuccess = true;
        Error = null;
    }

    private Result(string error, bool _)
    {
        Value = default;
        IsSuccess = false;
        Error = error;
    }

    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error) => new(error ?? "Unknown error.", false);

    public static implicit operator Result<T>(T value) => Success(value);
}
