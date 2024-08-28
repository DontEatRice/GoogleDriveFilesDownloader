namespace GoogleDriveFilesDownloader;

public struct Result<T>
{
    private readonly bool _success;
    public readonly T Value;
    public readonly string Error;

    private Result(T v, string e, bool success)
    {
        Value = v;
        Error = e;
        _success = success;
    }

    public bool IsOk => _success;

    public static Result<T> Ok(T v)
    {
        return new Result<T>(v, null!, true);
    }

    public static Result<T> Err(string e)
    {
        return new Result<T>(default!, e, false);
    }

    public static implicit operator Result<T>(T v) => new(v, null!, true);
    public static implicit operator Result<T>(string e) => new(default!, e, false);

    public TR Match<TR>(
        Func<T, TR> success,
        Func<string, TR> failure) =>
        _success ? success(Value) : failure(Error);
}