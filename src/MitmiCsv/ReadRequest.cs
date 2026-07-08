using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace MitmiCsv;

public sealed record ReadRequest(
    string Host,
    int Port,
    byte UnitId,
    int FunctionCode,
    int Address,
    int Count,
    int TimeoutMilliseconds,
    bool IncludeHeader,
    bool IncludeTimestamp)
{
    public const int DefaultPort = 502;
    public const int DefaultTimeoutMilliseconds = 3000;
    public const bool DefaultIncludeHeader = true;
    public const bool DefaultIncludeTimestamp = true;

    private static readonly HashSet<string> KnownParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "host",
        "port",
        "unit",
        "fc",
        "address",
        "count",
        "timeoutMs",
        "header",
        "timestamp"
    };

    public static ReadRequestParseResult Parse(IQueryCollection query)
    {
        ArgumentNullException.ThrowIfNull(query);

        foreach (string key in query.Keys)
        {
            if (!KnownParameters.Contains(key))
            {
                return ReadRequestParseResult.Invalid($"Unknown parameter '{key}'.");
            }
        }

        if (!TryGetSingle(query, "host", required: true, out string? host, out string? error))
        {
            return ReadRequestParseResult.Invalid(error);
        }

        if (!ValidateHost(host, out error))
        {
            return ReadRequestParseResult.Invalid(error);
        }

        if (!TryParseInteger(query, "port", required: false, DefaultPort, 1, 65_535, out int port, out error))
        {
            return ReadRequestParseResult.Invalid(error);
        }

        if (!TryParseInteger(query, "unit", required: true, defaultValue: -1, 0, 255, out int unit, out error))
        {
            return ReadRequestParseResult.Invalid(error);
        }

        if (!TryParseInteger(query, "fc", required: true, defaultValue: -1, 0, 255, out int functionCode, out error))
        {
            return ReadRequestParseResult.Invalid(error);
        }

        if (functionCode is not (1 or 2 or 3 or 4))
        {
            return ReadRequestParseResult.Invalid("Invalid parameter 'fc'. Supported values are 1, 2, 3, 4.");
        }

        if (!TryParseInteger(query, "address", required: true, defaultValue: -1, 0, 65_535, out int address, out error))
        {
            return ReadRequestParseResult.Invalid(error);
        }

        if (!TryParseCount(query, functionCode, out int count, out error))
        {
            return ReadRequestParseResult.Invalid(error);
        }

        if (address + count - 1 > 65_535)
        {
            return ReadRequestParseResult.Invalid("Requested range exceeds maximum zero-based Modbus address 65535.");
        }

        if (!TryParseInteger(query, "timeoutMs", required: false, DefaultTimeoutMilliseconds, 1, 600_000, out int timeoutMilliseconds, out error))
        {
            return ReadRequestParseResult.Invalid(error);
        }

        if (!TryParseBoolean(query, "header", DefaultIncludeHeader, out bool includeHeader, out error))
        {
            return ReadRequestParseResult.Invalid(error);
        }

        if (!TryParseBoolean(query, "timestamp", DefaultIncludeTimestamp, out bool includeTimestamp, out error))
        {
            return ReadRequestParseResult.Invalid(error);
        }

        return ReadRequestParseResult.Valid(new ReadRequest(
            host!,
            port,
            (byte)unit,
            functionCode,
            address,
            count,
            timeoutMilliseconds,
            includeHeader,
            includeTimestamp));
    }

    private static bool ValidateHost(string? host, out string error)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            error = "Missing required parameter 'host'.";
            return false;
        }

        if (host.Contains("://", StringComparison.Ordinal))
        {
            error = "Invalid parameter 'host'. Use a host name or IP address without a URI scheme.";
            return false;
        }

        foreach (char character in host)
        {
            if (char.IsWhiteSpace(character))
            {
                error = "Invalid parameter 'host'. Host must not contain whitespace.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool TryGetSingle(
        IQueryCollection query,
        string parameterName,
        bool required,
        out string? value,
        out string error)
    {
        if (!query.TryGetValue(parameterName, out StringValues values))
        {
            if (required)
            {
                value = null;
                error = $"Missing required parameter '{parameterName}'.";
                return false;
            }

            value = null;
            error = string.Empty;
            return true;
        }

        if (values.Count != 1)
        {
            value = null;
            error = $"Parameter '{parameterName}' must be specified once.";
            return false;
        }

        value = values[0];
        error = string.Empty;
        return true;
    }

    private static bool TryParseInteger(
        IQueryCollection query,
        string parameterName,
        bool required,
        int defaultValue,
        int minimum,
        int maximum,
        out int value,
        out string error)
    {
        if (!TryGetSingle(query, parameterName, required, out string? rawValue, out error))
        {
            value = defaultValue;
            return false;
        }

        if (rawValue is null)
        {
            value = defaultValue;
            return true;
        }

        if (!int.TryParse(rawValue, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)
            || parsed < minimum
            || parsed > maximum)
        {
            value = defaultValue;
            error = $"Invalid parameter '{parameterName}'. Expected an integer between {minimum.ToString(CultureInfo.InvariantCulture)} and {maximum.ToString(CultureInfo.InvariantCulture)}.";
            return false;
        }

        value = parsed;
        error = string.Empty;
        return true;
    }

    private static bool TryParseBoolean(
        IQueryCollection query,
        string parameterName,
        bool defaultValue,
        out bool value,
        out string error)
    {
        if (!TryGetSingle(query, parameterName, required: false, out string? rawValue, out error))
        {
            value = defaultValue;
            return false;
        }

        if (rawValue is null)
        {
            value = defaultValue;
            return true;
        }

        if (string.Equals(rawValue, "true", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            error = string.Empty;
            return true;
        }

        if (string.Equals(rawValue, "false", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            error = string.Empty;
            return true;
        }

        value = defaultValue;
        error = $"Invalid parameter '{parameterName}'. Expected true or false.";
        return false;
    }

    private static bool TryParseCount(
        IQueryCollection query,
        int functionCode,
        out int value,
        out string error)
    {
        if (!TryGetSingle(query, "count", required: true, out string? rawValue, out error))
        {
            value = -1;
            return false;
        }

        if (!int.TryParse(rawValue, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed))
        {
            value = -1;
            error = "Invalid parameter 'count'. Expected an integer.";
            return false;
        }

        if (parsed < 1 || parsed > MaxCount(functionCode))
        {
            value = -1;
            error = CountRangeMessage(functionCode);
            return false;
        }

        value = parsed;
        error = string.Empty;
        return true;
    }

    private static int MaxCount(int functionCode)
    {
        return functionCode is 1 or 2 ? 2000 : 125;
    }

    private static string CountRangeMessage(int functionCode)
    {
        return functionCode is 1 or 2
            ? $"Invalid parameter 'count'. Function {functionCode.ToString(CultureInfo.InvariantCulture)} supports 1 to 2000 bits."
            : $"Invalid parameter 'count'. Function {functionCode.ToString(CultureInfo.InvariantCulture)} supports 1 to 125 registers.";
    }

}
