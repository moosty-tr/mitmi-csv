using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace MitmiCsv.Tests;

public sealed class ReadRequestTests
{
    [Fact]
    public void Parse_AcceptsRequiredParametersAndDefaults()
    {
        ReadRequestParseResult result = ReadRequest.Parse(Query(
            ("host", "192.168.1.50"),
            ("unit", "1"),
            ("fc", "3"),
            ("address", "0"),
            ("count", "10")));

        Assert.True(result.IsValid);
        Assert.NotNull(result.Request);
        Assert.Equal("192.168.1.50", result.Request.Host);
        Assert.Equal(502, result.Request.Port);
        Assert.Equal(1, result.Request.UnitId);
        Assert.Equal(3, result.Request.FunctionCode);
        Assert.Equal(0, result.Request.Address);
        Assert.Equal(10, result.Request.Count);
        Assert.Equal(3000, result.Request.TimeoutMilliseconds);
        Assert.True(result.Request.IncludeHeader);
        Assert.True(result.Request.IncludeTimestamp);
    }

    [Fact]
    public void Parse_AcceptsOptionalParameters()
    {
        ReadRequestParseResult result = ReadRequest.Parse(Query(
            ("host", "modbus.local"),
            ("port", "1502"),
            ("unit", "255"),
            ("fc", "4"),
            ("address", "40001"),
            ("count", "2"),
            ("timeoutMs", "1000"),
            ("header", "false"),
            ("timestamp", "false")));

        Assert.True(result.IsValid);
        Assert.NotNull(result.Request);
        Assert.Equal(1502, result.Request.Port);
        Assert.Equal(255, result.Request.UnitId);
        Assert.Equal(40001, result.Request.Address);
        Assert.False(result.Request.IncludeHeader);
        Assert.False(result.Request.IncludeTimestamp);
    }

    [Theory]
    [InlineData("host")]
    [InlineData("unit")]
    [InlineData("fc")]
    [InlineData("address")]
    [InlineData("count")]
    public void Parse_RejectsMissingRequiredParameters(string parameterName)
    {
        List<(string Key, string Value)> values =
        [
            ("host", "192.168.1.50"),
            ("unit", "1"),
            ("fc", "3"),
            ("address", "0"),
            ("count", "1")
        ];
        values.RemoveAll(value => value.Key == parameterName);

        ReadRequestParseResult result = ReadRequest.Parse(Query(values.ToArray()));

        Assert.False(result.IsValid);
        Assert.Equal($"Missing required parameter '{parameterName}'.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_RejectsUnsupportedFunctionCode()
    {
        ReadRequestParseResult result = ReadRequest.Parse(Query(
            ("host", "192.168.1.50"),
            ("unit", "1"),
            ("fc", "5"),
            ("address", "0"),
            ("count", "1")));

        Assert.False(result.IsValid);
        Assert.Equal("Invalid parameter 'fc'. Supported values are 1, 2, 3, 4.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("1", "2001", "Invalid parameter 'count'. Function 1 supports 1 to 2000 bits.")]
    [InlineData("2", "2001", "Invalid parameter 'count'. Function 2 supports 1 to 2000 bits.")]
    [InlineData("3", "126", "Invalid parameter 'count'. Function 3 supports 1 to 125 registers.")]
    [InlineData("4", "126", "Invalid parameter 'count'. Function 4 supports 1 to 125 registers.")]
    public void Parse_RejectsCountBeyondModbusLimits(string functionCode, string count, string expected)
    {
        ReadRequestParseResult result = ReadRequest.Parse(Query(
            ("host", "192.168.1.50"),
            ("unit", "1"),
            ("fc", functionCode),
            ("address", "0"),
            ("count", count)));

        Assert.False(result.IsValid);
        Assert.Equal(expected, result.ErrorMessage);
    }

    [Fact]
    public void Parse_RejectsNonNumericCount()
    {
        ReadRequestParseResult result = ReadRequest.Parse(Query(
            ("host", "192.168.1.50"),
            ("unit", "1"),
            ("fc", "3"),
            ("address", "0"),
            ("count", "abc")));

        Assert.False(result.IsValid);
        Assert.Equal("Invalid parameter 'count'. Expected an integer.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_RejectsRangePastMaximumAddress()
    {
        ReadRequestParseResult result = ReadRequest.Parse(Query(
            ("host", "192.168.1.50"),
            ("unit", "1"),
            ("fc", "3"),
            ("address", "65535"),
            ("count", "2")));

        Assert.False(result.IsValid);
        Assert.Equal("Requested range exceeds maximum zero-based Modbus address 65535.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_RejectsUriSchemeInHost()
    {
        ReadRequestParseResult result = ReadRequest.Parse(Query(
            ("host", "http://192.168.1.50"),
            ("unit", "1"),
            ("fc", "3"),
            ("address", "0"),
            ("count", "1")));

        Assert.False(result.IsValid);
        Assert.Equal("Invalid parameter 'host'. Use a host name or IP address without a URI scheme.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_RejectsDuplicateParameters()
    {
        ReadRequestParseResult result = ReadRequest.Parse(Query(
            ("host", "192.168.1.50"),
            ("host", "192.168.1.51"),
            ("unit", "1"),
            ("fc", "3"),
            ("address", "0"),
            ("count", "1")));

        Assert.False(result.IsValid);
        Assert.Equal("Parameter 'host' must be specified once.", result.ErrorMessage);
    }

    private static QueryCollection Query(params (string Key, string Value)[] values)
    {
        Dictionary<string, StringValues> dictionary = values
            .GroupBy(value => value.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new StringValues(group.Select(value => value.Value).ToArray()),
                StringComparer.OrdinalIgnoreCase);

        return new QueryCollection(dictionary);
    }
}
