namespace RssReader.Data.Results;

public sealed record Error(string Message, string Type, string? Details)
{
    public Error(Exception exception)
        : this(exception.Message, exception.GetType().Name, exception.StackTrace)
    { }

    public Error(string message, Exception exception)
        : this(message, exception.GetType().Name, exception.Message)
    { }
}
