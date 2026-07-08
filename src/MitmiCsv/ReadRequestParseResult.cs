namespace MitmiCsv;

public sealed class ReadRequestParseResult
{
    private ReadRequestParseResult(ReadRequest? request, string? errorMessage)
    {
        Request = request;
        ErrorMessage = errorMessage ?? string.Empty;
    }

    public bool IsValid => Request is not null;

    public ReadRequest? Request { get; }

    public string ErrorMessage { get; }

    public static ReadRequestParseResult Valid(ReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ReadRequestParseResult(request, errorMessage: null);
    }

    public static ReadRequestParseResult Invalid(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new ReadRequestParseResult(request: null, errorMessage);
    }
}
