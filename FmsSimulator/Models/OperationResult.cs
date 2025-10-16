namespace FmsSimulator.Models;

public class OperationResult<T>
{
    public bool IsSuccess { get; }
    public T? Data { get; }
    public string? Error { get; }
    public Dictionary<string, object>? Metrics { get; }

    private OperationResult(bool isSuccess, T? data, string? error, Dictionary<string, object>? metrics)
    {
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
        Metrics = metrics;
    }

    public static OperationResult<T> Success(T data, Dictionary<string, object>? metrics = null) =>
        new(true, data, null, metrics);

    public static OperationResult<T> Failure(string error) =>
        new(false, default, error, null);
}