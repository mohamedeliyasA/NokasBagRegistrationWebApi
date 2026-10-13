namespace Nokas.CashMgmt.Common.Results;

public enum ErrorKind
{
    Validation,
    Conflict,
    NotFound,
    Unauthorized,
    DownstreamUnavailable,
    Internal
}

public sealed record Error(ErrorKind Kind, string Code, string Message, string? Field = null)
{
    public static Error Validation(string code, string message, string? field = null) =>
        new(ErrorKind.Validation, code, message, field);

    public static Error Conflict(string code, string message, string? field = null) =>
        new(ErrorKind.Conflict, code, message, field);

    public static Error NotFound(string code, string message) =>
        new(ErrorKind.NotFound, code, message);

    public static Error Unauthorized(string code, string message) =>
        new(ErrorKind.Unauthorized, code, message);

    public static Error DownstreamUnavailable(string code, string message) =>
        new(ErrorKind.DownstreamUnavailable, code, message);

    public static Error Internal(string code, string message) =>
        new(ErrorKind.Internal, code, message);
}

public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }

    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(Error error) => new(false, default, error);

    public static implicit operator Result<T>(Error error) => Failure(error);
}