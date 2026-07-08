using System.Text;

namespace MitmiCsv;

public static class ReadEndpoint
{
    public static async Task<IResult> HandleAsync(
        HttpContext context,
        IModbusReader reader,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(timeProvider);

        ReadRequestParseResult parseResult = ReadRequest.Parse(context.Request.Query);
        if (!parseResult.IsValid)
        {
            return PlainText(parseResult.ErrorMessage, StatusCodes.Status400BadRequest);
        }

        try
        {
            DateTimeOffset timestamp = timeProvider.GetLocalNow();
            ModbusReadResult readResult = await reader.ReadAsync(parseResult.Request!, timestamp, cancellationToken)
                .ConfigureAwait(false);
            string csv = CsvFormatter.Format(readResult);
            return Results.Text(csv, "text/csv", Encoding.UTF8, StatusCodes.Status200OK);
        }
        catch (ModbusReadException exception)
        {
            return PlainText(exception.Message, exception.StatusCode);
        }
    }

    private static IResult PlainText(string message, int statusCode)
    {
        return Results.Text(message + Environment.NewLine, "text/plain", Encoding.UTF8, statusCode);
    }
}
