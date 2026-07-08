using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace MitmiCsv.Tests;

public sealed class ReadEndpointTests
{
    [Fact]
    public async Task HandleAsync_ReturnsValidationErrorsAsPlainText()
    {
        DefaultHttpContext context = CreateContext("?unit=1&fc=3&address=0&count=1");
        FakeModbusReader reader = new();

        IResult result = await ReadEndpoint.HandleAsync(
            context,
            reader,
            TimeProvider.System,
            CancellationToken.None);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.StartsWith("text/plain", context.Response.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Missing required parameter 'host'." + Environment.NewLine, await ReadBodyAsync(context));
        Assert.Empty(reader.Requests);
    }

    [Fact]
    public async Task HandleAsync_ReturnsCsvOnSuccess()
    {
        DefaultHttpContext context = CreateContext("?host=device.local&unit=1&fc=3&address=0&count=1");
        FakeModbusReader reader = new([42]);
        FixedTimeProvider timeProvider = new(new DateTimeOffset(2026, 7, 8, 5, 30, 0, TimeSpan.Zero), TimeSpan.FromHours(3));

        IResult result = await ReadEndpoint.HandleAsync(
            context,
            reader,
            timeProvider,
            CancellationToken.None);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.StartsWith("text/csv", context.Response.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "timestamp,host,port,unit,function,address,offset,raw_hex,uint16,int16\r\n" +
            "2026-07-08T08:30:00+03:00,device.local,502,1,3,0,0,002A,42,42\r\n",
            await ReadBodyAsync(context));
        Assert.Single(reader.Requests);
    }

    [Fact]
    public async Task HandleAsync_ReturnsModbusErrorsAsPlainText()
    {
        DefaultHttpContext context = CreateContext("?host=device.local&unit=1&fc=3&address=0&count=1");
        FakeModbusReader reader = new()
        {
            Exception = new ModbusReadException(StatusCodes.Status502BadGateway, "Could not connect.")
        };

        IResult result = await ReadEndpoint.HandleAsync(
            context,
            reader,
            TimeProvider.System,
            CancellationToken.None);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status502BadGateway, context.Response.StatusCode);
        Assert.StartsWith("text/plain", context.Response.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Could not connect." + Environment.NewLine, await ReadBodyAsync(context));
    }

    private static DefaultHttpContext CreateContext(string queryString)
    {
        DefaultHttpContext context = new();
        context.Request.QueryString = new QueryString(queryString);
        context.RequestServices = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using StreamReader reader = new(context.Response.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    private sealed class FakeModbusReader : IModbusReader
    {
        private readonly ushort[] _registers;

        public FakeModbusReader(params ushort[] registers)
        {
            _registers = registers;
        }

        public List<ReadRequest> Requests { get; } = [];

        public ModbusReadException? Exception { get; init; }

        public Task<ModbusReadResult> ReadAsync(
            ReadRequest request,
            DateTimeOffset timestamp,
            CancellationToken cancellationToken)
        {
            _ = cancellationToken;

            Requests.Add(request);

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(ModbusReadResult.ForRegisters(request, timestamp, _registers));
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        private readonly TimeZoneInfo _localTimeZone;

        public FixedTimeProvider(DateTimeOffset utcNow, TimeSpan localOffset)
        {
            _utcNow = utcNow.ToUniversalTime();
            _localTimeZone = TimeZoneInfo.CreateCustomTimeZone("Fixed", localOffset, "Fixed", "Fixed");
        }

        public override TimeZoneInfo LocalTimeZone => _localTimeZone;

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
