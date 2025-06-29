namespace Lfm.Core.Models.Results;

/// <summary>
/// Represents the result of an operation that can succeed or fail
/// </summary>
/// <typeparam name="T">The type of data returned on success</typeparam>
public class Result<T>
{
    private Result(T? data, ErrorResult? error, bool success)
    {
        Data = data;
        Error = error;
        Success = success;
    }

    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Whether the operation failed
    /// </summary>
    public bool IsFailure => !Success;

    /// <summary>
    /// The data returned by the operation (null if failed)
    /// </summary>
    public T? Data { get; }

    /// <summary>
    /// The error information (null if successful)
    /// </summary>
    public ErrorResult? Error { get; }

    /// <summary>
    /// Creates a successful result with data
    /// </summary>
    public static Result<T> Ok(T data) => new(data, null, true);

    /// <summary>
    /// Creates a failed result with error
    /// </summary>
    public static Result<T> Fail(ErrorResult error) => new(default, error, false);

    /// <summary>
    /// Creates a failed result with error message and type
    /// </summary>
    public static Result<T> Fail(ErrorType type, string message, string? technicalDetails = null)
        => new(default, new ErrorResult(type, message, technicalDetails), false);

    /// <summary>
    /// Creates a failed API error result
    /// </summary>
    public static Result<T> ApiError(string message, string? technicalDetails = null)
        => Fail(ErrorType.ApiError, message, technicalDetails);

    /// <summary>
    /// Creates a failed validation error result
    /// </summary>
    public static Result<T> ValidationError(string message, string? technicalDetails = null)
        => Fail(ErrorType.ValidationError, message, technicalDetails);

    /// <summary>
    /// Creates a failed data error result
    /// </summary>
    public static Result<T> DataError(string message, string? technicalDetails = null)
        => Fail(ErrorType.DataError, message, technicalDetails);

    /// <summary>
    /// Creates a failed configuration error result
    /// </summary>
    public static Result<T> ConfigurationError(string message, string? technicalDetails = null)
        => Fail(ErrorType.ConfigurationError, message, technicalDetails);

    /// <summary>
    /// Converts successful result data to another type
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        if (IsFailure)
            return Result<TNew>.Fail(Error!);

        return Result<TNew>.Ok(mapper(Data!));
    }

    /// <summary>
    /// Executes action if result is successful
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (Success && Data != null)
            action(Data);
        return this;
    }

    /// <summary>
    /// Executes action if result failed
    /// </summary>
    public Result<T> OnFailure(Action<ErrorResult> action)
    {
        if (IsFailure && Error != null)
            action(Error);
        return this;
    }
}

/// <summary>
/// Non-generic result for operations that don't return data
/// </summary>
public class Result
{
    private Result(ErrorResult? error, bool success)
    {
        Error = error;
        Success = success;
    }

    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Whether the operation failed
    /// </summary>
    public bool IsFailure => !Success;

    /// <summary>
    /// The error information (null if successful)
    /// </summary>
    public ErrorResult? Error { get; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static Result Ok() => new(null, true);

    /// <summary>
    /// Creates a failed result with error
    /// </summary>
    public static Result Fail(ErrorResult error) => new(error, false);

    /// <summary>
    /// Creates a failed result with error message and type
    /// </summary>
    public static Result Fail(ErrorType type, string message, string? technicalDetails = null)
        => new(new ErrorResult(type, message, technicalDetails), false);

    /// <summary>
    /// Creates a failed API error result
    /// </summary>
    public static Result ApiError(string message, string? technicalDetails = null)
        => Fail(ErrorType.ApiError, message, technicalDetails);

    /// <summary>
    /// Creates a failed validation error result
    /// </summary>
    public static Result ValidationError(string message, string? technicalDetails = null)
        => Fail(ErrorType.ValidationError, message, technicalDetails);

    /// <summary>
    /// Creates a failed data error result
    /// </summary>
    public static Result DataError(string message, string? technicalDetails = null)
        => Fail(ErrorType.DataError, message, technicalDetails);

    /// <summary>
    /// Creates a failed configuration error result
    /// </summary>
    public static Result ConfigurationError(string message, string? technicalDetails = null)
        => Fail(ErrorType.ConfigurationError, message, technicalDetails);

    /// <summary>
    /// Executes action if result failed
    /// </summary>
    public Result OnFailure(Action<ErrorResult> action)
    {
        if (IsFailure && Error != null)
            action(Error);
        return this;
    }
}