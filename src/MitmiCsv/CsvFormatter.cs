using System.Globalization;
using System.Text;

namespace MitmiCsv;

public static class CsvFormatter
{
    private const string LineEnding = "\r\n";

    public static string Format(ModbusReadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        StringBuilder builder = new();
        ReadRequest request = result.Request;

        if (request.IncludeHeader)
        {
            AppendHeader(builder, result);
        }

        if (result.IsBitResult)
        {
            AppendBitRows(builder, result);
        }
        else
        {
            AppendRegisterRows(builder, result);
        }

        return builder.ToString();
    }

    private static void AppendHeader(StringBuilder builder, ModbusReadResult result)
    {
        List<string> fields = [];

        if (result.Request.IncludeTimestamp)
        {
            fields.Add("timestamp");
        }

        fields.AddRange(["host", "port", "unit", "function", "address", "offset"]);

        if (result.IsBitResult)
        {
            fields.Add("value");
        }
        else
        {
            fields.AddRange(["raw_hex", "uint16", "int16"]);
        }

        AppendRow(builder, fields);
    }

    private static void AppendBitRows(StringBuilder builder, ModbusReadResult result)
    {
        IReadOnlyList<bool> values = result.Bits ?? throw new InvalidOperationException("Bit result has no bit values.");

        for (int offset = 0; offset < values.Count; offset++)
        {
            List<string> fields = CommonFields(result, offset);
            fields.Add(values[offset] ? "true" : "false");
            AppendRow(builder, fields);
        }
    }

    private static void AppendRegisterRows(StringBuilder builder, ModbusReadResult result)
    {
        IReadOnlyList<ushort> values = result.Registers ?? throw new InvalidOperationException("Register result has no register values.");

        for (int offset = 0; offset < values.Count; offset++)
        {
            ushort value = values[offset];
            List<string> fields = CommonFields(result, offset);
            fields.Add(value.ToString("X4", CultureInfo.InvariantCulture));
            fields.Add(value.ToString(CultureInfo.InvariantCulture));
            fields.Add(unchecked((short)value).ToString(CultureInfo.InvariantCulture));
            AppendRow(builder, fields);
        }
    }

    private static List<string> CommonFields(ModbusReadResult result, int offset)
    {
        ReadRequest request = result.Request;
        List<string> fields = [];

        if (request.IncludeTimestamp)
        {
            fields.Add(result.Timestamp.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture));
        }

        fields.Add(request.Host);
        fields.Add(request.Port.ToString(CultureInfo.InvariantCulture));
        fields.Add(request.UnitId.ToString(CultureInfo.InvariantCulture));
        fields.Add(request.FunctionCode.ToString(CultureInfo.InvariantCulture));
        fields.Add(request.Address.ToString(CultureInfo.InvariantCulture));
        fields.Add(offset.ToString(CultureInfo.InvariantCulture));

        return fields;
    }

    private static void AppendRow(StringBuilder builder, IReadOnlyList<string> fields)
    {
        for (int index = 0; index < fields.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            AppendField(builder, fields[index]);
        }

        builder.Append(LineEnding);
    }

    private static void AppendField(StringBuilder builder, string value)
    {
        if (!RequiresQuoting(value))
        {
            builder.Append(value);
            return;
        }

        builder.Append('"');
        foreach (char character in value)
        {
            if (character == '"')
            {
                builder.Append('"');
            }

            builder.Append(character);
        }

        builder.Append('"');
    }

    private static bool RequiresQuoting(string value)
    {
        return value.Contains(',', StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\r', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal);
    }
}
